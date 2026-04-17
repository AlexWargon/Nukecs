using System.Runtime.CompilerServices;

namespace Wargon.Nukecs
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;

    public struct AllocatorError
    {
        public const int NO_ERRORS = 0;
        public const int ERROR_ALLOCATOR_FAILED_TO_DEALLOCATE = -1;
        public const int ERROR_ALLOCATOR_MAX_BLOCKS_REACHED = -2;
        public const int ERROR_ALLOCATOR_OUT_OF_MEMORY = -3;
    }

    public class Memory
    {
        public const int MEGABYTE = 1024 * 1024;
        public const int KILOBYTE = 1024;

        public static int BytesToMegabytes(long bytes)
        {
            return (int)(bytes / 1024 / 1024);
        }
    }

    public unsafe partial struct MemAllocator : IDisposable
    {
        private const int ALIGNMENT = 16;
        private const int HEADER_SIZE = 16;
        private const int FOOTER_SIZE = 8;
        private const int OVERHEAD = HEADER_SIZE + FOOTER_SIZE;
        private const int MAX_REGIONS = 256;
        private const long NULL_OFFSET = -1L;
        private const long MIN_SPLIT_SIZE = OVERHEAD + ALIGNMENT;
        private const int NUM_BUCKETS = 16;
        public const int BIG_MEMORY_BLOCK_SIZE = 1024 * 1024;

        private static readonly int[] BucketSizes =
        {
            16, 32, 48, 64, 96, 128, 192, 256,
            384, 512, 768, 1024, 1536, 2048, 3072, 4096
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct Region
        {
            public byte* basePtr;
            public long size;
            public long cursor;
            public fixed long buckets[16];
            public long largeFreeHead;
            public int freeBlockCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BlockHeader
        {
            public long Size;
            public int RegionIndex;
            public int IsFree;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FreeNode
        {
            public long PrevFree;
            public long NextFree;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MemoryBlock
        {
            public long Pointer;
            public int Size;
            public bool IsUsed;
        }

        public struct AllocatorMarker
        {
            public int RegionIndex;
            public long Cursor;
        }

        private Region* regions;
        private Spinner* regionLocks;
        private int regionCount;
        private long initialRegionSize;
        private long totalCapacity;
        private long totalAllocated;
        private Spinner addRegionLock;

        public bool IsActive { get; private set; }
        public bool IsDisposed => !IsActive;

        public byte* BasePtr => regions != null && regionCount > 0 ? regions[0].basePtr : null;
        public long TotalSize => totalCapacity;
        public long MemoryUsed => totalAllocated;
        public int RegionCount => regionCount;

        public long MemoryLeft
        {
            get
            {
                long free = 0;
                for (int i = 0; i < regionCount; i++)
                    free += regions[i].size - regions[i].cursor;
                return free;
            }
        }

        public int TotalFreeBlocks
        {
            get
            {
                int count = 0;
                for (int i = 0; i < regionCount; i++)
                    count += regions[i].freeBlockCount;
                return count;
            }
        }

        public long FreeListMemory
        {
            get
            {
                long total = 0;
                for (int i = 0; i < regionCount; i++)
                {
                    regionLocks[i].Acquire();
                    ref var region = ref regions[i];
                    for (int b = 0; b < NUM_BUCKETS; b++)
                    {
                        long cur = region.buckets[b];
                        while (cur != NULL_OFFSET)
                        {
                            var h = (BlockHeader*)(region.basePtr + cur);
                            total += h->Size + OVERHEAD;
                            var node = (FreeNode*)(region.basePtr + cur + HEADER_SIZE);
                            cur = node->NextFree;
                        }
                    }
                    long lCur = region.largeFreeHead;
                    while (lCur != NULL_OFFSET)
                    {
                        var h = (BlockHeader*)(region.basePtr + lCur);
                        total += h->Size + OVERHEAD;
                        var node = (FreeNode*)(region.basePtr + lCur + HEADER_SIZE);
                        lCur = node->NextFree;
                    }
                    regionLocks[i].Release();
                }
                return total;
            }
        }

        public MemAllocator(long sizeInBytes)
        {
            initialRegionSize = Math.Max(sizeInBytes, 4096);
            regions = (Region*)UnsafeUtility.Malloc(sizeof(Region) * MAX_REGIONS, ALIGNMENT, Allocator.Persistent);
            UnsafeUtility.MemClear(regions, sizeof(Region) * MAX_REGIONS);
            regionLocks = (Spinner*)UnsafeUtility.Malloc(sizeof(Spinner) * MAX_REGIONS, ALIGNMENT, Allocator.Persistent);
            UnsafeUtility.MemClear(regionLocks, sizeof(Spinner) * MAX_REGIONS);
            regionCount = 0;
            totalCapacity = 0;
            totalAllocated = 0;
            addRegionLock = new Spinner();
            IsActive = true;
            AddRegionInternal(initialRegionSize);
        }

        public static MemAllocator* New(long sizeInBytes)
        {
            var ptr = (MemAllocator*)UnsafeUtility.MallocTracked(sizeof(MemAllocator),
                UnsafeUtility.AlignOf<MemAllocator>(), Allocator.Persistent, 0);
            *ptr = new MemAllocator(sizeInBytes);
            return ptr;
        }

        public static void Destroy(MemAllocator* allocator)
        {
            allocator->Dispose();
            UnsafeUtility.FreeTracked(allocator, Allocator.Persistent);
        }

        public byte* GetRegionPtr(int index)
        {
            return regions[index].basePtr;
        }

        public ref Region GetRegion(int index)
        {
            return ref regions[index];
        }

        public AllocatorMarker GetMarker()
        {
            int lastRegion = regionCount - 1;
            return new AllocatorMarker { RegionIndex = lastRegion, Cursor = regions[lastRegion].cursor };
        }

        public AllocatorMarker GetMarker(int regionIndex)
        {
            return new AllocatorMarker { RegionIndex = regionIndex, Cursor = regions[regionIndex].cursor };
        }

        public void Rewind(AllocatorMarker marker)
        {
            addRegionLock.Acquire();
            for (int i = regionCount - 1; i > marker.RegionIndex; i--)
            {
                Interlocked.Add(ref totalAllocated, -regions[i].cursor);
                UnsafeUtility.Free(regions[i].basePtr, Allocator.Persistent);
                totalCapacity -= regions[i].size;
            }
            regionCount = marker.RegionIndex + 1;
            ref var region = ref regions[marker.RegionIndex];
            Interlocked.Add(ref totalAllocated, -(region.cursor - marker.Cursor));
            region.cursor = marker.Cursor;
            region.largeFreeHead = NULL_OFFSET;
            region.freeBlockCount = 0;
            for (int b = 0; b < NUM_BUCKETS; b++)
                region.buckets[b] = NULL_OFFSET;
            addRegionLock.Release();
        }

        private void AddRegionInternal(long sizeInBytes)
        {
            if (regionCount >= MAX_REGIONS)
                throw new InvalidOperationException($"Max regions ({MAX_REGIONS}) reached");

            var basePtr = (byte*)UnsafeUtility.Malloc(sizeInBytes, ALIGNMENT, Allocator.Persistent);
            regions[regionCount] = new Region
            {
                basePtr = basePtr,
                size = sizeInBytes,
                cursor = 0,
                largeFreeHead = NULL_OFFSET,
                freeBlockCount = 0
            };
            for (int b = 0; b < NUM_BUCKETS; b++)
                regions[regionCount].buckets[b] = NULL_OFFSET;
            totalCapacity += sizeInBytes;
            regionCount++;
        }

        private static void SizeWithAlign(ref long size, int align)
        {
            size = (size + align - 1) / align * align;
        }

        private static int GetBucketIndex(long userSize)
        {
            for (int i = 0; i < NUM_BUCKETS; i++)
            {
                if (userSize <= BucketSizes[i])
                    return i;
            }
            return -1;
        }

        private static void WriteFooter(byte* regionBase, long headerOffset, long totalBlockSize)
        {
            *(long*)(regionBase + headerOffset + totalBlockSize - FOOTER_SIZE) = totalBlockSize;
        }

        private static void RemoveFromList(ref Region region, int bucketIndex, long headerOffset)
        {
            byte* basePtr = region.basePtr;
            var node = (FreeNode*)(basePtr + headerOffset + HEADER_SIZE);

            if (node->PrevFree != NULL_OFFSET)
            {
                var prevNode = (FreeNode*)(basePtr + node->PrevFree + HEADER_SIZE);
                prevNode->NextFree = node->NextFree;
            }
            else if (bucketIndex >= 0)
            {
                region.buckets[bucketIndex] = node->NextFree;
            }
            else
            {
                region.largeFreeHead = node->NextFree;
            }

            if (node->NextFree != NULL_OFFSET)
            {
                var nextNode = (FreeNode*)(basePtr + node->NextFree + HEADER_SIZE);
                nextNode->PrevFree = node->PrevFree;
            }

            region.freeBlockCount--;
        }

        private static void AddToList(ref Region region, int bucketIndex, long headerOffset)
        {
            byte* basePtr = region.basePtr;
            var node = (FreeNode*)(basePtr + headerOffset + HEADER_SIZE);

            long oldHead;
            if (bucketIndex >= 0)
            {
                oldHead = region.buckets[bucketIndex];
                region.buckets[bucketIndex] = headerOffset;
            }
            else
            {
                oldHead = region.largeFreeHead;
                region.largeFreeHead = headerOffset;
            }

            node->PrevFree = NULL_OFFSET;
            node->NextFree = oldHead;

            if (oldHead != NULL_OFFSET)
            {
                var oldHeadNode = (FreeNode*)(basePtr + oldHead + HEADER_SIZE);
                oldHeadNode->PrevFree = headerOffset;
            }

            region.freeBlockCount++;
        }

        private static void CoalescingFree(ref Region region, int regionIndex, long headerOffset)
        {
            byte* base1 = region.basePtr;
            var header = (BlockHeader*)(base1 + headerOffset);
            long totalBlockSize = header->Size + OVERHEAD;
            long coalescedSize = totalBlockSize;
            long coalescedOffset = headerOffset;

            if (headerOffset + totalBlockSize < region.cursor)
            {
                long nextOffset = headerOffset + totalBlockSize;
                var nextHeader = (BlockHeader*)(base1 + nextOffset);
                if (nextHeader->IsFree != 0)
                {
                    int nextBucket = GetBucketIndex(nextHeader->Size);
                    RemoveFromList(ref region, nextBucket, nextOffset);
                    coalescedSize += nextHeader->Size + OVERHEAD;
                }
            }

            if (coalescedOffset >= OVERHEAD && coalescedOffset < region.cursor)
            {
                long prevTotalSize = *(long*)(base1 + coalescedOffset - FOOTER_SIZE);
                if (prevTotalSize >= OVERHEAD && prevTotalSize <= coalescedOffset)
                {
                    long prevHeaderOffset = coalescedOffset - prevTotalSize;
                    if (prevHeaderOffset >= 0)
                    {
                        var prevHeader = (BlockHeader*)(base1 + prevHeaderOffset);
                        if (prevHeader->IsFree != 0 && prevHeader->Size + OVERHEAD == prevTotalSize)
                        {
                            int prevBucket = GetBucketIndex(prevHeader->Size);
                            RemoveFromList(ref region, prevBucket, prevHeaderOffset);
                            coalescedSize += prevTotalSize;
                            coalescedOffset = prevHeaderOffset;
                        }
                    }
                }
            }

            var finalHeader = (BlockHeader*)(base1 + coalescedOffset);
            finalHeader->Size = coalescedSize - OVERHEAD;
            finalHeader->RegionIndex = regionIndex;
            finalHeader->IsFree = 1;
            WriteFooter(base1, coalescedOffset, coalescedSize);

            if (coalescedOffset + coalescedSize == region.cursor)
            {
                region.cursor = coalescedOffset;
                return;
            }

            int bucket = GetBucketIndex(finalHeader->Size);
            AddToList(ref region, bucket, coalescedOffset);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LockRegion(int i) => regionLocks[i].Acquire();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UnlockRegion(int i) => regionLocks[i].Release();

        private bool TryAllocateFromRegion(int regionIndex, long userSize, long totalSize, int bucketHint, out ptr_offset result)
        {
            LockRegion(regionIndex);
            ref var region = ref regions[regionIndex];

            if (bucketHint >= 0)
            {
                for (int b = bucketHint; b < NUM_BUCKETS; b++)
                {
                    if (region.buckets[b] != NULL_OFFSET)
                    {
                        long found = region.buckets[b];
                        RemoveFromList(ref region, b, found);
                        result = FinalizeAllocation(ref region, regionIndex, found, userSize, totalSize);
                        UnlockRegion(regionIndex);
                        return true;
                    }
                }
            }

            if (region.largeFreeHead != NULL_OFFSET)
            {
                long found = FindBestFitLarge(ref region, totalSize);
                if (found != NULL_OFFSET)
                {
                    RemoveFromList(ref region, -1, found);
                    result = FinalizeAllocation(ref region, regionIndex, found, userSize, totalSize);
                    UnlockRegion(regionIndex);
                    return true;
                }
            }

            if (region.cursor + totalSize <= region.size)
            {
                var headerOffset = region.cursor;
                var header = (BlockHeader*)(region.basePtr + headerOffset);
                header->Size = userSize;
                header->RegionIndex = regionIndex;
                header->IsFree = 0;
                WriteFooter(region.basePtr, headerOffset, totalSize);
                region.cursor += totalSize;
                Interlocked.Add(ref totalAllocated, totalSize);
                result = new ptr_offset((uint)regionIndex, (uint)(headerOffset + HEADER_SIZE));
                UnlockRegion(regionIndex);
                return true;
            }

            UnlockRegion(regionIndex);
            result = default;
            return false;
        }

        private ptr_offset AllocateCore(long sizeInBytes)
        {
            SizeWithAlign(ref sizeInBytes, ALIGNMENT);
            var totalSize = sizeInBytes + OVERHEAD;
            int bucketHint = GetBucketIndex(sizeInBytes);

            int snapshot = regionCount;
            for (int i = 0; i < snapshot; i++)
            {
                if (TryAllocateFromRegion(i, sizeInBytes, totalSize, bucketHint, out var r))
                    return r;
            }

            addRegionLock.Acquire();

            for (int i = snapshot; i < regionCount; i++)
            {
                if (TryAllocateFromRegion(i, sizeInBytes, totalSize, bucketHint, out var r))
                {
                    addRegionLock.Release();
                    return r;
                }
            }

            var newRegionSize = Math.Max(initialRegionSize, totalSize * 2);
            AddRegionInternal(newRegionSize);
            int ri = regionCount - 1;

            LockRegion(ri);
            var hOffset = regions[ri].cursor;
            var h = (BlockHeader*)(regions[ri].basePtr + hOffset);
            h->Size = sizeInBytes;
            h->RegionIndex = ri;
            h->IsFree = 0;
            WriteFooter(regions[ri].basePtr, hOffset, totalSize);
            regions[ri].cursor += totalSize;
            Interlocked.Add(ref totalAllocated, totalSize);
            UnlockRegion(ri);

            addRegionLock.Release();
            return new ptr_offset((uint)ri, (uint)(hOffset + HEADER_SIZE));
        }

        private ptr_offset FinalizeAllocation(ref Region region, int regionIndex, long headerOffset, long userSize, long requestedTotal)
        {
            var header = (BlockHeader*)(region.basePtr + headerOffset);
            long availableTotal = header->Size + OVERHEAD;
            long remainder = availableTotal - requestedTotal;

            header->IsFree = 0;
            header->RegionIndex = regionIndex;
            Interlocked.Add(ref totalAllocated, requestedTotal);

            if (remainder >= MIN_SPLIT_SIZE)
            {
                header->Size = userSize;
                WriteFooter(region.basePtr, headerOffset, requestedTotal);

                long splitOffset = headerOffset + requestedTotal;
                var splitHeader = (BlockHeader*)(region.basePtr + splitOffset);
                splitHeader->Size = remainder - OVERHEAD;
                splitHeader->RegionIndex = regionIndex;
                splitHeader->IsFree = 1;
                WriteFooter(region.basePtr, splitOffset, remainder);

                if (splitOffset + remainder == region.cursor)
                    region.cursor = splitOffset;
                else
                {
                    int splitBucket = GetBucketIndex(splitHeader->Size);
                    AddToList(ref region, splitBucket, splitOffset);
                }
            }
            else
            {
                header->Size = availableTotal - OVERHEAD;
                WriteFooter(region.basePtr, headerOffset, availableTotal);
            }

            return new ptr_offset((uint)regionIndex, (uint)(headerOffset + HEADER_SIZE));
        }

        private static long FindBestFitLarge(ref Region region, long totalSize)
        {
            long bestOffset = NULL_OFFSET;
            long bestSize = long.MaxValue;
            long current = region.largeFreeHead;

            while (current != NULL_OFFSET)
            {
                var header = (BlockHeader*)(region.basePtr + current);
                long blockSize = header->Size + OVERHEAD;

                if (blockSize >= totalSize && blockSize < bestSize)
                {
                    bestOffset = current;
                    bestSize = blockSize;
                    if (blockSize == totalSize) break;
                }

                var node = (FreeNode*)(region.basePtr + current + HEADER_SIZE);
                current = node->NextFree;
            }

            return bestOffset;
        }

        private void FreeCore(int regionIndex, long userOffset)
        {
            if (regionIndex < 0 || regionIndex >= regionCount) return;

            LockRegion(regionIndex);
            ref var region = ref regions[regionIndex];
            long headerOffset = userOffset - HEADER_SIZE;
            var header = (BlockHeader*)(region.basePtr + headerOffset);
            if (header->IsFree != 0)
            {
                UnlockRegion(regionIndex);
                return;
            }

            long blockSize = header->Size + OVERHEAD;
            CoalescingFree(ref region, regionIndex, headerOffset);
            UnlockRegion(regionIndex);

            Interlocked.Add(ref totalAllocated, -blockSize);
        }

        public ptr_offset AllocateRaw(long sizeInBytes) => AllocateCore(sizeInBytes);

        public IntPtr AllocateRaw(long sizeInBytes, ref int error)
        {
            var off = AllocateCore(sizeInBytes);
            if (off.BlockIndex == uint.MaxValue)
            {
                error = AllocatorError.ERROR_ALLOCATOR_OUT_OF_MEMORY;
                return IntPtr.Zero;
            }
            error = 0;
            return (IntPtr)(regions[off.BlockIndex].basePtr + off.Offset);
        }

        public void* Allocate(long sizeInBytes)
        {
            var off = AllocateCore(sizeInBytes);
            if (off.BlockIndex == uint.MaxValue) return null;
            return regions[off.BlockIndex].basePtr + off.Offset;
        }

        public ptr<T> AllocatePtr<T>() where T : unmanaged => AllocatePtr<T>(sizeof(T));

        public ptr<T> AllocatePtr<T>(long sizeInBytes) where T : unmanaged
        {
            var off = AllocateCore(sizeInBytes);
            if (off.BlockIndex == uint.MaxValue) return ptr<T>.NULL;
            return new ptr<T>(regions[off.BlockIndex].basePtr, off);
        }

        public ptr AllocatePtr(long sizeInBytes)
        {
            var off = AllocateCore(sizeInBytes);
            if (off.BlockIndex == uint.MaxValue) return ptr.NULL;
            return new ptr(regions[off.BlockIndex].basePtr, off);
        }

        public void Free(ptr p)
        {
            if (p.IsNull) return;
            FreeCore((int)p.offset.BlockIndex, p.offset.Offset);
        }

        public void Free<T>(ptr<T> p) where T : unmanaged
        {
            if (p.IsNull) return;
            FreeCore((int)p.offset.BlockIndex, p.offset.Offset);
        }

        public void Free(void* p)
        {
            if (p == null) return;
            var header = (BlockHeader*)((byte*)p - HEADER_SIZE);
            int ri = header->RegionIndex;
            if (ri < 0 || ri >= regionCount) return;
            FreeCore(ri, (long)((byte*)p - regions[ri].basePtr));
        }

        public void Free(uint p) => FreeCore(0, p);

        public void Free(ptr_offset p)
        {
            if (p.IsNull) return;
            FreeCore((int)p.BlockIndex, p.Offset);
        }

        public void Free(ptr_offset p, ref int error)
        {
            if (p.IsNull)
            {
                error = AllocatorError.ERROR_ALLOCATOR_FAILED_TO_DEALLOCATE;
                return;
            }
            FreeCore((int)p.BlockIndex, p.Offset);
            error = AllocatorError.NO_ERRORS;
        }

        public void Free(byte* p)
        {
            if (p == null) return;
            var header = (BlockHeader*)(p - HEADER_SIZE);
            int ri = header->RegionIndex;
            if (ri < 0 || ri >= regionCount) return;
            FreeCore(ri, (long)(p - regions[ri].basePtr));
        }

        public void Free(byte* p, ref int error)
        {
            if (p == null)
            {
                error = AllocatorError.ERROR_ALLOCATOR_FAILED_TO_DEALLOCATE;
                return;
            }
            var header = (BlockHeader*)(p - HEADER_SIZE);
            int ri = header->RegionIndex;
            if (ri < 0 || ri >= regionCount)
            {
                error = AllocatorError.ERROR_ALLOCATOR_FAILED_TO_DEALLOCATE;
                return;
            }
            FreeCore(ri, (long)(p - regions[ri].basePtr));
            error = AllocatorError.NO_ERRORS;
        }

        public void Free(uint p, ref int error)
        {
            FreeCore(0, p);
            error = AllocatorError.NO_ERRORS;
        }

        public void Dispose()
        {
            addRegionLock.Acquire();
            for (int i = 0; i < regionCount; i++)
            {
                if (regions[i].basePtr != null)
                    UnsafeUtility.Free(regions[i].basePtr, Allocator.Persistent);
            }
            if (regions != null)
            {
                UnsafeUtility.Free(regions, Allocator.Persistent);
                regions = null;
            }
            if (regionLocks != null)
            {
                UnsafeUtility.Free(regionLocks, Allocator.Persistent);
                regionLocks = null;
            }
            regionCount = 0;
            totalCapacity = 0;
            totalAllocated = 0;
            IsActive = false;
            addRegionLock.Release();
        }

        public long GetTotalSize() => totalCapacity;

        public (long totalSize, long usedSize, long freeSize, int regionCount) GetMemoryInfo()
        {
            return (totalCapacity, totalAllocated, totalCapacity - totalAllocated, regionCount);
        }

        public MemoryView GetMemoryView()
        {
            return new MemoryView
            {
                Regions = regions,
                RegionCount = regionCount,
                memoryUsed = totalAllocated
            };
        }
    }

    public unsafe class MemoryView
    {
        public MemAllocator.Region* Regions;
        public int RegionCount;
        public long memoryUsed;
        [Obsolete("Use Regions instead")]
        public MemAllocator.MemoryBlock* Blocks => null;
        [Obsolete("Use RegionCount instead")]
        public int BlockCount => RegionCount;
    }

    public interface IOnDeserialize
    {
        void OnDeserialize(ref MemAllocator memoryAllocator);
    }

    namespace Allocators
    {
        public enum Allocator
        {
            World,
            OneFrame,
            UnityPersistnace,
            UnityTemp,
            UnityJobs
        }
    }
}

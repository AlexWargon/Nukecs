namespace Wargon.Nukecs
{
    using System;
    using System.Runtime.InteropServices;
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
        private const int INITIAL_REGION_CAPACITY = 64;
        private const long FREE_LIST_END = -1;
        private const long MIN_SPLIT_SIZE = HEADER_SIZE + ALIGNMENT * 2;
        public const int BIG_MEMORY_BLOCK_SIZE = 1024 * 1024;

        [StructLayout(LayoutKind.Sequential)]
        public struct Region
        {
            public byte* basePtr;
            public long size;
            public long cursor;
            public long freeListHead;
            public int freeBlockCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AllocHeader
        {
            public long Size;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MemoryBlock
        {
            public long Pointer;
            public int Size;
            public bool IsUsed;
        }

        private Region* regions;
        private int regionCount;
        private int regionCapacity;
        private long initialRegionSize;
        private long totalCapacity;
        private long totalAllocated;
        private Spinner spinner;

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
                    long offset = regions[i].freeListHead;
                    while (offset != FREE_LIST_END)
                    {
                        var header = (AllocHeader*)(regions[i].basePtr + offset);
                        total += header->Size + HEADER_SIZE;
                        long nextOffset = *(long*)(regions[i].basePtr + offset + HEADER_SIZE);
                        offset = nextOffset;
                    }
                }
                return total;
            }
        }

        public MemAllocator(long sizeInBytes)
        {
            initialRegionSize = Math.Max(sizeInBytes, 4096);
            regionCapacity = INITIAL_REGION_CAPACITY;
            regions = (Region*)UnsafeUtility.Malloc(sizeof(Region) * regionCapacity, ALIGNMENT, Allocator.Persistent);
            UnsafeUtility.MemClear(regions, sizeof(Region) * regionCapacity);
            regionCount = 0;
            totalCapacity = 0;
            totalAllocated = 0;
            spinner = new Spinner();
            IsActive = true;
            AddRegion(initialRegionSize);
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

        private void AddRegion(long sizeInBytes)
        {
            if (regionCount >= regionCapacity)
            {
                var newCap = regionCapacity * 2;
                var newRegions = (Region*)UnsafeUtility.Malloc(sizeof(Region) * newCap, ALIGNMENT, Allocator.Persistent);
                UnsafeUtility.MemCpy(newRegions, regions, sizeof(Region) * regionCount);
                UnsafeUtility.MemClear(newRegions + regionCount, sizeof(Region) * (newCap - regionCount));
                UnsafeUtility.Free(regions, Allocator.Persistent);
                regions = newRegions;
                regionCapacity = newCap;
            }

            var basePtr = (byte*)UnsafeUtility.Malloc(sizeInBytes, ALIGNMENT, Allocator.Persistent);
            UnsafeUtility.MemClear(basePtr, sizeInBytes);
            regions[regionCount] = new Region
            {
                basePtr = basePtr,
                size = sizeInBytes,
                cursor = 0,
                freeListHead = FREE_LIST_END,
                freeBlockCount = 0
            };
            totalCapacity += sizeInBytes;
            regionCount++;
        }

        private static void SizeWithAlign(ref long size, int align)
        {
            size = (size + align - 1) / align * align;
        }

        private ptr_offset AllocateCore(long sizeInBytes)
        {
            SizeWithAlign(ref sizeInBytes, ALIGNMENT);
            var totalSize = sizeInBytes + HEADER_SIZE;

            for (int i = 0; i < regionCount; i++)
            {
                var found = TryAllocateFromFreeList(i, sizeInBytes, totalSize);
                if (found.BlockIndex != uint.MaxValue)
                    return found;

                if (regions[i].cursor + totalSize <= regions[i].size)
                {
                    var headerOffset = regions[i].cursor;
                    var header = (AllocHeader*)(regions[i].basePtr + headerOffset);
                    header->Size = sizeInBytes;
                    regions[i].cursor += totalSize;
                    totalAllocated += totalSize;
                    return new ptr_offset((uint)i, (uint)(headerOffset + HEADER_SIZE));
                }
            }

            var newSize = Math.Max(initialRegionSize, totalSize * 2);
            AddRegion(newSize);
            ref var r = ref regions[regionCount - 1];
            var hOffset = r.cursor;
            var h = (AllocHeader*)(r.basePtr + hOffset);
            h->Size = sizeInBytes;
            r.cursor += totalSize;
            totalAllocated += totalSize;
            return new ptr_offset((uint)(regionCount - 1), (uint)(hOffset + HEADER_SIZE));
        }

        private ptr_offset TryAllocateFromFreeList(int regionIndex, long userSize, long totalSize)
        {
            ref var region = ref regions[regionIndex];
            if (region.freeListHead == FREE_LIST_END)
                return ptr_offset.NULL;

            long currentOffset = region.freeListHead;
            bool isHead = true;
            long prevOffset = FREE_LIST_END;

            while (currentOffset != FREE_LIST_END)
            {
                var header = (AllocHeader*)(region.basePtr + currentOffset);
                long blockSize = header->Size + HEADER_SIZE;
                long nextOffset = *(long*)(region.basePtr + currentOffset + HEADER_SIZE);

                if (blockSize >= totalSize)
                {
                    long remainder = blockSize - totalSize;
                    if (remainder >= MIN_SPLIT_SIZE)
                    {
                        long splitOffset = currentOffset + totalSize;
                        var splitHeader = (AllocHeader*)(region.basePtr + splitOffset);
                        splitHeader->Size = remainder - HEADER_SIZE;
                        *(long*)(region.basePtr + splitOffset + HEADER_SIZE) = nextOffset;

                        if (isHead)
                            region.freeListHead = splitOffset;
                        else
                            *(long*)(region.basePtr + prevOffset + HEADER_SIZE) = splitOffset;

                        header->Size = userSize;
                    }
                    else
                    {
                        if (isHead)
                            region.freeListHead = nextOffset;
                        else
                            *(long*)(region.basePtr + prevOffset + HEADER_SIZE) = nextOffset;
                        region.freeBlockCount--;
                    }

                    totalAllocated += totalSize;
                    return new ptr_offset((uint)regionIndex, (uint)(currentOffset + HEADER_SIZE));
                }

                prevOffset = currentOffset;
                currentOffset = nextOffset;
                isHead = false;
            }

            return ptr_offset.NULL;
        }

        private void FreeCore(int regionIndex, long userOffset)
        {
            if (regionIndex < 0 || regionIndex >= regionCount)
                return;

            ref var region = ref regions[regionIndex];
            long headerOffset = userOffset - HEADER_SIZE;
            var header = (AllocHeader*)(region.basePtr + headerOffset);
            long totalSize = header->Size + HEADER_SIZE;

            if (headerOffset + totalSize == region.cursor)
            {
                region.cursor = headerOffset;
                totalAllocated -= totalSize;
                return;
            }

            long* nextPtr = (long*)(region.basePtr + userOffset);
            *nextPtr = region.freeListHead;
            region.freeListHead = headerOffset;
            region.freeBlockCount++;
            totalAllocated -= totalSize;
        }

        private int FindRegionIndex(void* ptr)
        {
            for (int i = 0; i < regionCount; i++)
            {
                if (ptr >= regions[i].basePtr && ptr < regions[i].basePtr + regions[i].size)
                    return i;
            }
            return -1;
        }

        public ptr_offset AllocateRaw(long sizeInBytes)
        {
            spinner.Acquire();
            var result = AllocateCore(sizeInBytes);
            spinner.Release();
            return result;
        }

        public IntPtr AllocateRaw(long sizeInBytes, ref int error)
        {
            spinner.Acquire();
            var off = AllocateCore(sizeInBytes);
            spinner.Release();
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
            spinner.Acquire();
            var off = AllocateCore(sizeInBytes);
            spinner.Release();
            if (off.BlockIndex == uint.MaxValue) return null;
            return regions[off.BlockIndex].basePtr + off.Offset;
        }

        public ptr<T> AllocatePtr<T>() where T : unmanaged
        {
            return AllocatePtr<T>(sizeof(T));
        }

        public ptr<T> AllocatePtr<T>(long sizeInBytes) where T : unmanaged
        {
            spinner.Acquire();
            var off = AllocateCore(sizeInBytes);
            spinner.Release();
            if (off.BlockIndex == uint.MaxValue) return ptr<T>.NULL;
            return new ptr<T>(regions[off.BlockIndex].basePtr, off);
        }

        public ptr AllocatePtr(long sizeInBytes)
        {
            spinner.Acquire();
            var off = AllocateCore(sizeInBytes);
            spinner.Release();
            if (off.BlockIndex == uint.MaxValue) return ptr.NULL;
            return new ptr(regions[off.BlockIndex].basePtr, off);
        }

        public void Free(ptr p)
        {
            if (p.IsNull) return;
            spinner.Acquire();
            FreeCore((int)p.offset.BlockIndex, p.offset.Offset);
            spinner.Release();
        }

        public void Free<T>(ptr<T> p) where T : unmanaged
        {
            if (p.IsNull) return;
            spinner.Acquire();
            FreeCore((int)p.offset.BlockIndex, p.offset.Offset);
            spinner.Release();
        }

        public void Free(void* p)
        {
            if (p == null) return;
            spinner.Acquire();
            int regionIndex = FindRegionIndex(p);
            if (regionIndex >= 0)
                FreeCore(regionIndex, (long)((byte*)p - regions[regionIndex].basePtr));
            spinner.Release();
        }

        public void Free(uint p)
        {
            spinner.Acquire();
            FreeCore(0, p);
            spinner.Release();
        }

        public void Free(ptr_offset p)
        {
            if (p.IsNull) return;
            spinner.Acquire();
            FreeCore((int)p.BlockIndex, p.Offset);
            spinner.Release();
        }

        public void Free(ptr_offset p, ref int error)
        {
            if (p.IsNull)
            {
                error = AllocatorError.ERROR_ALLOCATOR_FAILED_TO_DEALLOCATE;
                return;
            }
            spinner.Acquire();
            FreeCore((int)p.BlockIndex, p.Offset);
            spinner.Release();
            error = AllocatorError.NO_ERRORS;
        }

        public void Free(byte* p)
        {
            if (p == null) return;
            spinner.Acquire();
            int regionIndex = FindRegionIndex(p);
            if (regionIndex >= 0)
                FreeCore(regionIndex, (long)(p - regions[regionIndex].basePtr));
            spinner.Release();
        }

        public void Free(byte* p, ref int error)
        {
            if (p == null)
            {
                error = AllocatorError.ERROR_ALLOCATOR_FAILED_TO_DEALLOCATE;
                return;
            }
            spinner.Acquire();
            int regionIndex = FindRegionIndex(p);
            if (regionIndex >= 0)
            {
                FreeCore(regionIndex, (long)(p - regions[regionIndex].basePtr));
                error = AllocatorError.NO_ERRORS;
            }
            else
            {
                error = AllocatorError.ERROR_ALLOCATOR_FAILED_TO_DEALLOCATE;
            }
            spinner.Release();
        }

        public void Free(uint p, ref int error)
        {
            spinner.Acquire();
            FreeCore(0, p);
            spinner.Release();
            error = AllocatorError.NO_ERRORS;
        }

        public void Dispose()
        {
            spinner.Release();
            for (int i = 0; i < regionCount; i++)
            {
                if (regions[i].basePtr != null)
                {
                    UnsafeUtility.Free(regions[i].basePtr, Allocator.Persistent);
                }
            }

            if (regions != null)
            {
                UnsafeUtility.Free(regions, Allocator.Persistent);
                regions = null;
            }

            regionCount = 0;
            totalCapacity = 0;
            totalAllocated = 0;
            IsActive = false;
        }

        public long GetTotalSize()
        {
            return totalCapacity;
        }

        public (long totalSize, long usedSize, long freeSize, int regionCount) GetMemoryInfo()
        {
            return (totalCapacity, totalAllocated, totalCapacity - totalAllocated, regionCount);
        }
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

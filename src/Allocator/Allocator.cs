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
        public static int BytesToMegabytes(long bytes) => (int)(bytes / 1024 / 1024);
        public static int BytesToKilobytes(long bytes) => (int)(bytes / 1024);
    }

    public unsafe partial struct MemAllocator : IDisposable
    {
        private const int ALIGN = 16;
        private const int HDR = 16;
        private const int FTR = 8;
        private const int OVR = HDR + FTR;
        private const long NPOS = -1L;
        private const int MAX_REGIONS = 256;
        public const int BIG_MEMORY_BLOCK_SIZE = 1024 * 1024;

        // Block layout:
        // [Size:8 (neg=free, pos=alloc)] [NextFree:8] [|Size| bytes data] [TotalSize:8]
        // TotalSize = |Size| + OVR. Footer enables backward coalescing.

        [StructLayout(LayoutKind.Sequential)]
        public struct Region
        {
            public byte* basePtr;
            public long size;
            public long cursor;
            public long freeHead;
            public int freeCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Header { public long Size; public long NextFree; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MemoryBlock { public long Pointer; public int Size; public bool IsUsed; }

        public struct AllocatorMarker { public int RegionIndex; public long Cursor; }

        private Region* regions;
        private int regionCount;
        private long initialRegionSize;
        private long totalCapacity;
        private long totalAllocated;
        private Spinner lock_;

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
                long f = 0;
                for (int i = 0; i < regionCount; i++)
                    f += regions[i].size - regions[i].cursor;
                return f;
            }
        }

        public int TotalFreeBlocks
        {
            get
            {
                int c = 0;
                for (int i = 0; i < regionCount; i++) c += regions[i].freeCount;
                return c;
            }
        }

        public long FreeListMemory
        {
            get
            {
                long t = 0;
                for (int i = 0; i < regionCount; i++)
                {
                    long cur = regions[i].freeHead;
                    while (cur != NPOS)
                    {
                        var h = (Header*)(regions[i].basePtr + cur);
                        t += (h->Size < 0 ? -h->Size : h->Size) + OVR;
                        cur = h->NextFree;
                    }
                }
                return t;
            }
        }

        public MemAllocator(long sizeInBytes)
        {
            initialRegionSize = Math.Max(sizeInBytes, 4096);
            regions = (Region*)UnsafeUtility.Malloc(sizeof(Region) * MAX_REGIONS, ALIGN, Allocator.Persistent);
            UnsafeUtility.MemClear(regions, sizeof(Region) * MAX_REGIONS);
            regionCount = 0;
            totalCapacity = 0;
            totalAllocated = 0;
            lock_ = new Spinner();
            IsActive = true;
            AddRegion(initialRegionSize);
        }

        public static MemAllocator* New(long sizeInBytes)
        {
            var p = (MemAllocator*)UnsafeUtility.MallocTracked(
                sizeof(MemAllocator), UnsafeUtility.AlignOf<MemAllocator>(), Allocator.Persistent, 0);
            *p = new MemAllocator(sizeInBytes);
            return p;
        }

        public static void Destroy(MemAllocator* a)
        {
            a->Dispose();
            UnsafeUtility.FreeTracked(a, Allocator.Persistent);
        }

        public byte* GetRegionPtr(int i) => regions[i].basePtr;
        public ref Region GetRegion(int i) => ref regions[i];

        public AllocatorMarker GetMarker()
        {
            int last = regionCount - 1;
            return new AllocatorMarker { RegionIndex = last, Cursor = regions[last].cursor };
        }

        public AllocatorMarker GetMarker(int i)
            => new AllocatorMarker { RegionIndex = i, Cursor = regions[i].cursor };

        public void Rewind(AllocatorMarker m)
        {
            lock_.Acquire();
            for (int i = regionCount - 1; i > m.RegionIndex; i--)
            {
                totalAllocated -= regions[i].cursor;
                UnsafeUtility.Free(regions[i].basePtr, Allocator.Persistent);
                totalCapacity -= regions[i].size;
            }
            regionCount = m.RegionIndex + 1;
            ref var r = ref regions[m.RegionIndex];
            totalAllocated -= r.cursor - m.Cursor;
            r.cursor = m.Cursor;
            r.freeHead = NPOS;
            r.freeCount = 0;
            lock_.Release();
        }

        private void AddRegion(long sz)
        {
            if (regionCount >= MAX_REGIONS)
                throw new InvalidOperationException($"Max regions ({MAX_REGIONS})");
            var bp = (byte*)UnsafeUtility.Malloc(sz, ALIGN, Allocator.Persistent);
            regions[regionCount] = new Region
            {
                basePtr = bp, size = sz, cursor = 0, freeHead = NPOS, freeCount = 0
            };
            totalCapacity += sz;
            regionCount++;
        }

        private static long A16(long v) => (v + 15) / 16 * 16;

        private static void WriteF(byte* b, long hOff, long total)
            => *(long*)(b + hOff + total - FTR) = total;

        private static void RemoveFree(ref Region r, byte* bp, long target)
        {
            if (r.freeHead == target)
            {
                r.freeHead = ((Header*)(bp + target))->NextFree;
                return;
            }
            long cur = r.freeHead;
            while (cur != NPOS)
            {
                var h = (Header*)(bp + cur);
                if (h->NextFree == target)
                {
                    h->NextFree = ((Header*)(bp + target))->NextFree;
                    return;
                }
                cur = h->NextFree;
            }
        }

        private void* Alloc(long size)
        {
            size = A16(Math.Max(size, ALIGN));
            long total = size + OVR;

            lock_.Acquire();

            for (int i = 0; i < regionCount; i++)
            {
                ref var r = ref regions[i];
                byte* bp = r.basePtr;

                long prevOff = NPOS;
                long curOff = r.freeHead;
                while (curOff != NPOS)
                {
                    var h = (Header*)(bp + curOff);
                    long freeSz = -h->Size;

                    if (freeSz >= size)
                    {
                        if (prevOff == NPOS) r.freeHead = h->NextFree;
                        else ((Header*)(bp + prevOff))->NextFree = h->NextFree;
                        r.freeCount--;

                        long remain = freeSz - size;

                        if (remain >= OVR + ALIGN)
                        {
                            h->Size = size;
                            WriteF(bp, curOff, size + OVR);

                            long sOff = curOff + size + OVR;
                            long sData = remain - OVR;
                            var sh = (Header*)(bp + sOff);
                            sh->Size = -sData;
                            sh->NextFree = r.freeHead;
                            r.freeHead = sOff;
                            WriteF(bp, sOff, remain);
                            r.freeCount++;

                            totalAllocated += size + OVR;
                        }
                        else
                        {
                            h->Size = freeSz;
                            WriteF(bp, curOff, freeSz + OVR);
                            totalAllocated += freeSz + OVR;
                        }

                        lock_.Release();
                        return bp + curOff + HDR;
                    }

                    prevOff = curOff;
                    curOff = h->NextFree;
                }

                if (r.cursor + total <= r.size)
                {
                    long off = r.cursor;
                    var hh = (Header*)(bp + off);
                    hh->Size = size;
                    hh->NextFree = 0;
                    WriteF(bp, off, total);
                    r.cursor += total;
                    totalAllocated += total;
                    lock_.Release();
                    return bp + off + HDR;
                }
            }

            var ns = Math.Max(initialRegionSize, total * 2);
            AddRegion(ns);
            int ri = regionCount - 1;
            ref var rn = ref regions[ri];
            var hn = (Header*)(rn.basePtr);
            hn->Size = size;
            hn->NextFree = 0;
            WriteF(rn.basePtr, 0, total);
            rn.cursor = total;
            totalAllocated += total;
            lock_.Release();
            return rn.basePtr + HDR;
        }

        private void Dealloc(int ri, long userOffset)
        {
            lock_.Acquire();
            if (ri < 0 || ri >= regionCount) { lock_.Release(); return; }

            ref var r = ref regions[ri];
            byte* bp = r.basePtr;
            long hOff = userOffset - HDR;
            var h = (Header*)(bp + hOff);
            long size = h->Size;
            if (size <= 0) { lock_.Release(); return; }

            h->Size = -size;

            long merged = size;
            long mOff = hOff;
            bool inList = false;

            // Forward coalescing
            long nextOff = hOff + size + OVR;
            if (nextOff < r.cursor)
            {
                var nh = (Header*)(bp + nextOff);
                if (nh->Size < 0)
                {
                    RemoveFree(ref r, bp, nextOff);
                    r.freeCount--;
                    merged += (-nh->Size) + OVR;
                }
            }

            // Backward coalescing
            if (hOff >= OVR)
            {
                long pTotal = *(long*)(bp + hOff - FTR);
                if (pTotal >= OVR && pTotal <= hOff)
                {
                    long pOff = hOff - pTotal;
                    var ph = (Header*)(bp + pOff);
                    if (ph->Size < 0)
                    {
                        merged += (-ph->Size) + OVR;
                        mOff = pOff;
                        inList = true;
                    }
                }
            }

            var fh = (Header*)(bp + mOff);
            fh->Size = -merged;
            long mTotal = merged + OVR;
            WriteF(bp, mOff, mTotal);

            totalAllocated -= size + OVR;

            if (mOff + mTotal == r.cursor)
            {
                if (inList) { RemoveFree(ref r, bp, mOff); r.freeCount--; }
                r.cursor = mOff;
            }
            else if (!inList)
            {
                fh->NextFree = r.freeHead;
                r.freeHead = mOff;
                r.freeCount++;
            }

            lock_.Release();
        }

        private int FindRegion(void* ptr)
        {
            for (int i = 0; i < regionCount; i++)
                if (ptr >= regions[i].basePtr && ptr < regions[i].basePtr + regions[i].size)
                    return i;
            return -1;
        }

        public ptr_offset AllocateRaw(long size) => ToOff(Alloc(size));
        public void* Allocate(long size) => Alloc(size);

        public IntPtr AllocateRaw(long size, ref int error)
        {
            var p = Alloc(size);
            if (p == null) { error = AllocatorError.ERROR_ALLOCATOR_OUT_OF_MEMORY; return IntPtr.Zero; }
            error = 0;
            return (IntPtr)p;
        }

        public ptr<T> AllocatePtr<T>() where T : unmanaged => AllocatePtr<T>(sizeof(T));

        public ptr<T> AllocatePtr<T>(long size) where T : unmanaged
        {
            var p = Alloc(size);
            if (p == null) return ptr<T>.NULL;
            int ri = FindRegion(p);
            long off = (byte*)p - regions[ri].basePtr;
            return new ptr<T>(regions[ri].basePtr, new ptr_offset((uint)ri, (uint)off));
        }

        public ptr AllocatePtr(long size)
        {
            var p = Alloc(size);
            if (p == null) return ptr.NULL;
            int ri = FindRegion(p);
            long off = (byte*)p - regions[ri].basePtr;
            return new ptr(regions[ri].basePtr, new ptr_offset((uint)ri, (uint)off));
        }

        private ptr_offset ToOff(void* p)
        {
            if (p == null) return ptr_offset.NULL;
            int ri = FindRegion(p);
            long off = (byte*)p - regions[ri].basePtr;
            return new ptr_offset((uint)ri, (uint)off);
        }

        public void Free(ptr p) { if (!p.IsNull) Dealloc((int)p.offset.BlockIndex, p.offset.Offset); }
        public void Free<T>(ptr<T> p) where T : unmanaged { if (!p.IsNull) Dealloc((int)p.offset.BlockIndex, p.offset.Offset); }

        public void Free(void* p)
        {
            if (p == null) return;
            int ri = FindRegion(p);
            if (ri < 0) return;
            Dealloc(ri, (long)((byte*)p - regions[ri].basePtr));
        }

        public void Free(uint p) => Dealloc(0, p);
        public void Free(ptr_offset p) { if (!p.IsNull) Dealloc((int)p.BlockIndex, p.Offset); }

        public void Free(ptr_offset p, ref int error)
        {
            if (p.IsNull) { error = AllocatorError.ERROR_ALLOCATOR_FAILED_TO_DEALLOCATE; return; }
            Dealloc((int)p.BlockIndex, p.Offset);
            error = AllocatorError.NO_ERRORS;
        }

        public void Free(byte* p)
        {
            if (p == null) return;
            int ri = FindRegion(p);
            if (ri < 0) return;
            Dealloc(ri, (long)(p - regions[ri].basePtr));
        }

        public void Free(byte* p, ref int error)
        {
            if (p == null) { error = AllocatorError.ERROR_ALLOCATOR_FAILED_TO_DEALLOCATE; return; }
            int ri = FindRegion(p);
            if (ri < 0) { error = AllocatorError.ERROR_ALLOCATOR_FAILED_TO_DEALLOCATE; return; }
            Dealloc(ri, (long)(p - regions[ri].basePtr));
            error = AllocatorError.NO_ERRORS;
        }

        public void Free(uint p, ref int error) { Dealloc(0, p); error = AllocatorError.NO_ERRORS; }

        public void Dispose()
        {
            lock_.Acquire();
            for (int i = 0; i < regionCount; i++)
                if (regions[i].basePtr != null)
                    UnsafeUtility.Free(regions[i].basePtr, Allocator.Persistent);
            if (regions != null) { UnsafeUtility.Free(regions, Allocator.Persistent); regions = null; }
            regionCount = 0;
            totalCapacity = 0;
            totalAllocated = 0;
            IsActive = false;
            lock_.Release();
        }

        public long GetTotalSize() => totalCapacity;
        public (long totalSize, long usedSize, long freeSize, int regionCount) GetMemoryInfo()
            => (totalCapacity, totalAllocated, totalCapacity - totalAllocated, regionCount);
        public MemoryView GetMemoryView() => new MemoryView
        {
            Regions = regions, RegionCount = regionCount, memoryUsed = totalAllocated
        };
    }

    public unsafe class MemoryView
    {
        public MemAllocator.Region* Regions;
        public int RegionCount;
        public long memoryUsed;
        [Obsolete("Use Regions instead")] public MemAllocator.MemoryBlock* Blocks => null;
        [Obsolete("Use RegionCount instead")] public int BlockCount => RegionCount;
    }

    public interface IOnDeserialize { void OnDeserialize(ref MemAllocator memoryAllocator); }

    namespace Allocators
    {
        public enum Allocator { World, OneFrame, UnityPersistnace, UnityTemp, UnityJobs }
    }
}

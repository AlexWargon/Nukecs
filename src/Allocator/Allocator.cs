namespace Wargon.Nukecs
{
    using System;
    using System.Runtime.CompilerServices;
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
            // freed headers below the marker cursor must not stay as free-list ghosts
            for (int i = 0; i < regionCount; i++)
                RebuildFreeList(i);
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

        /// <summary>
        /// Rebuilds a region's free-list by walking block headers from the region start up to
        /// the cursor. Used after FastDeserialize: the saved bytes contain freed headers
        /// (negative Size, e.g. from DynamicBitmask.EnsureCapacity growth) while the Region
        /// metadata was reset — leaving them unrelinked lets later Dealloc/Alloc coalesce
        /// against ghost blocks and corrupt the arena.
        /// </summary>
        private unsafe void RebuildFreeList(int ri)
        {
            ref var r = ref regions[ri];
            byte* bp = r.basePtr;
            r.freeHead = NPOS;
            r.freeCount = 0;
            long tail = NPOS;
            long hOff = 0;
            // every block advances at least ALIGN + OVR bytes — the walk is finite;
            // break on a malformed header (corrupt save) keeping whatever was relinked
            while (hOff + HDR <= r.cursor)
            {
                var h = (Header*)(bp + hOff);
                long sz = h->Size;
                long data = sz < 0 ? -sz : sz;
                if (data < ALIGN) break;
                if (sz < 0)
                {
                    h->NextFree = NPOS;
                    if (tail == NPOS) r.freeHead = hOff;
                    else ((Header*)(bp + tail))->NextFree = hOff;
                    tail = hOff;
                    r.freeCount++;
                }
                hOff += data + OVR;
            }
        }

        private void* Alloc(long size, int tag) {
            // Arena Guard: canary adds 16 guard bytes to the END of the aligned data slot
            // (slot = A16(size) + 16). Guard presence is marked in NextFree bit 32 so
            // Validate can check it later; flags OFF → layout identical to canary-less.
            var guard = (AllocatorDebugState.Mode & AllocatorDebugMode.Canary) != 0;
            if (guard) size += 16;
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
                            h->NextFree = TagWord(tag, guard);
                            WriteF(bp, curOff, size + OVR);

                            long sOff = curOff + size + OVR;
                            long sData = remain - OVR;
                            var sh = (Header*)(bp + sOff);
                            sh->Size = -sData;
                            sh->NextFree = r.freeHead;
                            r.freeHead = sOff;
                            WriteF(bp, sOff, remain);
                            r.freeCount++;
                            if ((AllocatorDebugState.Mode & AllocatorDebugMode.PoisonFree) != 0)
                                PoisonFirst16(bp + sOff + HDR);

                            totalAllocated += size + OVR;
                        }
                        else
                        {
                            h->Size = freeSz;
                            h->NextFree = TagWord(tag, guard);
                            WriteF(bp, curOff, freeSz + OVR);
                            totalAllocated += freeSz + OVR;
                        }

                        var dataPtr = bp + curOff + HDR;
                        if (guard) WriteGuard(dataPtr + size - 16, i, curOff);
                        lock_.Release();
                        return dataPtr;
                    }

                    prevOff = curOff;
                    curOff = h->NextFree;
                }

                if (r.cursor + total <= r.size)
                {
                    long off = r.cursor;
                    var hh = (Header*)(bp + off);
                    hh->Size = size;
                    hh->NextFree = TagWord(tag, guard);
                    WriteF(bp, off, total);
                    r.cursor += total;
                    totalAllocated += total;
                    var dataPtr = bp + off + HDR;
                    if (guard) WriteGuard(dataPtr + size - 16, i, off);
                    lock_.Release();
                    return dataPtr;
                }
            }

            var ns = Math.Max(initialRegionSize, total * 2);
            AddRegion(ns);
            int ri = regionCount - 1;
            ref var rn = ref regions[ri];
            var hn = (Header*)(rn.basePtr);
            hn->Size = size;
            hn->NextFree = TagWord(tag, guard);
            WriteF(rn.basePtr, 0, total);
            rn.cursor = total;
            totalAllocated += total;
            var dataPtrNew = rn.basePtr + HDR;
            if (guard) WriteGuard(dataPtrNew + size - 16, ri, 0);
            lock_.Release();
            return dataPtrNew;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long TagWord(int tag, bool guard) => tag | (guard ? 1L << 32 : 0L);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteGuard(byte* guardPtr, int ri, long hOff)
        {
            var m0 = 0x5A5AA5A5DEADBEEFUL ^ ((ulong)(uint)ri << 32) ^ (ulong)hOff;
            ((ulong*)guardPtr)[0] = m0;
            ((ulong*)guardPtr)[1] = ~m0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool GuardIntact(byte* guardPtr, int ri, long hOff)
        {
            var m0 = 0x5A5AA5A5DEADBEEFUL ^ ((ulong)(uint)ri << 32) ^ (ulong)hOff;
            return ((ulong*)guardPtr)[0] == m0 && ((ulong*)guardPtr)[1] == ~m0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PoisonFirst16(byte* userPtr)
        {
            ((ulong*)userPtr)[0] = 0xDDDDDDDDDDDDDDDDUL;
            ((ulong*)userPtr)[1] = 0xDDDDDDDDDDDDDDDDUL;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool PoisonIntact(byte* userPtr)
        {
            return ((ulong*)userPtr)[0] == 0xDDDDDDDDDDDDDDDDUL
                && ((ulong*)userPtr)[1] == 0xDDDDDDDDDDDDDDDDUL;
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
            // Arena Guard: poison the first 16 user bytes so later writes into this freed
            // block (use-after-free) are detected by Validate
            if ((AllocatorDebugState.Mode & AllocatorDebugMode.PoisonFree) != 0)
                PoisonFirst16(bp + hOff + HDR);

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

        public ptr_offset AllocateRaw(long size, int tag = 0) => ToOff(Alloc(size, tag));
        public void* Allocate(long size, int tag = 0) => Alloc(size, tag);

        public IntPtr AllocateRaw(long size, ref int error, int tag = 0)
        {
            var p = Alloc(size, tag);
            if (p == null) { error = AllocatorError.ERROR_ALLOCATOR_OUT_OF_MEMORY; return IntPtr.Zero; }
            error = 0;
            return (IntPtr)p;
        }

        public ptr<T> AllocatePtr<T>() where T : unmanaged => AllocatePtr<T>(sizeof(T));

        public ptr<T> AllocatePtr<T>(long size, int tag = 0) where T : unmanaged
        {
            var p = Alloc(size, tag);
            if (p == null) return ptr<T>.NULL;
            int ri = FindRegion(p);
            long off = (byte*)p - regions[ri].basePtr;
            return new ptr<T>(regions[ri].basePtr, new ptr_offset((uint)ri, (uint)off));
        }

        public ptr AllocatePtr(long size, int tag = 0)
        {
            var p = Alloc(size, tag);
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

        // ------------------------------------------------------------------
        // Arena Guard: corruption validation + leak/tag statistics
        // ------------------------------------------------------------------

        /// <summary>
        /// Walks every region's block chain and checks: header sanity (size aligned,
        /// chain within cursor), canary guards of canary-allocated blocks (OOB writes past
        /// live allocations) and poison of freed blocks (use-after-free writes).
        /// Limitation: freeing the LAST block of a region rewinds its cursor (Dealloc) —
        /// such blocks leave the chain, and UAF writes into the rewound tail are invisible
        /// to this walk by construction (that space is unallocated region memory).
        /// Burst-safe core: no managed types, caller reports via ValidateAndReport.
        /// </summary>
        public bool Validate(out AllocatorDebugState.Violation violation)
        {
            var checkGuards = (AllocatorDebugState.Mode & AllocatorDebugMode.Canary) != 0;
            var checkPoison = (AllocatorDebugState.Mode & AllocatorDebugMode.PoisonFree) != 0;
            violation = default;

            for (int ri = 0; ri < regionCount; ri++)
            {
                ref var r = ref regions[ri];
                byte* bp = r.basePtr;
                if (bp == null) continue;
                long hOff = 0;
                while (hOff + HDR <= r.cursor)
                {
                    var h = (Header*)(bp + hOff);
                    long data = h->Size < 0 ? -h->Size : h->Size;
                    if (data < ALIGN || (data & 15) != 0)
                    {
                        violation = NewViolation(ri, hOff, data, h, AllocatorDebugState.ViolationKind.BadHeaderSize);
                        return false;
                    }
                    if (hOff + data + OVR > r.cursor)
                    {
                        violation = NewViolation(ri, hOff, data, h, AllocatorDebugState.ViolationKind.ChainOutOfCursor);
                        return false;
                    }
                    if (h->Size >= 0)
                    {
                        // live block: guard check (only for blocks allocated with canary ON)
                        if (checkGuards && (h->NextFree & (1L << 32)) != 0
                            && !GuardIntact(bp + hOff + HDR + data - 16, ri, hOff))
                        {
                            violation = NewViolation(ri, hOff, data, h, AllocatorDebugState.ViolationKind.CanaryBroken);
                            return false;
                        }
                    }
                    else if (checkPoison && data >= 16 && !PoisonIntact(bp + hOff + HDR))
                    {
                        violation = NewViolation(ri, hOff, data, h, AllocatorDebugState.ViolationKind.FreedBlockWritten);
                        return false;
                    }
                    hOff += data + OVR;
                }
            }
            return true;
        }

        private static AllocatorDebugState.Violation NewViolation(
            int ri, long hOff, long data, Header* h, AllocatorDebugState.ViolationKind kind)
            => new AllocatorDebugState.Violation
            {
                Region = ri,
                BlockOffset = hOff,
                DataSize = data,
                Tag = (int)(h->NextFree & 0xFFFFFFFF),
                Kind = kind
            };

        /// <summary>Managed wrapper: validate + log the result (report goes through [BurstDiscard]).</summary>
        public bool ValidateAndReport(string context)
        {
            if (Validate(out var v))
            {
                AllocatorDebugState.ReportClean(context, 0, totalAllocated);
                return true;
            }
            AllocatorDebugState.Report(in v, context);
            return false;
        }

        /// <summary>
        /// Poisons the first 16 user bytes of every currently free block. Call when
        /// PoisonFree is switched ON mid-session: blocks freed before the switch would
        /// otherwise hold arbitrary bytes and trip Validate with false positives.
        /// </summary>
        public unsafe void PoisonAllFree()
        {
            for (int ri = 0; ri < regionCount; ri++)
            {
                ref var r = ref regions[ri];
                byte* bp = r.basePtr;
                if (bp == null) continue;
                long hOff = 0;
                while (hOff + HDR <= r.cursor)
                {
                    var h = (Header*)(bp + hOff);
                    long data = h->Size < 0 ? -h->Size : h->Size;
                    if (data < ALIGN || (data & 15) != 0) break;
                    if (h->Size < 0 && data >= 16)
                        PoisonFirst16(bp + hOff + HDR);
                    hOff += data + OVR;
                }
            }
        }

        /// <summary>Fixed-capacity per-tag aggregation of LIVE blocks (leak/growth signal).</summary>
        public struct TagStats
        {
            public const int Capacity = 32;
            public fixed int Tags[Capacity];
            public fixed long Counts[Capacity];
            public fixed long Bytes[Capacity];
            public int Length;
        }

        public unsafe void GetTagStats(ref TagStats stats)
        {
            stats.Length = 0;
            for (int ri = 0; ri < regionCount; ri++)
            {
                ref var r = ref regions[ri];
                byte* bp = r.basePtr;
                if (bp == null) continue;
                long hOff = 0;
                while (hOff + HDR <= r.cursor)
                {
                    var h = (Header*)(bp + hOff);
                    long data = h->Size < 0 ? -h->Size : h->Size;
                    if (data < ALIGN || (data & 15) != 0) break;
                    if (h->Size >= 0)
                    {
                        var tag = (int)(h->NextFree & 0xFFFFFFFF);
                        var slot = -1;
                        for (var i = 0; i < stats.Length; i++)
                            if (stats.Tags[i] == tag) { slot = i; break; }
                        if (slot < 0 && stats.Length < TagStats.Capacity)
                        {
                            slot = stats.Length++;
                            stats.Tags[slot] = tag;
                            stats.Counts[slot] = 0;
                            stats.Bytes[slot] = 0;
                        }
                        if (slot >= 0)
                        {
                            stats.Counts[slot]++;
                            stats.Bytes[slot] += data;
                        }
                    }
                    hOff += data + OVR;
                }
            }
        }

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

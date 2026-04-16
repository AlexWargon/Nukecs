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
        private const int INITIAL_REGION_CAPACITY = 64;
        public const int BIG_MEMORY_BLOCK_SIZE = 1024 * 1024;

        [StructLayout(LayoutKind.Sequential)]
        public struct Region
        {
            public byte* basePtr;
            public long size;
            public long cursor;
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
                cursor = 0
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
            for (int i = 0; i < regionCount; i++)
            {
                if (regions[i].cursor + sizeInBytes <= regions[i].size)
                {
                    var offset = (uint)regions[i].cursor;
                    regions[i].cursor += sizeInBytes;
                    totalAllocated += sizeInBytes;
                    return new ptr_offset((uint)i, offset);
                }
            }

            var newSize = Math.Max(initialRegionSize, sizeInBytes * 2);
            AddRegion(newSize);
            ref var r = ref regions[regionCount - 1];
            var result = (uint)r.cursor;
            r.cursor += sizeInBytes;
            totalAllocated += sizeInBytes;
            return new ptr_offset((uint)(regionCount - 1), result);
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

        public void Free(ptr p) { }
        public void Free<T>(ptr<T> p) where T : unmanaged { }
        public void Free(void* p) { }
        public void Free(uint p) { }
        public void Free(ptr_offset p) { }
        public void Free(ptr_offset p, ref int error) { error = 0; }
        public void Free(byte* p) { }
        public void Free(byte* p, ref int error) { error = 0; }
        public void Free(uint p, ref int error) { error = 0; }

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

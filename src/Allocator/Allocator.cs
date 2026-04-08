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
    [StructLayout(LayoutKind.Sequential)]
    public unsafe partial struct MemAllocator : IDisposable
    {
        private int maxBlocks;
        private const int ALIGNMENT = 16;
        public const int BIG_MEMORY_BLOCK_SIZE = 1024 * 1024;
        [StructLayout(LayoutKind.Sequential)]
        public struct MemoryBlock
        {
            public long Pointer;
            public long Size;
            public bool IsUsed;
        }

        private byte* basePtr;
        private long totalSize;
        private MemoryBlock* blocks;
        private int blockCount;
        private long memoryUsed;
        private int defragmentationCount;
        private Spinner spinner;
        private const int SIZE_CLASS_COUNT = 11;
        private int* freeListHeads;
        private int* freeListNext;
        private bool freeListDirty;
        public long MemoryLeft => totalSize - memoryUsed;
        public byte* BasePtr
        {
            get => basePtr;
            set => basePtr = value;
        }

        public long TotalSize
        {
            get => totalSize;
            set => totalSize = value;
        }

        public MemoryBlock* Blocks
        {
            get => blocks;
            set => blocks = value;
        }

        public int BlockCount
        {
            get => blockCount;
            set => blockCount = value;
        }
        public bool IsActive { get; private set; }
        public bool IsDisposed => !IsActive;
        
        public MemAllocator(long sizeInBytes, int maxBlocks = 65536)
        {
            this.maxBlocks = maxBlocks;
            totalSize = sizeInBytes;
            freeListDirty = false;
            basePtr = (byte*)UnsafeUtility.Malloc(totalSize, ALIGNMENT, Allocator.Persistent);
            blocks = (MemoryBlock*)UnsafeUtility.Malloc(sizeof(MemoryBlock) * this.maxBlocks, ALIGNMENT, Allocator.Persistent);
            UnsafeUtility.MemClear(basePtr, totalSize);
            UnsafeUtility.MemClear(blocks, sizeof(MemoryBlock) * this.maxBlocks);
            freeListHeads = (int*)UnsafeUtility.Malloc(sizeof(int) * SIZE_CLASS_COUNT, ALIGNMENT, Allocator.Persistent);
            freeListNext = (int*)UnsafeUtility.Malloc(sizeof(int) * this.maxBlocks, ALIGNMENT, Allocator.Persistent);
            UnsafeUtility.MemSet(freeListHeads, 0xFF, sizeof(int) * SIZE_CLASS_COUNT);
            UnsafeUtility.MemSet(freeListNext, 0xFF, sizeof(int) * this.maxBlocks);
            blocks[0] = new MemoryBlock
            {
                Pointer = 0,
                Size = totalSize,
                IsUsed = false,
            };
            blockCount = 1;
            defragmentationCount = 0;
            memoryUsed = 0;
            spinner = new Spinner();
            IsActive = true;
            AddToFreeList(0, totalSize);
        }

        public static MemAllocator* New(long sizeInBytes, int maxBlocks = 65536)
        {
            var ptr = (MemAllocator*)UnsafeUtility.MallocTracked(sizeof(MemAllocator), 
                UnsafeUtility.AlignOf<MemAllocator>(),
                Allocator.Persistent, 0);
            *ptr = new MemAllocator(sizeInBytes, maxBlocks);
            return ptr;
        }

        public static void Destroy(MemAllocator* allocator)
        {
            allocator->Dispose();
            UnsafeUtility.FreeTracked(allocator, Allocator.Persistent);
        }
        private static int GetSizeClassIndex(long size)
        {
            if (size <= 64) return 0;
            if (size <= 128) return 1;
            if (size <= 256) return 2;
            if (size <= 512) return 3;
            if (size <= 1024) return 4;
            if (size <= 2048) return 5;
            if (size <= 4096) return 6;
            if (size <= 8192) return 7;
            if (size <= 16384) return 8;
            if (size <= 32768) return 9;
            return 10;
        }

        private void RebuildFreeList()
        {
            UnsafeUtility.MemSet(freeListHeads, 0xFF, sizeof(int) * SIZE_CLASS_COUNT);
            for (var i = 0; i < blockCount; i++)
            {
                if (!blocks[i].IsUsed)
                {
                    var sc = GetSizeClassIndex(blocks[i].Size);
                    freeListNext[i] = freeListHeads[sc];
                    freeListHeads[sc] = i;
                }
            }
        }

        private void AddToFreeList(int blockIdx, long size)
        {
            var sc = GetSizeClassIndex(size);
            freeListNext[blockIdx] = freeListHeads[sc];
            freeListHeads[sc] = blockIdx;
        }

        private IntPtr TryAllocate(long sizeInBytes, ref int error, out int blockIndex)
        {
            blockIndex = -1;
            if (freeListDirty)
            {
                RebuildFreeList();
                freeListDirty = false;
            }
            var sc = GetSizeClassIndex(sizeInBytes);
            for (var c = sc; c < SIZE_CLASS_COUNT; c++)
            {
                var fi = freeListHeads[c];
                var prev = -1;
                while (fi >= 0)
                {
                    ref var block = ref blocks[fi];
                    if (block.Size >= sizeInBytes)
                    {
                        if (prev >= 0) freeListNext[prev] = freeListNext[fi];
                        else freeListHeads[c] = freeListNext[fi];
                        freeListNext[fi] = -1;
                        if (block.Size > sizeInBytes)
                        {
                            InsertBlock(fi + 1, block.Pointer + sizeInBytes, block.Size - sizeInBytes, false, ref error);
                            if (error == 0)
                                AddToFreeList(fi + 1, block.Size - sizeInBytes);
                        }
                        block.Size = sizeInBytes;
                        block.IsUsed = true;
                        blockIndex = fi;
                        return (IntPtr)(basePtr + block.Pointer);
                    }
                    prev = fi;
                    fi = freeListNext[fi];
                }
            }
            for (var i = 0; i < blockCount; i++)
            {
                ref var block = ref blocks[i];
                if (!block.IsUsed && block.Size >= sizeInBytes)
                {
                    if (block.Size > sizeInBytes)
                        InsertBlock(i + 1, block.Pointer + sizeInBytes, block.Size - sizeInBytes, false, ref error);
                    block.Size = sizeInBytes;
                    block.IsUsed = true;
                    blockIndex = i;
                    return (IntPtr)(basePtr + block.Pointer);
                }
            }
            return IntPtr.Zero;
        }

        private IntPtr AllocateInternal(long sizeInBytes, ref int error, out int blockIndex)
        {
            SizeWithAlign(ref sizeInBytes, ALIGNMENT);
            spinner.Acquire();
            var result = TryAllocate(sizeInBytes, ref error, out blockIndex);
            if (result == IntPtr.Zero)
            {
                DeFragment();
                result = TryAllocate(sizeInBytes, ref error, out blockIndex);
            }
            spinner.Release();
            return result;
        }

        public IntPtr AllocateRaw(long sizeInBytes, ref int error)
        {
            var result = AllocateInternal(sizeInBytes, ref error, out _);
            if (result == IntPtr.Zero)
                error = AllocatorError.ERROR_ALLOCATOR_OUT_OF_MEMORY;
            return result;
        }

        public ptr_offset AllocateRaw(long sizeInBytes)
        {
            var error = 0;
            var result = AllocateInternal(sizeInBytes, ref error, out var blockIndex);
            if (result == IntPtr.Zero)
                return ptr_offset.NULL;
            return new ptr_offset(0, (uint)blocks[blockIndex].Pointer);
        }

        public void* Allocate(long sizeInBytes)
        {
            var error = 0;
            return (void*)AllocateInternal(sizeInBytes, ref error, out _);
        }

        public ptr<T> AllocatePtr<T>() where T : unmanaged
        {
            return AllocatePtr<T>(sizeof(T));
        }

        public ptr<T> AllocatePtr<T>(long sizeInBytes) where T : unmanaged
        {
            var error = 0;
            var result = AllocateInternal(sizeInBytes, ref error, out var blockIndex);
            if (result == IntPtr.Zero)
                return ptr<T>.NULL;
            return new ptr<T>(basePtr, (uint)blocks[blockIndex].Pointer);
        }

        public ptr AllocatePtr(long sizeInBytes)
        {
            var error = 0;
            var result = AllocateInternal(sizeInBytes, ref error, out var blockIndex);
            if (result == IntPtr.Zero)
                return ptr.NULL;
            return new ptr(basePtr, (uint)blocks[blockIndex].Pointer);
        }
        private void SizeWithAlign(ref long size, int align)
        {
            size = (size + align - 1) / align * align;
        }
        public void Free(ptr ptr)
        {
            var error = 0;
            Free(ptr.offset, ref error);
        }
        public void Free<T>(ptr<T> ptr) where T : unmanaged
        {
            var error = 0;
            Free(ptr.offset, ref error);
        }
        public void Free(void* ptr)
        {
            var error = 0;
            Free((byte*)ptr, ref error);
        }

        public void Free(uint ptr)
        {
            var error = 0;
            Free(ptr, ref error);
        }
        public void Free(ptr_offset ptr, ref int error)
        {
            spinner.Acquire();
            var offset = ptr.Offset;
            for (var i = 0; i < blockCount; i++)
            {
                ref var block = ref blocks[i];
                
                if (block.Pointer == offset)
                {
                    block.IsUsed = false;
                    memoryUsed -= block.Size;
                    AddToFreeList(i, block.Size);
                    spinner.Release();
                    return;
                }
            }
            spinner.Release();
            
            error = AllocatorError.ERROR_ALLOCATOR_FAILED_TO_DEALLOCATE;
        }
        public void Free(byte* ptr, ref int error)
        {
            spinner.Acquire();
            var offset = ptr - basePtr;
            for (var i = 0; i < blockCount; i++)
            {
                ref var block = ref blocks[i];
                
                if (block.Pointer == offset)
                {
                    block.IsUsed = false;
                    memoryUsed -= block.Size;
                    AddToFreeList(i, block.Size);
                    spinner.Release();
                    return;
                }
            }
            spinner.Release();
            
            error = AllocatorError.ERROR_ALLOCATOR_FAILED_TO_DEALLOCATE;
        }
        public void Free(uint ptr, ref int error)
        {
            spinner.Acquire();
            var offset = ptr;
            for (var i = 0; i < blockCount; i++)
            {
                ref var block = ref blocks[i];
                
                if (block.Pointer == offset)
                {
                    block.IsUsed = false;
                    memoryUsed -= block.Size;
                    AddToFreeList(i, block.Size);
                    spinner.Release();
                    return;
                }
            }
            spinner.Release();
            
            error = AllocatorError.ERROR_ALLOCATOR_FAILED_TO_DEALLOCATE;
        }
        private void DeFragment()
        {
            freeListDirty = true;
            for (var i = 0; i < blockCount - 1; i++)
            {
                if (!blocks[i].IsUsed && !blocks[i + 1].IsUsed)
                {
                    blocks[i].Size += blocks[i + 1].Size;
                    RemoveBlock(i + 1);
                    i--;
                }
            }
            defragmentationCount++;
        }

        private void InsertBlock(int index, long offset, long size, bool isUsed, ref int error)
        {
            freeListDirty = true;
            if (blockCount >= maxBlocks)
            {
                error = AllocatorError.ERROR_ALLOCATOR_MAX_BLOCKS_REACHED;
                return;
            }

            for (var i = blockCount; i > index; i--) 
                blocks[i] = blocks[i - 1];

            blocks[index] = new MemoryBlock
            {
                Pointer = offset,
                Size = size,
                IsUsed = isUsed
            };
            blockCount++;
            memoryUsed += size;
            error = 0;
        }

        private void RemoveBlock(int index)
        {
            freeListDirty = true;
            for (var i = index; i < blockCount - 1; i++)
            {
                blocks[i] = blocks[i + 1];
                blocks[i + 1].IsUsed = false;
            }
            blockCount--;
        }

        public void Dispose()
        {
            spinner.Release();
            if (basePtr != null)
            {
                UnsafeUtility.Free(basePtr, Allocator.Persistent);
                basePtr = null;
            }

            if (blocks != null)
            {
                UnsafeUtility.Free(blocks, Allocator.Persistent);
                blocks = null;
            }

            if (freeListHeads != null)
            {
                UnsafeUtility.Free(freeListHeads, Allocator.Persistent);
                freeListHeads = null;
            }

            if (freeListNext != null)
            {
                UnsafeUtility.Free(freeListNext, Allocator.Persistent);
                freeListNext = null;
            }
            
            IsActive = false;
            
            dbug.log(nameof(MemAllocator) + $" disposed {totalSize}b, {totalSize/1024/1024}mb ");
        }

        // Get total allocated memory size
        public long GetTotalSize()
        {
            return totalSize;
        }

        // Optional: Get memory usage information
        public (long totalSize, long usedSize, long freeSize, int defragmentationCycles, int blockCount) GetMemoryInfo()
        {
            long usedSize = 0;
            var freeSize = totalSize;

            for (var i = 0; i < blockCount; i++)
                if (blocks[i].IsUsed)
                {
                    usedSize += blocks[i].Size;
                    freeSize -= blocks[i].Size;
                }

            return (totalSize, usedSize, freeSize, defragmentationCount, blockCount);
        }

        public MemoryView GetMemoryView()
        {
            return new MemoryView
            {
                Blocks = blocks,
                BlockCount = blockCount,
                memoryUsed = memoryUsed
            };
        }
    }

    public class MemoryView
    {
        public unsafe MemAllocator.MemoryBlock* Blocks;
        public int BlockCount;
        public long memoryUsed;
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
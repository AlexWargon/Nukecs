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
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe partial struct MemAllocator : IDisposable
    {
        private const int MAX_BLOCKS = 1024 * 1024;
        private const int ALIGNMENT = 16;
        public const int BIG_MEMORY_BLOCK_SIZE = 1024 * 1024;
        [StructLayout(LayoutKind.Sequential)]
        public struct MemoryBlock
        {
            public long Pointer;
            public int Size;
            public bool IsUsed;
        }

        private byte* basePtr;
        private long totalSize;
        private MemoryBlock* blocks;
        private int blockCount;
        private long memoryUsed;
        private int defragmentationCount;
        private Spinner spinner;
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
        
        public MemAllocator(long sizeInBytes)
        {
            totalSize = sizeInBytes;
            basePtr = (byte*)UnsafeUtility.Malloc(totalSize, ALIGNMENT, Allocator.Persistent);
            blocks = (MemoryBlock*)UnsafeUtility.Malloc(sizeof(MemoryBlock) * MAX_BLOCKS, ALIGNMENT, Allocator.Persistent);
            // Initialize first block covering entire memory
            UnsafeUtility.MemClear(basePtr, totalSize);
            UnsafeUtility.MemClear(blocks, sizeof(MemoryBlock) * MAX_BLOCKS);
            blocks[0] = new MemoryBlock
            {
                Pointer = 0,
                Size = (int)totalSize,
                IsUsed = false,
            };
            blockCount = 1;
            defragmentationCount = 0;
            memoryUsed = 0;
            spinner = new Spinner();
            IsActive = true;
        }

        public static MemAllocator* New(long sizeInBytes)
        {
            var ptr = (MemAllocator*)UnsafeUtility.MallocTracked(sizeof(MemAllocator), 
                UnsafeUtility.AlignOf<MemAllocator>(),
                Allocator.Persistent, 0);
            *ptr = new MemAllocator(sizeInBytes);
            return ptr;
        }

        public static void Destroy(MemAllocator* allocator)
        {
            allocator->Dispose();
            UnsafeUtility.FreeTracked(allocator, Allocator.Persistent);
        }
        public IntPtr AllocateRaw(long sizeInBytes, ref int error)
        {
            SizeWithAlign(ref sizeInBytes, ALIGNMENT);
            spinner.Acquire();
            DeFragment();
            for (var i = 0; i < blockCount; i++)
            {
                ref var block = ref blocks[i];
                if (!block.IsUsed && block.Size >= sizeInBytes)
                {
                    // Split block if larger than requested size
                    if (block.Size > sizeInBytes)
                        InsertBlock(i + 1, block.Pointer + sizeInBytes, block.Size - sizeInBytes, false, ref error);

                    block.Size = (int)sizeInBytes;
                    block.IsUsed = true;
                    spinner.Release();
                    return (IntPtr)(basePtr + block.Pointer);
                }
            }
            spinner.Release();
            error = AllocatorError.ERROR_ALLOCATOR_OUT_OF_MEMORY;
            
            return IntPtr.Zero;
        }
        public ptr_offset AllocateRaw(long sizeInBytes)
        {
            var error = 0;
            SizeWithAlign(ref sizeInBytes, ALIGNMENT);
            spinner.Acquire();
            DeFragment();
            for (var i = 0; i < blockCount; i++)
            {
                ref var block = ref blocks[i];
                if (!block.IsUsed && block.Size >= sizeInBytes)
                {
                    // Split block if larger than requested size
                    if (block.Size > sizeInBytes)
                        InsertBlock(i + 1, block.Pointer + sizeInBytes, block.Size - sizeInBytes, false, ref error);

                    block.Size = (int)sizeInBytes;
                    block.IsUsed = true;
                    spinner.Release();
                    return new ptr_offset(0, (uint)block.Pointer);
                }
            }
            spinner.Release();

            return ptr_offset.NULL;
        }

        public ptr<T> AllocatePtr<T>() where T : unmanaged
        {
            return AllocatePtr<T>(sizeof(T));
        }

        public void* Allocate(long sizeInBytes)
        {
            var error = 0;
            SizeWithAlign(ref sizeInBytes, ALIGNMENT);
            spinner.Acquire();
            DeFragment();
            for (var i = 0; i < blockCount; i++)
            {
                ref var block = ref blocks[i];
                if (!block.IsUsed && block.Size >= sizeInBytes)
                {
                    // Split block if larger than requested size
                    if (block.Size > sizeInBytes)
                        InsertBlock(i + 1, block.Pointer + sizeInBytes, block.Size - sizeInBytes, false, ref error);

                    block.Size = (int)sizeInBytes;
                    block.IsUsed = true;
                    spinner.Release();
                    return basePtr + block.Pointer;
                }
            }
            spinner.Release();
            return null;
        }
        public ptr<T> AllocatePtr<T>(long sizeInBytes) where T : unmanaged
        {
            var error = 0;
            SizeWithAlign(ref sizeInBytes, ALIGNMENT);
            spinner.Acquire();
            DeFragment();
            for (var i = 0; i < blockCount; i++)
            {
                ref var block = ref blocks[i];
                if (!block.IsUsed && block.Size >= sizeInBytes)
                {
                    // Split block if larger than requested size
                    if (block.Size > sizeInBytes)
                        InsertBlock(i + 1, block.Pointer + sizeInBytes, block.Size - sizeInBytes, false, ref error);

                    block.Size = (int)sizeInBytes;
                    block.IsUsed = true;
                    spinner.Release();
                    return new ptr<T>(basePtr,(uint)block.Pointer);
                }
            }
            spinner.Release();
            return ptr<T>.NULL;
        }
        
        public ptr AllocatePtr(long sizeInBytes)
        {
            var error = 0;
            SizeWithAlign(ref sizeInBytes, ALIGNMENT);
            spinner.Acquire();
            DeFragment();
            for (var i = 0; i < blockCount; i++)
            {
                ref var block = ref blocks[i];
                if (!block.IsUsed && block.Size >= sizeInBytes)
                {
                    // Split block if larger than requested size
                    if (block.Size > sizeInBytes)
                        InsertBlock(i + 1, block.Pointer + sizeInBytes, block.Size - sizeInBytes, false, ref error);

                    block.Size = (int)sizeInBytes;
                    block.IsUsed = true;
                    spinner.Release();
                    return new ptr(basePtr,(uint)block.Pointer);
                }
            }
            spinner.Release();
            return ptr.NULL;
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
                    spinner.Release();
                    return;
                }
            }
            spinner.Release();
            
            error = AllocatorError.ERROR_ALLOCATOR_FAILED_TO_DEALLOCATE;
        }
        internal void DeFragment()
        {
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
            if (blockCount >= MAX_BLOCKS)
            {
                error = AllocatorError.ERROR_ALLOCATOR_MAX_BLOCKS_REACHED;
                return;
            }

            for (var i = blockCount; i > index; i--) 
                blocks[i] = blocks[i - 1];

            blocks[index] = new MemoryBlock
            {
                Pointer = offset,
                Size = (int)size,
                IsUsed = isUsed
            };
            blockCount++;
            memoryUsed += size;
            error = 0;
        }

        private void RemoveBlock(int index)
        {
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
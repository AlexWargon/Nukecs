using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Wargon.Nukecs.Collections;


namespace Wargon.Nukecs
{
    public struct AllocatorError
    {
        public const int NO_ERRORS = 0;
        public const int ERROR_ALLOCATOR_FAILED_TO_DEALLOCATE = -1;
        public const int ERROR_ALLOCATOR_MAX_BLOCKS_REACHED = -2;
        public const int ERROR_ALLOCATOR_OUT_OF_MEMORY = -3;
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe partial struct SerializableMemoryAllocator : IDisposable
    {
        private const int MAX_BLOCKS = 1024 * 16;
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
        private int defragmentationCount;
        private Spinner spinner;
        public SerializableMemoryAllocator(long sizeInBytes)
        {
            totalSize = sizeInBytes;
            basePtr = (byte*)UnsafeUtility.MallocTracked(totalSize, ALIGNMENT, Allocator.Persistent, 0);
            blocks = (MemoryBlock*)UnsafeUtility.MallocTracked(sizeof(MemoryBlock) * MAX_BLOCKS, ALIGNMENT, Allocator.Persistent, 0);
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
        
        public IntPtr AllocateRaw(long size, ref int error)
        {
            SizeWithAlign(ref size, ALIGNMENT);
            spinner.Acquire();
            DeFragment();
            for (var i = 0; i < blockCount; i++)
            {
                ref var block = ref blocks[i];
                if (!block.IsUsed && block.Size >= size)
                {
                    // Split block if larger than requested size
                    if (block.Size > size)
                        InsertBlock(i + 1, block.Pointer + size, block.Size - size, false, ref error);

                    block.Size = (int)size;
                    block.IsUsed = true;
                    spinner.Release();
                    return (IntPtr)(basePtr + block.Pointer);
                }
            }
            spinner.Release();
            error = AllocatorError.ERROR_ALLOCATOR_OUT_OF_MEMORY;
            
            return IntPtr.Zero;
        }
        public ptr_offset AllocateRaw(long size)
        {
            var error = 0;
            SizeWithAlign(ref size, ALIGNMENT);
            spinner.Acquire();
            DeFragment();
            for (var i = 0; i < blockCount; i++)
            {
                ref var block = ref blocks[i];
                if (!block.IsUsed && block.Size >= size)
                {
                    // Split block if larger than requested size
                    if (block.Size > size)
                        InsertBlock(i + 1, block.Pointer + size, block.Size - size, false, ref error);

                    block.Size = (int)size;
                    block.IsUsed = true;
                    spinner.Release();
                    return new ptr_offset( 0, (uint)block.Pointer);
                }
            }
            spinner.Release();

            return ptr_offset.NULL;
        }

        public ptr<T> AllocatePtr<T>() where T : unmanaged
        {
            return AllocatePtr<T>(sizeof(T));
        }
        public ptr<T> AllocatePtr<T>(long size) where T : unmanaged
        {
            var error = 0;
            SizeWithAlign(ref size, ALIGNMENT);
            spinner.Acquire();
            DeFragment();
            for (var i = 0; i < blockCount; i++)
            {
                ref var block = ref blocks[i];
                if (!block.IsUsed && block.Size >= size)
                {
                    // Split block if larger than requested size
                    if (block.Size > size)
                        InsertBlock(i + 1, block.Pointer + size, block.Size - size, false, ref error);

                    block.Size = (int)size;
                    block.IsUsed = true;
                    spinner.Release();
                    return new ptr<T>(basePtr,(uint)block.Pointer);
                }
            }
            spinner.Release();
            return ptr<T>.NULL;
        }
        private void SizeWithAlign(ref long size, int align)
        {
            size = (size + align - 1) / align * align;
        }

        public void Free<T>(ptr<T> ptr) where T : unmanaged
        {
            var p = (byte*)ptr.Ptr;
            Free(p);
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
            }
            blockCount--;
        }

        public void Dispose()
        {
            spinner.Release();
            if (basePtr != null)
            {
                UnsafeUtility.FreeTracked(basePtr, Allocator.Persistent);
                basePtr = null;
            }

            if (blocks != null)
            {
                UnsafeUtility.FreeTracked(blocks, Allocator.Persistent);
                blocks = null;
            }

            IsActive = false;
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
                BlockCount = blockCount
            };
        }
        
        // public void DebugView()
        // {
        //     dbug.log("========== Memory Allocator Debug View ==========");
        //     dbug.log($"Total Memory: {totalSize} bytes");
        //     dbug.log($"Block Count: {blockCount}");
        //     dbug.log($"Defragmentation Cycles: {defragmentationCount}");
        //
        //     long usedSize = 0;
        //     long freeSize = totalSize;
        //
        //     for (int i = 0; i < blockCount; i++)
        //     {
        //         ref var block = ref blocks[i];
        //         string status = block.IsUsed ? "Used" : "Free";
        //         usedSize += block.IsUsed ? block.Size : 0;
        //         freeSize -= block.IsUsed ? block.Size : 0;
        //
        //         dbug.log($"Block {i}:");
        //         dbug.log($"  Offset: {block.Pointer}");
        //         dbug.log($"  Size: {block.Size} bytes");
        //         dbug.log($"  Status: {status}");
        //     }
        //
        //     dbug.log("------------------------------------------------");
        //     dbug.log($"Used Memory: {usedSize} bytes");
        //     dbug.log($"Free Memory: {freeSize} bytes");
        //     dbug.log("================================================");
        // }
    }

    public class MemoryView
    {
        public unsafe SerializableMemoryAllocator.MemoryBlock* Blocks;
        public int BlockCount;
    }

    
    public interface IOnDeserialize
    {
        void OnDeserialize(ref SerializableMemoryAllocator memoryAllocator);
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
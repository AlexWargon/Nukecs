using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


namespace Wargon.Nukecs
{
    public struct ALLOCATOR_ERROR
    {
        public const int NO_ERRORS = 0;
        public const int ERROR_ALLOCATOR_FAILED_TO_DEALLOCATE = -1;
        public const int ERROR_ALLOCATOR_MAX_BLOCKS_REACHED = -2;
        public const int ERROR_ALLOCATOR_OUT_OF_MEMORY= -3;
    }
    public unsafe partial struct SerializableMemoryAllocator : IDisposable
    {
        private const int MAX_BLOCKS = 10024;
        private const int ALIGNMENT = 16;
        private const int BIG_MEMORY_BLOCK_SIZE = 1024 * 1024;
        [StructLayout(LayoutKind.Sequential)]
        internal struct MemoryBlock
        {
            public long Pointer;
            public int Size;
            public bool IsUsed;
        }

        internal byte* basePtr;
        internal long totalSize;
        internal MemoryBlock* blocks;
        internal int blockCount;
        private int defragmentationCount;
        private Spinner spinner;
        public SerializableMemoryAllocator(long totalSize)
        {
            this.totalSize = totalSize;
            basePtr = (byte*)UnsafeUtility.MallocTracked(totalSize, ALIGNMENT, Allocator.Persistent, 0);
            blocks = (MemoryBlock*)UnsafeUtility.MallocTracked(sizeof(MemoryBlock) * MAX_BLOCKS, ALIGNMENT, Allocator.Persistent, 0);
            // Initialize first block covering entire memory
            dbug.log($"bloc size {sizeof(MemoryBlock)}");
            blocks[0] = new MemoryBlock
            {
                Pointer = 0,
                Size = (int)totalSize,
                IsUsed = false,
            };
            blockCount = 1;
            defragmentationCount = 0;
            spinner = new Spinner();
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
                    //Debug.Log($"Allocated {size} bytes ({((float)size/1048576):F} Megabytes) ");
                    spinner.Release();
                    return (IntPtr)(basePtr + block.Pointer);
                }
            }
            spinner.Release();
            error = ALLOCATOR_ERROR.ERROR_ALLOCATOR_OUT_OF_MEMORY;
            return IntPtr.Zero;
        }

        private void SizeWithAlign(ref long size, int align)
        {
            size = (size + align - 1) / align * align;
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
                    //dbug.log($"Allocator Free {block.Size} bytes at offset {block.Pointer}. Pointer {(int)ptr}");
                    return;
                }
            }
            spinner.Release();
            error = ALLOCATOR_ERROR.ERROR_ALLOCATOR_FAILED_TO_DEALLOCATE;
        }

        internal void DeFragment()
        {
            for (var i = 0; i < blockCount - 1; i++)
                if (!blocks[i].IsUsed && !blocks[i + 1].IsUsed)
                {
                    // Merge consecutive free blocks
                    blocks[i].Size += blocks[i + 1].Size;
                    RemoveBlock(i + 1);
                    i--; // Recheck current block
                }

            defragmentationCount++;
        }

        private void InsertBlock(int index, long offset, long size, bool isUsed, ref int error)
        {
            if (blockCount >= MAX_BLOCKS)
            {
                error = ALLOCATOR_ERROR.ERROR_ALLOCATOR_MAX_BLOCKS_REACHED;
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
            error = 0;
        }

        private void RemoveBlock(int index)
        {
            var block = blocks[index];
            dbug.log($"Removing {block.Size} bytes at offset {block.Pointer}. Pointer {(int)basePtr + block.Pointer}");
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
        
        public void DebugView()
        {
            dbug.log("========== Memory Allocator Debug View ==========");
            dbug.log($"Total Memory: {totalSize} bytes");
            dbug.log($"Block Count: {blockCount}");
            dbug.log($"Defragmentation Cycles: {defragmentationCount}");

            long usedSize = 0;
            long freeSize = totalSize;

            for (int i = 0; i < blockCount; i++)
            {
                ref var block = ref blocks[i];
                string status = block.IsUsed ? "Used" : "Free";
                usedSize += block.IsUsed ? block.Size : 0;
                freeSize -= block.IsUsed ? block.Size : 0;

                dbug.log($"Block {i}:");
                dbug.log($"  Offset: {block.Pointer}");
                dbug.log($"  Size: {block.Size} bytes");
                dbug.log($"  Status: {status}");
            }

            dbug.log("------------------------------------------------");
            dbug.log($"Used Memory: {usedSize} bytes");
            dbug.log($"Free Memory: {freeSize} bytes");
            dbug.log("================================================");
        }
    }

    public struct MemoryView
    {
        internal unsafe SerializableMemoryAllocator.MemoryBlock* Blocks;
        internal int BlockCount;
    }
    public unsafe struct UnityAllocatorWrapper : AllocatorManager.IAllocator
    {
        public SerializableMemoryAllocator MemoryAllocator;
        private AllocatorManager.AllocatorHandle m_handle;
        public AllocatorManager.TryFunction Function => AllocatorFunction;

        public AllocatorManager.AllocatorHandle Handle
        {
            get => m_handle;
            set => m_handle = value;
        }

        public Allocator ToAllocator => m_handle.ToAllocator;
        public bool IsCustomAllocator => true;
        public bool IsAutoDispose => false;

        public void Initialize(long capacity)
        {
            MemoryAllocator = new SerializableMemoryAllocator(capacity);
        }

        public void Dispose()
        {
            MemoryAllocator.Dispose();
        }

        public int Try(ref AllocatorManager.Block block)
        {
            var error = ALLOCATOR_ERROR.NO_ERRORS;
            if (block.Range.Pointer == IntPtr.Zero)
            {
                block.Range.Pointer = MemoryAllocator.AllocateRaw(block.Bytes, ref error);
            }
            else
            {
                MemoryAllocator.Free((byte*)block.Range.Pointer, ref error);
            }
            ShowError(error);
            return error;
        }
        [BurstDiscard]
        private static void ShowError(int error)
        {
            if (error != 0)
            {
                switch (error)
                {
                    case ALLOCATOR_ERROR.ERROR_ALLOCATOR_OUT_OF_MEMORY:
                        dbug.error("Allocator out of memory.");
                        break;
                    case ALLOCATOR_ERROR.ERROR_ALLOCATOR_MAX_BLOCKS_REACHED:
                        dbug.error("Allocator max blocks reached.");
                        break;
                }
            }
        }
        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(AllocatorManager.TryFunction))]
        private static int AllocatorFunction(IntPtr customAllocatorPtr, ref AllocatorManager.Block block)
        {
            return ((UnityAllocatorWrapper*)customAllocatorPtr)->Try(ref block);
        }

        public SerializableMemoryAllocator* GetAllocatorPtr()
        {
            fixed (SerializableMemoryAllocator* ptr = &MemoryAllocator)
            {
                return ptr;
            }
        }
    }

    // Example user structure that contains the custom allocator
}
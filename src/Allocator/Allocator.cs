using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


namespace Wargon.Nukecs
{
    public struct AllocatorError
    {
        public const int NO_ERRORS = 0;
        public const int ERROR_ALLOCATOR_FAILED_TO_DEALLOCATE = -1;
        public const int ERROR_ALLOCATOR_MAX_BLOCKS_REACHED = -2;
        public const int ERROR_ALLOCATOR_OUT_OF_MEMORY = -3;
    }
    public unsafe partial struct SerializableMemoryAllocator : IDisposable
    {
        private const int MAX_BLOCKS = 1024 * 16;
        private const int ALIGNMENT = 16;
        private const int BIG_MEMORY_BLOCK_SIZE = 1024 * 1024;
        [StructLayout(LayoutKind.Sequential)]
        internal struct MemoryBlock
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

        internal MemoryBlock* Blocks
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
                    //Debug.Log($"Allocated {size} bytes ({((float)size/1048576):F} Megabytes) ");
                    spinner.Release();
                    return (IntPtr)(basePtr + block.Pointer);
                }
            }
            spinner.Release();
            dbug.error($"Allocator failed allocate {size} bytes. Not free Blocks");
            error = AllocatorError.ERROR_ALLOCATOR_OUT_OF_MEMORY;
            
            return IntPtr.Zero;
        }
        public PtrOffset AllocateRaw(long size)
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
                    //Debug.Log($"Allocated {size} bytes ({((float)size/1048576):F} Megabytes) ");
                    spinner.Release();
                    return new PtrOffset( 0, (uint)block.Pointer);
                }
            }
            spinner.Release();

            return PtrOffset.NULL;
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
            //var block = blocks[index];
            //dbug.log($"Removing {block.Size} bytes at offset {block.Pointer}. Pointer {(int)basePtr + block.Pointer}");
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

    public class MemoryView
    {
        internal unsafe SerializableMemoryAllocator.MemoryBlock* Blocks;
        internal int BlockCount;
    }
    public unsafe struct UnityAllocatorWrapper : AllocatorManager.IAllocator
    {
        public SerializableMemoryAllocator Allocator;
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
            Allocator = new SerializableMemoryAllocator(capacity);
        }

        public void Dispose()
        {
            Allocator.Dispose();
        }

        public int Try(ref AllocatorManager.Block block)
        {
            var error = AllocatorError.NO_ERRORS;
            if (block.Range.Pointer == IntPtr.Zero)
            {
                block.Range.Pointer = Allocator.AllocateRaw(block.Bytes, ref error);
            }
            else
            {
                Allocator.Free((byte*)block.Range.Pointer, ref error);
            }
            ShowError(error);
            return error;
        }
        [BurstDiscard]
        private void ShowError(int error)
        {
            if (error != 0)
            {
                switch (error)
                {
                    case AllocatorError.ERROR_ALLOCATOR_OUT_OF_MEMORY:
                        dbug.error($"Allocator out of memory.");
                        break;
                    case AllocatorError.ERROR_ALLOCATOR_MAX_BLOCKS_REACHED:
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
            fixed (SerializableMemoryAllocator* ptr = &Allocator)
            {
                return ptr;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PtrOffset
    {
        public const int SIZE_OF_BYTES = 8;
        public uint BlockIndex;
        public uint Offset;
        
        public static readonly PtrOffset NULL = new (0u,0u);

        public PtrOffset(uint blockIndex, uint offset)
        {
            BlockIndex = blockIndex;
            Offset = offset;
        }
        
        public unsafe void* AsPtr(ref SerializableMemoryAllocator allocator)
        {
            return allocator.BasePtr + allocator.Blocks[BlockIndex].Pointer + Offset;
        }
        
        public unsafe T* AsPtr<T>(ref SerializableMemoryAllocator allocator) where T : unmanaged
        {
            return (T*)allocator.BasePtr + Offset;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct PtrOffset<T> where T : unmanaged
    {
        private PtrOffset offset;
        private T* pointer;
        public void Update(ref SerializableMemoryAllocator allocator)
        {
            pointer = (T*)(allocator.BasePtr + offset.Offset);
        }

        public T* AsPtr()
        {
            return pointer;
        }
    }
    public unsafe struct SList<T> where T : unmanaged
    {
        internal Unity.Collections.LowLevel.Unsafe.UnsafeList<T> list;
        internal PtrOffset PtrOffset;

        public SList(int capacity, ref UnityAllocatorWrapper allocatorHandler, bool lenAsCapacity = false)
        {
            PtrOffset = allocatorHandler.Allocator.AllocateRaw(sizeof(T) * capacity);
            list = default;
            list.Capacity = capacity;
            list.Allocator = allocatorHandler.Handle;
            list.Ptr = PtrOffset.AsPtr<T>(ref allocatorHandler.Allocator);
            if (lenAsCapacity)
            {
                list.Length = capacity;
            }
        }
        public void OnDeserialize(uint blockIndex, uint offset, ref SerializableMemoryAllocator memoryAllocator)
        {
            PtrOffset = new PtrOffset(blockIndex, offset);
            list.Ptr = PtrOffset.AsPtr<T>(ref memoryAllocator);
        }

        public void OnDeserialize(uint offset, ref SerializableMemoryAllocator memoryAllocator)
        {
            PtrOffset = new PtrOffset(0, offset);
            list.Ptr = PtrOffset.AsPtr<T>(ref memoryAllocator);
        }
    }

    public unsafe struct PtrList<T> where T: unmanaged, IOnDeserialize
    {
        internal SList<PtrOffset> list;
        
        public void Add(T* ptr, ref SerializableMemoryAllocator allocator)
        {
            for (var index = 0; index < list.list.Length; index++)
            {
                ref var ptrOffset = ref list.list.Ptr[index];
            }
        }
        
    }
    
    public interface IOnDeserialize
    {
        void OnDeserialize(ref SerializableMemoryAllocator memoryAllocator);
    }
}
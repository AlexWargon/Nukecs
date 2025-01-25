using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AOT;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;

namespace Wargon.Nukecs
{
    public unsafe struct MemoryAllocator
    {
        private const int MAX_BLOCKS = 1024;
        private const int ALIGNMENT = 16;

        private struct MemoryBlock
        {
            public byte* Pointer;
            public long Size;
            public bool IsUsed;
        }

        private byte* m_BasePtr;
        private long m_TotalSize;
        private MemoryBlock* m_Blocks;
        private int m_BlockCount;

        public MemoryAllocator(long totalSize)
        {
            m_TotalSize = totalSize;
            m_BasePtr = (byte*)UnsafeUtility.Malloc(totalSize, ALIGNMENT, Allocator.Persistent);
            m_Blocks = (MemoryBlock*)UnsafeUtility.Malloc(sizeof(MemoryBlock) * MAX_BLOCKS, ALIGNMENT,
                Allocator.Persistent);

            m_Blocks[0] = new MemoryBlock
            {
                Pointer = m_BasePtr,
                Size = totalSize,
                IsUsed = false
            };
            m_BlockCount = 1;
        }

        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // public IntPtr Allocate<T>(int count = 1) where T : unmanaged
        // {
        //     long size = UnsafeUtility.SizeOf<T>() * count;
        //     return AllocateRaw(size);
        // }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* Allocate<T>(int count = 1) where T : unmanaged
        {
            return (T*)AllocateRaw(UnsafeUtility.SizeOf<T>() * count);
        }

        public IntPtr AllocateRaw(long size)
        {
            Defragment();

            for (var i = 0; i < m_BlockCount; i++)
            {
                ref var block = ref m_Blocks[i];
                if (!block.IsUsed && block.Size >= size)
                {
                    if (block.Size > size)
                        // Split block
                        InsertBlock(i + 1, block.Pointer + size, block.Size - size, false);

                    block.Size = size;
                    block.IsUsed = true;
                    return (IntPtr)block.Pointer;
                }
            }

            throw new OutOfMemoryException("Not enough memory");
        }

        private void InsertBlock(int index, byte* pointer, long size, bool isUsed)
        {
            if (m_BlockCount >= MAX_BLOCKS)
                throw new InvalidOperationException("Max blocks reached");

            for (var i = m_BlockCount; i > index; i--) m_Blocks[i] = m_Blocks[i - 1];

            m_Blocks[index] = new MemoryBlock
            {
                Pointer = pointer,
                Size = size,
                IsUsed = isUsed
            };
            m_BlockCount++;
        }

        public void Free(IntPtr ptr)
        {
            for (var i = 0; i < m_BlockCount; i++)
                if (m_Blocks[i].Pointer == (byte*)ptr)
                {
                    m_Blocks[i].IsUsed = false;
                    break;
                }
        }

        private void Defragment()
        {
            for (var i = 0; i < m_BlockCount - 1; i++)
                if (!m_Blocks[i].IsUsed && !m_Blocks[i + 1].IsUsed)
                {
                    // Merge consecutive free blocks
                    m_Blocks[i].Size += m_Blocks[i + 1].Size;
                    RemoveBlock(i + 1);
                    i--; // Recheck current block
                }
        }

        private void RemoveBlock(int index)
        {
            for (var i = index; i < m_BlockCount - 1; i++) m_Blocks[i] = m_Blocks[i + 1];
            m_BlockCount--;
        }

        public void Dispose()
        {
            if (m_BasePtr != null)
            {
                UnsafeUtility.Free(m_BasePtr, Allocator.Persistent);
                m_BasePtr = null;
            }

            if (m_Blocks != null)
            {
                UnsafeUtility.Free(m_Blocks, Allocator.Persistent);
                m_Blocks = null;
            }
        }
    }
    
    public unsafe struct CompleteCustomAllocator : AllocatorManager.IAllocator
    {
        private MemoryAllocator m_Allocator;
        private IntPtr m_AllocatorState;
        private AllocatorManager.AllocatorHandle handle;
        public CompleteCustomAllocator(long totalMemorySize)
        {
            m_Allocator = new MemoryAllocator(totalMemorySize);
        
            // Создаем состояние аллокатора
            m_AllocatorState = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(m_AllocatorState, IntPtr.Zero);
            handle = Allocator.Persistent;
            m_allocationCount = 0;
            m_initialValue = 0;
        }

        public AllocatorManager.AllocatorHandle Handle {
            get => handle;
            set => handle = value;
        }

        public Allocator ToAllocator => Handle.ToAllocator;
        private byte m_initialValue;
        private int m_allocationCount;
        public int Try(ref AllocatorManager.Block block)
        {
            if (block.Range.Pointer == IntPtr.Zero)
            {
                block.Range.Allocator = handle;
                block.Range.Pointer = m_Allocator.AllocateRaw(block.Bytes);
                
                if (block.Range.Pointer != IntPtr.Zero)
                {
                    UnsafeUtility.MemSet((void*)block.Range.Pointer, m_initialValue, block.Bytes);
                    m_allocationCount++;
                }
            }
            
            else
            {
                
            }
            block.Range.Allocator = handle;
            block.Range.Pointer = m_Allocator.AllocateRaw(block.Bytes);

            var result = handle.Try(ref block);
            return result;
        }
        
        [BurstCompile]
        [MonoPInvokeCallback(typeof(AllocatorManager.TryFunction))]
        internal static int Try(System.IntPtr state, ref AllocatorManager.Block block) {
            unsafe { return ((CompleteCustomAllocator*)state)->Try(ref block); }
        }
        public AllocatorManager.TryFunction Function => Try;

        public byte* Allocate(long size, int alignment, AllocatorManager.AllocatorHandle handle)
        {
            return m_Allocator.Allocate<byte>((int)size);
        }

        public void Free(IntPtr pointer, AllocatorManager.AllocatorHandle handle)
        {
            m_Allocator.Free(pointer);
        }

        public byte* Reallocate(IntPtr pointer, long oldSize, long newSize, int alignment, AllocatorManager.AllocatorHandle handle)
        {
            var newPtr = m_Allocator.Allocate<byte>((int)newSize);
        
            // Копируем старые данные
            UnsafeUtility.MemCpy(
                (void*)newPtr, 
                (void*)pointer, 
                Math.Min(oldSize, newSize)
            );

            return newPtr;
        }

        public void Dispose()
        {
            m_Allocator.Dispose();
        
            // Освобождаем состояние аллокатора
            if (m_AllocatorState != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(m_AllocatorState);
                m_AllocatorState = IntPtr.Zero;
            }
        }

        // Дополнительный метод для проверки, что это кастомный аллокатор
        public bool IsCustomAllocator => true;
    }

    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct ArenaAllocator : AllocatorManager.IAllocator
    {
        private AllocatorManager.AllocatorHandle m_handle;

        public AllocatorManager.TryFunction Function => AllocatorFunction;
        public AllocatorManager.AllocatorHandle Handle { get => m_handle; set => m_handle = value; }
        public Allocator ToAllocator => m_handle.ToAllocator;
        public bool IsCustomAllocator => m_handle.IsCustomAllocator;
        public bool IsAutoDispose => false;

        private byte* m_memoryBase;         // Указатель на базовую память
        private long m_capacityInBytes;            // Общий размер памяти
        private long m_offset;              // Текущая позиция для следующей аллокации
        private long m_alignment;           // Выравнивание
        int m_allocationCount;
        byte m_initialValue;
        private NativeArray<long> m_freeOffsets; // Массив для хранения освобожденных блоков
        private int m_freeCount;             // Количество свободных блоков
        // Value to initialize the allocated memory
        public byte InitialValue => m_initialValue;
        // Allocation count
        public int AllocationCount => m_allocationCount;
        public long CapacityInBytes => m_capacityInBytes;
        public long CapacityInKilobytes => m_capacityInBytes / 1000;
        public long CapacityInMegabytes => m_capacityInBytes / 1000000;
        public void Initialize(long capacity, long alignment = 16, int maxFreeBlocks = 1024)
        {
            m_capacityInBytes = capacity;
            m_offset = 0;
            m_alignment = alignment;

            // Выделяем память
            m_memoryBase = (byte*)UnsafeUtility.Malloc(capacity, (int)alignment, Allocator.Persistent);
            UnsafeUtility.MemSet(m_memoryBase, 0, capacity);

            // Создаем массив для хранения освобожденных блоков
            m_freeOffsets = new NativeArray<long>(maxFreeBlocks, Allocator.Persistent);
            m_freeCount = 0;
            m_allocationCount = 0;
            m_initialValue = 0;
        }

        public void Dispose()
        {
            if (m_memoryBase != null)
            {
                UnsafeUtility.Free(m_memoryBase, Allocator.Persistent);
                m_memoryBase = null;
            }

            if (m_freeOffsets.IsCreated)
            {
                m_freeOffsets.Dispose();
            }

            m_capacityInBytes = 0;
            m_offset = 0;
            m_freeCount = 0;
        }

        public int Try(ref AllocatorManager.Block block)
        {
            if (block.Range.Pointer == IntPtr.Zero)
            {
                return Allocate(ref block);
            }
            else
            {
                return Deallocate(ref block);
            }
        }

        private int Allocate(ref AllocatorManager.Block block)
        {
            // Проверяем наличие свободных блоков
            if (m_freeCount > 0)
            {
                m_freeCount--;
                long freeOffset = m_freeOffsets[m_freeCount];
                block.Range.Pointer = (IntPtr)(m_memoryBase + freeOffset);
                m_allocationCount++;
                return 0;
            }

            // Выравниваем текущий offset
            long alignedOffset = (m_offset + m_alignment - 1) & ~(m_alignment - 1);

            // Проверяем, достаточно ли памяти
            if (alignedOffset + block.Bytes > m_capacityInBytes)
            {
                return -1; // Недостаточно памяти
            }

            // Выделяем память
            block.Range.Pointer = (IntPtr)(m_memoryBase + alignedOffset);
            m_offset = alignedOffset + block.Bytes;
            m_allocationCount++;
            return 0;
        }

        private int Deallocate(ref AllocatorManager.Block block)
        {
            long offset = (byte*)block.Range.Pointer - m_memoryBase;

            // Проверяем, что указатель в пределах арены
            if (offset < 0 || offset >= m_capacityInBytes)
            {
                return -1; // Ошибка: указатель вне диапазона
            }

            // Проверяем, не переполнен ли массив свободных блоков
            if (m_freeCount >= m_freeOffsets.Length)
            {
                return -1; // Ошибка: массив свободных блоков переполнен
            }

            // Добавляем блок в массив
            m_freeOffsets[m_freeCount] = offset;
            m_freeCount++;
            block.Range.Pointer = IntPtr.Zero;
            m_allocationCount--;
            return 0;
        }

        [BurstCompile(CompileSynchronously = true)]
        [MonoPInvokeCallback(typeof(AllocatorManager.TryFunction))]
        public static unsafe int AllocatorFunction(IntPtr customAllocatorPtr, ref AllocatorManager.Block block)
        {
            return ((ArenaAllocator*)customAllocatorPtr)->Try(ref block);
        }

        public IntPtr GetBasePointer()
        {
            return (IntPtr)m_memoryBase;
        }

        public long GetRemainingCapacity()
        {
            return m_capacityInBytes - m_offset + m_freeCount * m_alignment;
        }
    }
    // Example user structure that contains the custom allocator
    public struct ArenaAllocatorHandle
    {
        // Use AllocatorHelper to help creating the example custom alloctor
        AllocatorHelper<ArenaAllocator> customAllocatorHelper;

        // Custom allocator property for accessibility
        public ref ArenaAllocator Allocator => ref customAllocatorHelper.Allocator;

        public AllocatorManager.AllocatorHandle AllocatorHandle => customAllocatorHelper.Allocator.Handle;
        // Create the example custom allocator
        void CreateCustomAllocator(AllocatorManager.AllocatorHandle backgroundAllocator, long initialValue)
        {
            // Allocate the custom allocator from backgroundAllocator and register the allocator
            customAllocatorHelper = new AllocatorHelper<ArenaAllocator>(backgroundAllocator);
            
            // Set the initial value to initialize the memory
            Allocator.Initialize(initialValue);
            
        }

        // Dispose the custom allocator
        void DisposeCustomAllocator()
        {
            // Dispose the custom allocator
            Allocator.Dispose();
            // Unregister the custom allocator and dispose it
            customAllocatorHelper.Dispose();
        }

        // Constructor of user structure
        public ArenaAllocatorHandle(long sizeInBytes)
        {
            this = default;
            CreateCustomAllocator(Unity.Collections.Allocator.Persistent, sizeInBytes);
        }

        // Dispose the user structure
        public void Dispose()
        {
            DisposeCustomAllocator();
        }

        // Sample code to use the custom allocator to allocate containers
        public void UseCustomAllocator(out NativeArray<int> nativeArray, out NativeList<int> nativeList)
        {
            // Use custom allocator to allocate a native array and check initial value.
            nativeArray = CollectionHelper.CreateNativeArray<int, ArenaAllocator>(100, ref Allocator, NativeArrayOptions.UninitializedMemory);
            Assert.AreEqual(Allocator.InitialValue, (byte)nativeArray[0] & 0xFF);
            nativeArray[0] = 0xFE;

            // Use custom allocator to allocate a native list and check initial value.
            nativeList = new NativeList<int>(Allocator.Handle);
            for (int i = 0; i < 50; i++)
            {
                nativeList.Add(i);
            }

            unsafe
            {
                // Use custom allocator to allocate a byte buffer.
                byte* bytePtr = (byte*)AllocatorManager.Allocate(ref Allocator, sizeof(byte), sizeof(byte), 10);
                Assert.AreEqual(Allocator.InitialValue, bytePtr[0]);

                // Free the byte buffer.
                AllocatorManager.Free(Allocator.ToAllocator, bytePtr, 10);
            }
        }

        // Get allocation count from the custom allocator
        public int AllocationCount => Allocator.AllocationCount;

    }
}
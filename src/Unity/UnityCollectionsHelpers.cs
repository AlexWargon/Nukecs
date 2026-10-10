using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

// Helpers for Unity.Collections containers; the core uses its own HeapList/HeapHashMap.
namespace Wargon.Nukecs {
    public static class UnsafeListExtensions {
        public static unsafe void Copy<T>(ref Unity.Collections.LowLevel.Unsafe.UnsafeList<T> dst, ref T[] source, int len) where T : unmanaged
        {
            fixed (T* ptr = source)
            {
                Mem.MemCpy(dst.Ptr, ptr, Mem.SizeOf<T>() * source.Length);
            }
            dst.m_length = len;
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T ElementAtNoCheck<T>(this Unity.Collections.LowLevel.Unsafe.UnsafeList<T> list, int index) where T : unmanaged {
            return ref list.Ptr[index];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T* ElementAtNoCheck<T>(this UnsafePtrList<T> list, int index) where T: unmanaged{
            return ref list.Ptr[index];
        }
    }
    public static class UnsafeHelp {
        public static Unity.Collections.LowLevel.Unsafe.UnsafeList<T> UnsafeListWithMaximumLenght<T>(int size, AllocatorHandle allocator,
            NativeArrayOptions options) where T : unmanaged {
            var list = new UnsafeList<T>(size, allocator, options);
            list.Length = size;
            return list;
        }

        public static unsafe Unity.Collections.LowLevel.Unsafe.UnsafeList<T>* UnsafeListPtrWithMaximumLenght<T>(int size, AllocatorHandle allocator,
            NativeArrayOptions options) where T : unmanaged {
            var ptr = Unity.Collections.LowLevel.Unsafe.UnsafeList<T>.Create(size, allocator, options);
            ptr->m_length = size;
            return ptr;
        }

        public static ref Unity.Collections.LowLevel.Unsafe.UnsafeList<T> ResizeUnsafeList<T>(ref Unity.Collections.LowLevel.Unsafe.UnsafeList<T> list, int size,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory) where T : unmanaged 
        {
            list.Resize(size, options);
            list.Length = size;
            return ref list;
        }

        public static unsafe void ResizeUnsafeList<T>(ref Unity.Collections.LowLevel.Unsafe.UnsafeList<T>* list, int size,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory) where T : unmanaged 
        {
            list->Resize(size, options);
            list->m_length = size;
        }

        public static int AlignOf(ComponentTypeData typeData) {
            return typeData.size + sizeof(byte) * 2 - typeData.size;
        }
        public static int AlignOf(Type type) {
            return Mem.SizeOf(type) + sizeof(byte) * 2 - Mem.SizeOf(type);
        }

        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Resize<T>(int oldCapacity, int newCapacity, ref T* buffer, AllocatorHandle allocator) where T : unmanaged
        {
            var typeSize = sizeof(T);
            var newBuffer = (T*)Mem.MallocTracked(
                newCapacity * typeSize,
                Mem.AlignOf<T>(),
                allocator, 0
            );

            if (newBuffer == null)  
            {
                throw new OutOfMemoryException("Failed to allocate memory for resizing.");
            }

            Mem.MemClear(newBuffer, newCapacity * typeSize);
            Mem.MemCpy(newBuffer, buffer, oldCapacity * typeSize);

            Mem.FreeTracked(buffer, allocator);

            buffer = newBuffer;
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void CheckResize<T>(int index, ref int capacity, ref T* buffer, AllocatorHandle allocator) where T : unmanaged
        {
            if (index >= capacity)
            {
                var newCapacity = math.max(capacity * 2, index + 1);
                var typeSize = sizeof(T);
                var newBuffer = (T*)Mem.MallocTracked(
                    newCapacity * sizeof(T),
                    Mem.AlignOf<T>(),
                    allocator, 0
                );

                if (newBuffer == null)
                {
                    throw new OutOfMemoryException("Failed to allocate memory for resizing.");
                }

                Mem.MemClear(newBuffer, newCapacity * typeSize);
                Mem.MemCpy(newBuffer, buffer, capacity * typeSize);

                Mem.FreeTracked(buffer, allocator);

                buffer = newBuffer;
                capacity = newCapacity;
            }
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void CheckResize<T>(int index, ref int capacity, ref void* buffer, AllocatorHandle allocator, int typeSize, int align) where T : unmanaged
        {
            if (index >= capacity)
            {
                int newCapacity = math.max(capacity * 2, index + 1);
                void* newBuffer = Mem.MallocTracked(
                    newCapacity * sizeof(T),
                    align,
                    allocator, 0
                );

                if (newBuffer == null)
                {
                    throw new OutOfMemoryException("Failed to allocate memory for resizing.");
                }
                Mem.MemClear(newBuffer, newCapacity * typeSize);
                Mem.MemCpy(newBuffer, buffer, capacity * typeSize);

                Mem.FreeTracked(buffer, allocator);

                buffer = newBuffer;
                capacity = newCapacity;
            }
        }
    }

    public static class DictionaryExtensions
    {
        public static NativeHashMap<TKey, TValue> ToNative<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, AllocatorHandle allocator)
            where TKey : unmanaged, IEquatable<TKey> where TValue : unmanaged
        {
            var map = new NativeHashMap<TKey, TValue>(dictionary.Count, allocator);
            foreach (var (key, value) in dictionary)
            {
                map.Add(key, value);
            }
            return map;
        }
    }
}

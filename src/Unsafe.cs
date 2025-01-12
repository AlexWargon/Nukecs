using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Wargon.Nukecs {
    public static unsafe class Unsafe {
        public static T* MallocTracked<T>(Allocator allocator) where T : unmanaged {
            return (T*) UnsafeUtility.MallocTracked(sizeof(T), UnsafeUtility.AlignOf<T>(), allocator, 0);
        }
        public static T* MallocTracked<T>(int items, Allocator allocator) where T : unmanaged {
            return (T*) UnsafeUtility.MallocTracked(sizeof(T) * items, UnsafeUtility.AlignOf<T>(), allocator, 0);
        }
        public static void FreeTracked<T>(T* ptr, Allocator allocator) where T : unmanaged {
            UnsafeUtility.FreeTracked(ptr, allocator);
        }
        public static T* Malloc<T>(Allocator allocator) where T : unmanaged {
            return (T*) UnsafeUtility.Malloc(sizeof(T), UnsafeUtility.AlignOf<T>(), allocator);
        }

        public static T* AllocateWithManager<T>(AllocatorManager.AllocatorHandle allocator) where T : unmanaged {
            return AllocatorManager.Allocate<T>(allocator);
        }
        public static T* Allocate<T>(int items, AllocatorManager.AllocatorHandle allocator) where T : unmanaged {
            return AllocatorManager.Allocate<T>(allocator, items);
        }
        public static void FreeWithManager(void* ptr, AllocatorManager.AllocatorHandle allocator) {
            AllocatorManager.Free(allocator, ptr);
        }
        public static void FreeWithManager<T>(T* ptr, int items, AllocatorManager.AllocatorHandle allocator) where T : unmanaged {
            AllocatorManager.Free(allocator, ptr, items);
        }
        public static void FreeWithManager<T>(T* ptr, AllocatorManager.AllocatorHandle allocator) where T : unmanaged {
            AllocatorManager.Free(allocator, ptr);
        }
    }
    public static class UnsafeListExtensions {
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T ElementAtNoCheck<T>(this UnsafeList<T> list, int index) where T : unmanaged {
            return ref list.Ptr[index];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T* ElementAtNoCheck<T>(this UnsafePtrList<T> list, int index) where T: unmanaged{
            return ref list.Ptr[index];
        }
    }
    public static class UnsafeHelp {
        public static UnsafeList<T> UnsafeListWithMaximumLenght<T>(int size, Allocator allocator,
            NativeArrayOptions options) where T : unmanaged {
            return new UnsafeList<T>(size, allocator, options) {
                m_length = size
            };
        }

        public static unsafe UnsafeList<T>* UnsafeListPtrWithMaximumLenght<T>(int size, Allocator allocator,
            NativeArrayOptions options) where T : unmanaged {
            var ptr = UnsafeList<T>.Create(size, allocator, options);
            ptr->m_length = size;
            return ptr;
        }

        public static ref UnsafeList<T> ResizeUnsafeList<T>(ref UnsafeList<T> list, int size,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory) where T : unmanaged 
        {
            list.Resize(size, options);
            list.m_length = size;
            return ref list;
        }

        public static unsafe void ResizeUnsafeList<T>(ref UnsafeList<T>* list, int size,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory) where T : unmanaged 
        {
            list->Resize(size, options);
            list->m_length = size;
        }

        public static int AlignOf(ComponentType type) {
            return type.size + sizeof(byte) * 2 - type.size;
        }
        public static int AlignOf(Type type) {
            return UnsafeUtility.SizeOf(type) + sizeof(byte) * 2 - UnsafeUtility.SizeOf(type);
        }

        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Resize<T>(int oldCapacity, int newCapacity, ref T* buffer, Allocator allocator) where T : unmanaged
        {
            // Calculate new capacity

            var typeSize = sizeof(T);
            // Allocate new buffer
            var newBuffer = (T*)UnsafeUtility.Malloc(
                newCapacity * typeSize,
                UnsafeUtility.AlignOf<T>(),
                allocator
            );

            if (newBuffer == null)  
            {
                throw new OutOfMemoryException("Failed to allocate memory for resizing.");
            }

            //UnsafeUtility.MemClear(newBuffer, newCapacity * impl->elementSize);
            // Copy old data to new buffer
            UnsafeUtility.MemCpy(newBuffer, buffer, oldCapacity * typeSize);

            // Free old buffer
            UnsafeUtility.Free(buffer, allocator);

            // Update impl
            buffer = newBuffer;
            //Debug.Log($"Resized ptr from {oldCapacity} to {newCapacity}");
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void CheckResize<T>(int index, ref int capacity, ref T* buffer, Allocator allocator) where T : unmanaged
        {
            if (index >= capacity)
            {
                // Calculate new capacity
                var newCapacity = math.max(capacity * 2, index + 1);
                var typeSize = sizeof(T);
                // Allocate new buffer
                var newBuffer = (T*)UnsafeUtility.Malloc(
                    newCapacity * sizeof(T),
                    UnsafeUtility.AlignOf<T>(),
                    allocator
                );

                if (newBuffer == null)
                {
                    throw new OutOfMemoryException("Failed to allocate memory for resizing.");
                }

                //UnsafeUtility.MemClear(newBuffer, newCapacity * impl->elementSize);
                // Copy old data to new buffer
                UnsafeUtility.MemCpy(newBuffer, buffer, capacity * typeSize);

                // Free old buffer
                UnsafeUtility.Free(buffer, allocator);

                // Update impl
                buffer = newBuffer;
                capacity = newCapacity;
            }
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void CheckResize<T>(int index, ref int capacity, ref void* buffer, Allocator allocator, int typeSize, int align) where T : unmanaged
        {
            if (index >= capacity)
            {
                // Calculate new capacity
                int newCapacity = math.max(capacity * 2, index + 1);
                // Allocate new buffer
                void* newBuffer = UnsafeUtility.Malloc(
                    newCapacity * sizeof(T),
                    align,
                    allocator
                );

                if (newBuffer == null)
                {
                    throw new OutOfMemoryException("Failed to allocate memory for resizing.");
                }
                //UnsafeUtility.MemClear(newBuffer, newCapacity * impl->elementSize);
                // Copy old data to new buffer
                UnsafeUtility.MemCpy(newBuffer, buffer, capacity * typeSize);

                // Free old buffer
                UnsafeUtility.Free(buffer, allocator);

                // Update impl
                buffer = newBuffer;
                capacity = newCapacity;
            }
        }
    }

    public static class DictionaryExtensions
    {
        public static NativeHashMap<TKey, TValue> ToNative<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, Allocator allocator)
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
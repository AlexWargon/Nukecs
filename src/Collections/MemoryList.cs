using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Wargon.Nukecs.Collections
{
    using System;
    using System.Runtime.CompilerServices;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct MemoryList<T> where T : unmanaged 
    {
        public bool IsCreated => Ptr != null;
        internal ptr_offset PtrOffset;
        internal int capacity;
        internal int length;
        [NativeDisableUnsafePtrRestriction]
        internal T* Ptr;
        public MemoryList(int capacity, ref SerializableMemoryAllocator allocator, bool lenAsCapacity = false)
        {
            PtrOffset = allocator.AllocateRaw(sizeof(T) * capacity);
            Ptr = PtrOffset.AsPtr<T>(ref allocator);
            this.capacity = capacity;
            length = 0;
            if (lenAsCapacity)
            {
                length = capacity;
            }
        }
        
        public static ptr<MemoryList<T>> Create(int capacity, ref UnityAllocatorWrapper allocatorHandler,
            bool lenAsCapacity = false)
        {
            var list = new MemoryList<T>
            {
                PtrOffset = allocatorHandler.Allocator.AllocateRaw(sizeof(T) * capacity),
                capacity = capacity
            };
            list.Ptr = list.PtrOffset.AsPtr<T>(ref allocatorHandler.Allocator);
            if (lenAsCapacity)
            {
                list.length = capacity;
            }
            var ptr = allocatorHandler.Allocator.AllocatePtr<MemoryList<T>>(sizeof(MemoryList<T>));
            *ptr.Ptr = list;
            return ptr;
        }
        
        internal int Capacity => capacity;

        internal int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => length;
        }

        public static void Destroy(ptr<MemoryList<T>> list, ref UnityAllocatorWrapper allocatorHandler)
        {
            ref var l = ref list.Ref;
            l.Dispose();
            allocatorHandler.Allocator.Free(list);
        }

        /// <summary>
        /// No bound checks
        /// </summary>
        /// <param name="index"></param>
        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Ptr[index];
        }
        
        public void Add(in T value, ref SerializableMemoryAllocator allocatorHandler)
        {
            var idx = length;
            if (length < capacity)
            {
                Ptr[idx] = value;
                length++;
                return;
            }
            
            Resize(idx + 1, ref allocatorHandler);
            Ptr[idx] = value;
        }
        
        public void Clear()
        {
            length = 0;
        }
        /// <summary>
        /// No bound checks
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public ref T ElementAt(int index)
        {
            return ref Ptr[index];
        }
        
        public void Dispose()
        {
            
        }

        public void OnDeserialize(uint blockIndex, uint offset, ref SerializableMemoryAllocator memoryAllocator)
        {
            PtrOffset = new ptr_offset(blockIndex, offset);
            Ptr = PtrOffset.AsPtr<T>(ref memoryAllocator);
        }

        public void OnDeserialize(ref SerializableMemoryAllocator memoryAllocator)
        {
            Ptr = PtrOffset.AsPtr<T>(ref memoryAllocator);
        }

        public void CopyFrom(ref MemoryList<T> other, ref SerializableMemoryAllocator allocatorHandler)
        {
            Resize(other.Length, ref allocatorHandler);
            UnsafeUtility.MemCpy(Ptr, other.Ptr, UnsafeUtility.SizeOf<T>() * other.Length);
        }
        
        public void Resize(int len, ref SerializableMemoryAllocator allocatorHandler)
        {
            if (len > Capacity)
            {
                SetCapacity(len, ref allocatorHandler);
            }
            length = len;
        }
        public Enumerator GetEnumerator()
        {
            return new Enumerator { Length = length, Index = -1, Ptr = Ptr };
        }

        private void SetCapacity(int size, ref SerializableMemoryAllocator allocator)
        {
            Utils.CheckCapacityInRange(capacity, length);

            var sizeOf = sizeof(T);
            var newCapacity = math.max(size, CollectionHelper.CacheLineSize / sizeOf);
            newCapacity = math.ceilpow2(newCapacity);

            if (newCapacity == capacity)
            {
                return;
            }

            ResizeExact(ref allocator, newCapacity);
        }
        
        private void ResizeExact(ref SerializableMemoryAllocator allocator, int newCapacity)
        {
            newCapacity = math.max(0, newCapacity);

            T* newPointer = null;

            var sizeOf = sizeof(T);

            if (newCapacity > 0)
            {
                var ptr = allocator.AllocateRaw(sizeOf * newCapacity);
                newPointer = ptr.AsPtr<T>(ref allocator);
                if (Ptr != null && capacity > 0)
                {
                    var itemsToCopy = math.min(newCapacity, capacity);
                    var bytesToCopy = itemsToCopy * sizeOf;
                    UnsafeUtility.MemCpy(newPointer, Ptr, bytesToCopy);
                }
            }
            allocator.Free(Ptr);
            Ptr = newPointer;
            capacity = newCapacity;
            length = math.min(length, newCapacity);
        }
        
        public void RemoveAt(int index)
        {
            var dst = Ptr + index;
            var src = dst + 1;
            length--;

            for (var i = index; i < length; i++)
            {
                *dst++ = *src++;
            }
        }

        public Span<T> AsSpan()
        {
            return new Span<T>(Ptr, length);
        }
        public struct Enumerator
        {
            internal T* Ptr;
            internal int Length;
            internal int Index;

            public void Dispose() { }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => ++Index < Length;

            public void Reset() => Index = -1;

            public ref T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref Ptr[Index];
            }
        }

    }

    public static unsafe class Extensions
    {
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
        public static int IndexOf<T, U>(this ref MemoryList<T> list, U value) where T : unmanaged, IEquatable<U>
        {
            return NativeArrayExtensions.IndexOf<T, U>(list.Ptr, list.Length, value);
        }
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new [] { typeof(int), typeof(int) })]
        public static bool Contains<T, U>(this ref MemoryList<T> list, U value) where T : unmanaged, IEquatable<U>
        {
            return list.IndexOf(value) != -1;
        }
    }

    public static class Utils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void CheckCapacityInRange(int capacity, int length)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException($"NUKECS: Capacity {capacity} must be positive.");

            if (capacity < length)
                throw new ArgumentOutOfRangeException($"NUKECS: Capacity {capacity} is out of range in container of '{length}' Length.");
        }
    }
}
namespace Wargon.Nukecs.Collections
{
    using System;
    using System.Runtime.CompilerServices;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;
    
    public unsafe struct MemoryList<T> where T : unmanaged
    {
        internal ptr_offset PtrOffset;
        internal int capacity;
        internal int length;
        [NativeDisableUnsafePtrRestriction]
        internal T* Ptr;
        public MemoryList(int capacity, ref UnityAllocatorWrapper allocatorHandler, bool lenAsCapacity = false)
        {
            PtrOffset = allocatorHandler.Allocator.AllocateRaw(sizeof(T) * capacity);
            Ptr = PtrOffset.AsPtr<T>(ref allocatorHandler.Allocator);
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


        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Ptr[index];
        }
        
        public void Add(in T value, ref UnityAllocatorWrapper allocatorHandler)
        {
            // var idx = list.m_length;
            // if (list.m_length < list.m_capacity)
            // {
            //     list.Ptr[idx] = item;
            //     list.m_length++;
            //     return;
            // }
            // Resize(idx + 1, ref allocatorHandler);
            // list.Ptr[idx] = item; 
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

        public void CopyFrom(ref MemoryList<T> other, ref UnityAllocatorWrapper allocatorHandler)
        {
            Resize(other.Length, ref allocatorHandler);
            UnsafeUtility.MemCpy(Ptr, other.Ptr, UnsafeUtility.SizeOf<T>() * other.Length);
        }
        
        public void Resize(int len, ref UnityAllocatorWrapper allocatorHandler)
        {
            if (len > Capacity)
            {
                SetCapacity(len, ref allocatorHandler);
            }
            this.length = len;
        }
        public Enumerator GetEnumerator()
        {
            return new Enumerator { Length = length, Index = -1, Ptr = Ptr };
        }

        private void SetCapacity(int size, ref UnityAllocatorWrapper allocator)
        {
            //CollectionHelper.CheckCapacityInRange(capacity, Length);

            var sizeOf = sizeof(T);
            var newCapacity = math.max(size, CollectionHelper.CacheLineSize / sizeOf);
            newCapacity = math.ceilpow2(newCapacity);

            if (newCapacity == Capacity)
            {
                return;
            }

            ResizeExact(ref allocator, newCapacity);
        }
        
        private void ResizeExact(ref UnityAllocatorWrapper allocatorWrapper, int newCapacity)
        {
            newCapacity = math.max(0, newCapacity);

            T* newPointer = null;

            var sizeOf = sizeof(T);

            if (newCapacity > 0)
            {
                var ptr = allocatorWrapper.Allocator.AllocateRaw(sizeOf * newCapacity);
                newPointer = ptr.AsPtr<T>(ref allocatorWrapper.Allocator);
                if (Ptr != null && capacity > 0)
                {
                    var itemsToCopy = math.min(newCapacity, capacity);
                    var bytesToCopy = itemsToCopy * sizeOf;
                    UnsafeUtility.MemCpy(newPointer, Ptr, bytesToCopy);
                    //dbug.log($"bytesToCopy: {bytesToCopy}");
                }
            }
            allocatorWrapper.Allocator.Free(Ptr);
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
}
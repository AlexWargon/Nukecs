using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Wargon.Nukecs
{
    public unsafe struct UnsafeList<T> where T : unmanaged
    {
        internal Unity.Collections.LowLevel.Unsafe.UnsafeList<T> list;
        
        internal PtrOffset PtrOffset;

        internal T* Ptr
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => list.Ptr;
        }
        internal int Capacity => list.m_capacity;
        internal int Length => list.m_length;

        public static Ptr<UnsafeList<T>> Create(int capacity, ref UnityAllocatorWrapper allocatorHandler,
            bool lenAsCapacity = false)
        {
            var list = new UnsafeList<T>
            {
                PtrOffset = allocatorHandler.Allocator.AllocateRaw(sizeof(T) * capacity)
            };
            list.list.Allocator = allocatorHandler.Handle;
            list.list.Capacity = capacity;
            list.list.Ptr = list.PtrOffset.AsPtr<T>(ref allocatorHandler.Allocator);
            if (lenAsCapacity)
            {
                list.list.Length = capacity;
            }
            var ptr = allocatorHandler.Allocator.AllocatePtr<UnsafeList<T>>(sizeof(UnsafeList<T>));
            *ptr.Value = list;
            return ptr;
        }

        public static void Destroy(Ptr<UnsafeList<T>> list, ref UnityAllocatorWrapper allocatorHandler)
        {
            var l = list.AsRef();
            l.Dispose();
            allocatorHandler.Allocator.Free(list);
        }
        public UnsafeList(int capacity, ref UnityAllocatorWrapper allocatorHandler, bool lenAsCapacity = false)
        {
            PtrOffset = allocatorHandler.Allocator.AllocateRaw(sizeof(T) * capacity);
            list = default;
            list.Allocator = allocatorHandler.Handle;
            list.Capacity = capacity;
            list.Ptr = PtrOffset.AsPtr<T>(ref allocatorHandler.Allocator);
            if (lenAsCapacity)
            {
                list.Length = capacity;
            }
        }

        public ref T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref list.Ptr[index];
        }
        public void Add(T item, ref UnityAllocatorWrapper allocatorHandler)
        {
            var idx = list.m_length;
            if (list.m_length < list.m_capacity)
            {
                list.Ptr[idx] = item;
                list.m_length++;
                return;
            }
            Resize(idx + 1, ref allocatorHandler);
            Ptr[idx] = item;            
        }
        
        public void Clear()
        {
            list.m_length = 0;
        }
        public ref T ElementAt(int index)
        {
            return ref list.Ptr[index];
        }
        public void Dispose()
        {
            list.Dispose();
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

        public void CopyFrom(ref UnsafeList<T> other, ref UnityAllocatorWrapper allocatorHandler)
        {
            Resize(other.Length, ref allocatorHandler);
            UnsafeUtility.MemCpy(Ptr, other.Ptr, UnsafeUtility.SizeOf<T>() * other.Length);
            dbug.log("copy");
        }
        
        public void Resize(int newSize, ref UnityAllocatorWrapper allocatorHandler)
        {
            ref var allocator = ref allocatorHandler.Allocator;
            var sizeOf = sizeof(T);
            var newCapacity = math.max(newSize, CollectionHelper.CacheLineSize / sizeOf);
            newCapacity = math.ceilpow2(newCapacity);

            if (newCapacity <= list.Capacity)
            {
                return;
            }
            
            newCapacity = math.max(0, newCapacity);
            
            T* newPointer = null;

            //var alignOf = UnsafeUtility.AlignOf<T>();

            if (newCapacity > 0)
            {
                var ptr = allocator.AllocateRaw(sizeOf * newCapacity);
                ;
                newPointer = ptr.AsPtr<T>(ref allocator);

                if (list.Ptr != null && list.m_capacity > 0)
                {
                    var itemsToCopy = math.min(newCapacity, list.Capacity);
                    var bytesToCopy = itemsToCopy * sizeOf;
                    UnsafeUtility.MemCpy(newPointer, list.Ptr, bytesToCopy);
                }
            }

            allocator.Free(list.Ptr);

            list.Ptr = newPointer;
            list.m_capacity = newCapacity;
            list.m_length = math.min(list.m_length, newCapacity);
        }
        public Unity.Collections.LowLevel.Unsafe.UnsafeList<T>.Enumerator GetEnumerator()
        {
            return list.GetEnumerator();
        }
    }
}
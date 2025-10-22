namespace Wargon.Nukecs
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using static UnsafeStatic;
    internal struct ComponentArray
    {
        internal const int DEFAULT_MAX_CAPACITY = 16;
    }

    internal unsafe struct ComponentArrayData
    {
        internal ptr Data;
        internal ptr<World.WorldUnsafe> world;
        internal ref MemAllocator Allocator => ref world.Ref.AllocatorRef;
        internal int length;
        internal int capacity;

        internal ref T ElementAt<T>(int index) where T : unmanaged
        {
            return ref Data.As<T>()[index];
        }
    }
    public unsafe struct ComponentArray<T> : IComponent, IDisposable, ICopyable<ComponentArray<T>> 
        where T : unmanaged, IArrayComponent
    {
        internal const int DEFAULT_MAX_CAPACITY = ComponentArray.DEFAULT_MAX_CAPACITY;
        public int Length => data.Ref.length;
        internal ptr<ComponentArrayData> data;
        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // private ComponentArray(int capacity)
        // {
        //     buffer = (T*)UnsafeUtility.MallocTracked(capacity* sizeof(T), UnsafeUtility.AlignOf<T>(), Allocator.Persistent, 0);
        //     this.capacity = capacity;
        //     length = 0;
        //     entity = default;
        // }
        
        internal ComponentArray(ref GenericPool pool, Entity index)
        {
            data = index.worldPointer->AllocatorRef.AllocatePtr<ComponentArrayData>();
            data.Ref.Data = index.worldPointer->AllocatorRef.AllocatePtr(sizeof(T) * DEFAULT_MAX_CAPACITY);
            data.Ref.length = 0;
            data.Ref.capacity = DEFAULT_MAX_CAPACITY;
            data.Ref.world = index.worldPointer->selfPtr;
            mem_clear(data.Ref.Data.cached, DEFAULT_MAX_CAPACITY * sizeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ComponentArray(ref ComponentArray<T> other, int index)
        {
            ref var w = ref other.data.Ref.world.Ref;
            data = w.AllocatorRef.AllocatePtr<ComponentArrayData>();
            data.Ref.Data = w.AllocatorRef.AllocatePtr(sizeof(T) * DEFAULT_MAX_CAPACITY);
            data.Ref.length = other.data.Ref.length;
            data.Ref.capacity = other.data.Ref.capacity;
            data.Ref.world = other.data.Ref.world;
            mem_clear(data.Ref.Data.cached, DEFAULT_MAX_CAPACITY * sizeof(T));
            memcpy(data.Ref.Data.cached, other.data.Ref.Data.cached, other.data.Ref.length * sizeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T ElementAt(int index)
        {
            if (index < 0 || index >= data.Ref.length)
                throw new IndexOutOfRangeException();
            return ref data.Ref.ElementAt<T>(index);
        }
        public T ReadAt(int index)
        {
            if (index < 0 || index >= data.Ref.length)
                throw new IndexOutOfRangeException($"Index {index} is out of range");
            return data.IsNull ? default(T) : data.Ref.ElementAt<T>(index);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(in T item)
        {
            if (data.Ref.length >= data.Ref.capacity - 1) return;
            if (data.Ref.length == data.Ref.capacity) Resize(data.Ref.capacity == 0 ? 4 : data.Ref.capacity * 2);
            data.Ref.ElementAt<T>(data.Ref.length++) = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNoResize(in T item)
        {
            if (data.Ref.length < data.Ref.capacity) data.Ref.ElementAt<T>(data.Ref.length++) = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddParallel(in T item)
        {
            var idx = data.Ref.length;
            if (idx < data.Ref.capacity)
            {
                data.Ref.ElementAt<T>(idx) = item;
                Interlocked.Increment(ref data.Ref.length);
            }
            // Note: parallel expansion requires additional synchronization
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveRange(int index, int count)
        {
            if (data.Ref.length <= index + count - 1) return;

            int elemSize = UnsafeUtility.SizeOf<T>();

            mem_move(data.Ref.Data.cached + index * elemSize, data.Ref.Data.cached + (index + count) * elemSize, (long)elemSize * (Length - count - index));
            data.Ref.length -= count;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= data.Ref.length)
                throw new IndexOutOfRangeException();
            RemoveRange(index, 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            data.Ref.length = 0;
        }

        public void Dispose()
        {
            var w = data.Ref.world.Ptr;
            data.Ref.length = 0;
            data.Ref.capacity = 0;
            w->AllocatorRef.Free(data.Ref.Data);
            w->AllocatorRef.Free(data);
            data.Ref.Data = default;
            data = default;
        }

        public ComponentArray<T> Copy(int to)
        {
            return new ComponentArray<T>(ref this, to);
        }

        public void Fill(T* buffer, int length)
        {
            memcpy(data.Ref.Data.As<T>(), buffer, length * sizeof(T));
            data.Ref.length = length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator()
        {
            return new Enumerator(data.Ref.Data.As<T>(), data.Ref.length);
        }

        private void Resize(int newCapacity)
        {
            var w = data.Ref.world.Ptr;
            var newBuffer = w->AllocatorRef.AllocatePtr(newCapacity * sizeof(T));
            if (!data.Ref.Data.IsNull)
            {
                memcpy(newBuffer.As<T>(), data.Ref.Data.As<T>(), data.Ref.length * sizeof(T));
                w->AllocatorRef.Free(data.Ref.Data);
            }

            data.Ref.Data = newBuffer;
            data.Ref.capacity = newCapacity;
            dbug.log("resized");
        }

        public ref struct Enumerator
        {
            private readonly T* listPtr;
            private readonly int len;
            private int index;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Enumerator(T* list, int length)
            {
                listPtr = list;
                len = length;
                index = -1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                index++;
                return index < len;
            }

            public void Reset()
            {
                index = -1;
            }

            public ref T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref listPtr[index];
            }
        }
    }

    [BurstCompile]
    public static class ComponentsArrayExtensions
    {
        [BurstCompile]
        public static unsafe int RemoveAtSwapBack<T>(this ref ComponentArray<T> buffer, in T item)
            where T : unmanaged, IArrayComponent, IEquatable<T>
        {
            for (var i = 0; i < buffer.Length; i++)
                if (item.Equals(buffer.ElementAt(i)))
                {
                    if (i != buffer.Length - 1) buffer.data.Ref.ElementAt<T>(i) = buffer.data.Ref.ElementAt<T>(buffer.data.Ref.length - 1);
                    buffer.data.Ref.length--;
                    break;
                }

            return buffer.Length - 1;
        }
        [BurstCompile]
        public static void RemoveAtSwapBack<T>(this ref ComponentArray<T> buffer, int index)
            where T : unmanaged, IArrayComponent, IEquatable<T>
        {
            if (index != buffer.Length - 1) buffer.data.Ref.ElementAt<T>(index) = buffer.data.Ref.ElementAt<T>(buffer.data.Ref.length - 1);
            buffer.data.Ref.length--;
        }
    }
}
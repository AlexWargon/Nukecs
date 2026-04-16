namespace Wargon.Nukecs
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using Unity.Burst;
    using Unity.Collections.LowLevel.Unsafe;
    using static UnsafeStatic;
    internal struct DummyElement : IArrayComponent {}
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

        internal void Restore(ref MemAllocator allocator)
        {
            Data.OnDeserialize(ref allocator);
            world.OnDeserialize(ref allocator);
        }
        internal static void Restore(byte* ptr, ref MemAllocator allocator)
        {
            var casted = (ComponentArrayData*)ptr;
            if(casted->capacity != 0)
                casted->Restore(ref allocator);
        }
    }
    public unsafe struct ComponentArray<T> : IComponent, IDisposable, ICopyable<ComponentArray<T>>
        where T : unmanaged, IArrayComponent
    {
        internal const int DEFAULT_MAX_CAPACITY = ComponentArray.DEFAULT_MAX_CAPACITY;
        public int Length => data.length;
        internal ComponentArrayData data;

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
            data = default;
            data.Data = index.worldPointer->AllocatorRef.AllocatePtr(sizeof(T) * DEFAULT_MAX_CAPACITY);
            data.length = 0;
            data.capacity = DEFAULT_MAX_CAPACITY;
            data.world = index.worldPointer->selfPtr;
            mem_clear(data.Data.cached, DEFAULT_MAX_CAPACITY * sizeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ComponentArray(ref ComponentArray<T> other, int index)
        {
            ref var w = ref other.data.world.Ref;
            data = default;
            data.Data = w.AllocatorRef.AllocatePtr(sizeof(T) * DEFAULT_MAX_CAPACITY);
            data.length = other.data.length;
            data.capacity = other.data.capacity;
            data.world = other.data.world;
            mem_clear(data.Data.cached, DEFAULT_MAX_CAPACITY * sizeof(T));
            memcpy(data.Data.cached, other.data.Data.cached, other.data.length * sizeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T ElementAt(int index)
        {
            if (index < 0 || index >= data.length)
                throw new IndexOutOfRangeException();
            return ref data.ElementAt<T>(index);
        }
        public T ReadAt(int index)
        {
            if (index < 0 || index >= data.length)
                throw new IndexOutOfRangeException($"Index {index} is out of range");
            return data.ElementAt<T>(index);
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(in T item)
        {
            if (data.length >= data.capacity - 1) return;
            if (data.length == data.capacity) Resize(data.capacity == 0 ? 4 : data.capacity * 2);
            data.ElementAt<T>(data.length++) = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNoResize(in T item)
        {
            if (data.length < data.capacity) data.ElementAt<T>(data.length++) = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddParallel(in T item)
        {
            var idx = data.length;
            if (idx < data.capacity)
            {
                data.ElementAt<T>(idx) = item;
                Interlocked.Increment(ref data.length);
            }
            // Note: parallel expansion requires additional synchronization
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveRange(int index, int count)
        {
            if (data.length <= index + count - 1) return;

            int elemSize = UnsafeUtility.SizeOf<T>();

            mem_move(data.Data.cached + index * elemSize, data.Data.cached + (index + count) * elemSize, (long)elemSize * (Length - count - index));
            data.length -= count;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= data.length)
                throw new IndexOutOfRangeException();
            RemoveRange(index, 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            data.length = 0;
        }

        public void Dispose()
        {
            var w = data.world.Ptr;
            data.length = 0;
            data.capacity = 0;
            if (w != null) {
                w->AllocatorRef.Free(data.Data);
            }
            data.Data = default;
            data = default;
        }

        public ComponentArray<T> Copy(int to)
        {
            return new ComponentArray<T>(ref this, to);
        }

        public void Fill(T* buffer, int length)
        {
            memcpy(data.Data.As<T>(), buffer, length * sizeof(T));
            data.length = length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator()
        {
            return new Enumerator(data.Data.As<T>(), data.length);
        }

        private void Resize(int newCapacity)
        {
            var w = data.world.Ptr;
            var newBuffer = w->AllocatorRef.AllocatePtr(newCapacity * sizeof(T));
            if (!data.Data.IsNull)
            {
                memcpy(newBuffer.As<T>(), data.Data.As<T>(), data.length * sizeof(T));
                w->AllocatorRef.Free(data.Data);
            }

            data.Data = newBuffer;
            data.capacity = newCapacity;
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
                    if (i != buffer.Length - 1) buffer.data.ElementAt<T>(i) = buffer.data.ElementAt<T>(buffer.data.length - 1);
                    buffer.data.length--;
                    break;
                }

            return buffer.Length - 1;
        }
        [BurstCompile]
        public static void RemoveAtSwapBack<T>(this ref ComponentArray<T> buffer, int index)
            where T : unmanaged, IArrayComponent, IEquatable<T>
        {
            if (index != buffer.Length - 1) buffer.data.ElementAt<T>(index) = buffer.data.ElementAt<T>(buffer.data.length - 1);
            buffer.data.length--;
        }
    }
}
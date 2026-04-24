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
        internal byte* data;
        internal int ownerId;
        internal int elementPoolIndex;
        internal ptr<World.WorldUnsafe> worldPtr;
        internal ref MemAllocator Allocator => ref worldPtr.Ref.AllocatorRef;
        internal int length;
        internal int capacity;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void EnsureData()
        {
            if (data != null) return;
            ref var elementPool = ref worldPtr.Ref.GetElementUntypedPool(elementPoolIndex);
            data = elementPool.UnsafeBufferPtr.Ref.GetArraySlot(ownerId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref T ElementAt<T>(int index) where T : unmanaged
        {
            return ref ((T*)data)[index];
        }

        internal void Restore(ref MemAllocator allocator)
        {
            worldPtr.OnDeserialize(ref allocator);
            EnsureData();
        }
        internal static void Restore(byte* ptr, ref MemAllocator allocator)
        {
            var casted = (ComponentArrayData*)ptr;
            if(casted->capacity != 0)
                casted->Restore(ref allocator);
        }
    }
    public unsafe struct ComponentArray<T> : IPoolComponent, IDisposable, ICopyable<ComponentArray<T>>
        where T : unmanaged, IArrayComponent
    {
        internal const int DEFAULT_MAX_CAPACITY = ComponentArray.DEFAULT_MAX_CAPACITY;
        public int Length => data.length;
        internal ComponentArrayData data;

        internal ComponentArray(ref GenericPool elementPool, Entity entity)
        {
            data = default;
            data.elementPoolIndex = ComponentType<ComponentArray<T>>.Index + 1;
            data.data = elementPool.GetArraySlot(entity.id);
            data.ownerId = entity.id;
            data.length = 0;
            data.capacity = DEFAULT_MAX_CAPACITY;
            data.worldPtr = entity.worldPointer->selfPtr;
            mem_clear(data.data, DEFAULT_MAX_CAPACITY * sizeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ComponentArray(ref ComponentArray<T> other, int toEntity)
        {
            data = default;
            data.elementPoolIndex = other.data.elementPoolIndex;
            ref var elementPool = ref other.data.worldPtr.Ref.GetElementUntypedPool(data.elementPoolIndex);
            data.data = elementPool.GetArraySlot(toEntity);
            data.ownerId = toEntity;
            data.length = other.data.length;
            data.capacity = other.data.capacity;
            data.worldPtr = other.data.worldPtr;
            mem_clear(data.data, DEFAULT_MAX_CAPACITY * sizeof(T));
            data.EnsureData();
            other.data.EnsureData();
            memcpy(data.data, other.data.data, other.data.length * sizeof(T));
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
            data.EnsureData();
            return ((T*)data.data)[index];
        }
        public void Add(in T item)
        {
            data.EnsureData();
            if (data.length >= data.capacity - 1) return;
            data.ElementAt<T>(data.length++) = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNoResize(in T item)
        {
            data.EnsureData();
            if (data.length < data.capacity) data.ElementAt<T>(data.length++) = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddParallel(in T item)
        {
            data.EnsureData();
            var idx = data.length;
            if (idx < data.capacity)
            {
                data.ElementAt<T>(idx) = item;
                Interlocked.Increment(ref data.length);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveRange(int index, int count)
        {
            data.EnsureData();
            if (data.length <= index + count - 1) return;

            int elemSize = UnsafeUtility.SizeOf<T>();

            mem_move(data.data + index * elemSize, data.data + (index + count) * elemSize, (long)elemSize * (Length - count - index));
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
            data.length = 0;
            data.capacity = 0;
            data.data = null;
            data.ownerId = -1;
        }

        public ComponentArray<T> Copy(int to)
        {
            return new ComponentArray<T>(ref this, to);
        }

        public void Fill(T* buffer, int length)
        {
            data.EnsureData();
            memcpy((T*)data.data, buffer, length * sizeof(T));
            data.length = length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator()
        {
            data.EnsureData();
            return new Enumerator((T*)data.data, data.length);
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

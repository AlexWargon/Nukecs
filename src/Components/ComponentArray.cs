using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    internal struct ComponentArray
    {
        internal const int DEFAULT_MAX_CAPACITY = 8;
    }

    public unsafe struct ComponentArray<T> : IComponent, IDisposable, ICopyable<ComponentArray<T>> 
        where T : unmanaged, IArrayComponent
    {
        internal const int DEFAULT_MAX_CAPACITY = ComponentArray.DEFAULT_MAX_CAPACITY;
        internal T* buffer;
        internal int length;
        internal int capacity;
        internal Entity entity;
        public int Length => length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ComponentArray(int capacity)
        {
            buffer = (T*)UnsafeUtility.MallocTracked(capacity* sizeof(T), UnsafeUtility.AlignOf<T>(), Allocator.Persistent, 0);
            this.capacity = capacity;
            length = 0;
            entity = default;
        }
        
        internal ComponentArray(ref GenericPool pool, Entity index)
        {
            buffer = (T*)pool.UnsafeBuffer->buffer + index.id * DEFAULT_MAX_CAPACITY;
            length = 0;
            capacity = DEFAULT_MAX_CAPACITY;
            entity = index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ComponentArray(ref ComponentArray<T> other, int index)
        {
            entity = other.entity.worldPointer->GetEntity(index);
            var elementTypeIndex = ComponentType<ComponentArray<T>>.Index + 1;
            buffer = (T*)other.entity.worldPointer->GetUntypedPool(elementTypeIndex).UnsafeBuffer->buffer + entity.id * DEFAULT_MAX_CAPACITY;
            length = other.length;
            capacity = other.capacity;
            UnsafeUtility.MemCpy(buffer, other.buffer, length * sizeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T ElementAt(int index)
        {
            if (index < 0 || index >= length)
                throw new IndexOutOfRangeException();
            return ref buffer[index];
        }
        public T ReadAt(int index)
        {
            if (index < 0 || index >= length)
                throw new IndexOutOfRangeException();
            return buffer is null ? default : buffer[index];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(in T item)
        {
            if (length >= capacity - 1) return;
            if (length == capacity) Resize(capacity == 0 ? 4 : capacity * 2);
            buffer[length++] = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNoResize(in T item)
        {
            if (length < capacity) buffer[length++] = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddParallel(in T item)
        {
            var idx = length;
            if (idx < capacity)
            {
                buffer[idx] = item;
                Interlocked.Increment(ref length);
            }
            // Note: parallel expansion requires additional synchronization
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveRange(int index, int count)
        {
            if (length <= index + count - 1) return;

            int elemSize = UnsafeUtility.SizeOf<T>();

            UnsafeUtility.MemMove(buffer + index * elemSize, buffer + (index + count) * elemSize, (long)elemSize * (Length - count - index));
            length -= count;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= length)
                throw new IndexOutOfRangeException();
            RemoveRange(index, 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            length = 0;
        }

        public void Dispose()
        {
            buffer = null;
            length = 0;
            capacity = 0;
        }

        public ComponentArray<T> Copy(int to)
        {
            return new ComponentArray<T>(ref this, to);
        }

        public void Fill(T* buffer, int length)
        {
            UnsafeUtility.MemCpy(this.buffer, buffer, length * sizeof(T));
            this.length = length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator()
        {
            return new Enumerator(buffer, length);
        }

        private void Resize(int newCapacity)
        {
            var w = entity.worldPointer;
            var newBuffer = w->_allocate<T>(newCapacity);
            if (buffer != null)
            {
                UnsafeUtility.MemCpy(newBuffer, buffer, length * sizeof(T));
                w->_free(buffer);
            }

            buffer = newBuffer;
            capacity = newCapacity;
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
                    if (i != buffer.Length - 1) buffer.buffer[i] = buffer.buffer[buffer.length - 1];
                    buffer.length--;
                    break;
                }

            return buffer.Length - 1;
        }
    }
}
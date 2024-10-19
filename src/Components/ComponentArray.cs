using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs {
    internal struct ComponentArray
    {
        internal const int DefaultMaxCapacity = 16;
    }
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct ComponentArrayData<T> where T : unmanaged
    {
        internal T* buffer;
        internal int length;
        internal int capacity;
        internal Entity entity;

        public static ComponentArrayData<T>* New(T* buffer, int length, int capacity, Entity entity)
        {
            ComponentArrayData<T>* ptr = Unsafe.MallocTracked<ComponentArrayData<T>>(Allocator.Persistent);
            *ptr = new ComponentArrayData<T>
            {
                buffer = buffer,
                length = length,
                capacity = capacity,
                entity = entity
            };
            return ptr;
        }

        public static void Destroy(ComponentArrayData<T>* ptr)
        {
            Unsafe.FreeTracked<ComponentArrayData<T>>(ptr, Allocator.Persistent);
        }
    }
    public unsafe struct ComponentArray<T> : IComponent, IDisposable<ComponentArray<T>>, ICopyable<ComponentArray<T>> where T : unmanaged, IArrayComponent
    {
        internal const int DefaultMaxCapacity = ComponentArray.DefaultMaxCapacity;
        internal ComponentArrayData<T>* data;
        public int Length => data->length;
        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // private ComponentArray(int capacity) {
        //     _buffer = Unsafe.MallocTracked<T>(capacity, Allocator.Persistent);
        //     _capacity = capacity;
        //     _length = 0;
        //     _entity = default;
        //
        // }

        public ComponentArray(ref GenericPool pool, Entity index)
        {
            data = ComponentArrayData<T>.New((T*)pool.UnsafeBuffer->buffer + index.id * DefaultMaxCapacity, 0, DefaultMaxCapacity, index);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ComponentArray(ref ComponentArray<T> other, int index)
        {
            var entity = other.data->entity.worldPointer->GetEntity(index);
            var elementTypeIndex = ComponentType<ComponentArray<T>>.Index + 1;
            var buffer = (T*)other.data->entity.worldPointer->GetUntypedPool(elementTypeIndex).UnsafeBuffer->buffer + entity.id * DefaultMaxCapacity;
            var length = other.data->length;
            var capacity = other.data->capacity;
            data = ComponentArrayData<T>.New(buffer, length, capacity, entity);
            UnsafeUtility.MemCpy(data->buffer, other.data->buffer, length * sizeof(T));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentArray(int capacity, Allocator allocator)
        {
            var buffer = Unsafe.MallocTracked<T>(capacity, allocator);
            Entity entity = default;
            data = ComponentArrayData<T>.New(buffer, 0, DefaultMaxCapacity, entity);
        }
        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // private ComponentArray(ref ComponentArray<T> other)
        // {
        //     _buffer = Unsafe.MallocTracked<T>(other._capacity, Allocator.Persistent);
        //     _capacity = other._capacity;
        //     _length = other._length;
        //     UnsafeUtility.MemCpy(_buffer, other._buffer, _length * sizeof(T));
        //     _entity = default;
        // }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T ElementAt(int index)
        {
            if (index < 0 || index >= data->length)
                throw new IndexOutOfRangeException();
            return ref data->buffer[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(in T item)
        {
            if(data->length >= data->capacity -1 ) return;
            if (data->length == data->capacity)
            {
                Resize(data->capacity == 0 ? 4 : data->capacity * 2);
            }
            data->buffer[data->length++] = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNoResize(in T item)
        {
            if (data->length < data->capacity)
            {
                data->buffer[data->length++] = item;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddParallel(in T item)
        {
            int idx = data->length;
            if (idx < data->capacity)
            {
                data->buffer[idx] = item;
                Interlocked.Increment(ref data->length);
            }
            // Note: parallel expansion requires additional synchronization
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= data->length)
                throw new IndexOutOfRangeException();

            data->length--;
            if (index < data->length)
            {
                UnsafeUtility.MemCpy(data->buffer + index, data->buffer + index + 1, (data->length - index) * sizeof(T));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            data->length = 0;
        }
        public void Dispose(ref ComponentArray<T> value)
        {
            // if (value._buffer != null)
            // {
            //     Unsafe.FreeTracked(value._buffer, Allocator.Persistent);
            //     //UnsafeUtility.Free(value._buffer, Allocator.Persistent);
            //     value._buffer = null;
            // }
            value.data->buffer = null;
            value.data->length = 0;
            value.data->capacity = 0;
            ComponentArrayData<T>.Destroy(value.data);
        }

        public ComponentArray<T> Copy(ref ComponentArray<T> toCopy, int to)
        {
            return new ComponentArray<T>(ref toCopy, to);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComponentArray<T> CreateAndFill(in T data, int size, Allocator allocator)
        {
            var array = new ComponentArray<T>(size , allocator);
            for (int i = 0; i < size; i++)
            {
                array.Add(in data);
            }
            return array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator()
        {
            return new Enumerator(data->buffer, data->length);
        }

        private void Resize(int newCapacity)
        {
            T* newBuffer = Unsafe.MallocTracked<T>(newCapacity, Allocator.Persistent);
            if (data->buffer != null)
            {
                UnsafeUtility.MemCpy(newBuffer, data->buffer, data->length * sizeof(T));
                Unsafe.FreeTracked(data->buffer, Allocator.Persistent);
            }
            data->buffer = newBuffer;
            data->capacity = newCapacity;
        }

        public ref struct Enumerator
        {
            private readonly T* _listPtr;
            private readonly int _len;
            private int _index;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Enumerator(T* list, int length)
            {
                _listPtr = list;
                _len = length;
                _index = -1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                _index++;
                return _index < _len;
            }

            public void Reset()
            {
                _index = -1;
            }

            public ref T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref _listPtr[_index];
            }

            public void Dispose() { }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveSwapBack(T item)
        {
            for (int i = 0; i < data->length; i++)
            {
                if (data->buffer[i].Equals(item))
                {
                    if (i != data->length - 1)
                    {
                        // Если удаляемый элемент не последний, 
                        // заменяем его последним элементом
                        data->buffer[i] = data->buffer[data->length - 1];
                    }
                    data->length--;
                    return true;
                }
            }
            return false;
        }

    }
    [BurstCompile]
    public static class ComponentsArrayExtensions {
        [BurstCompile]
        public static unsafe int RemoveAtSwapBack<T>(this ref ComponentArray<T> buffer, in T item) where T: unmanaged, IArrayComponent, IEquatable<T> {
            for (int i = 0; i < buffer.Length; i++) {
                if (item.Equals(buffer.ElementAt(i))) {
                    if (i != buffer.Length - 1)
                    {
                        buffer.data->buffer[i] = buffer.data->buffer[buffer.data->length - 1];
                    }
                    buffer.data->length--;
                    break;
                }
            }
            return buffer.Length- 1;
        }
    }
}
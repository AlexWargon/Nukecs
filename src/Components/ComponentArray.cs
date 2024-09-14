using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs {
    internal struct ComponentArray
    {
        internal const int DefaultMaxCapacity = 64;
    }
    public unsafe struct ComponentArray<T> : IComponent, IDisposable<ComponentArray<T>>, ICopyable<ComponentArray<T>> where T : unmanaged, IArrayComponent
    {
        internal const int DefaultMaxCapacity = ComponentArray.DefaultMaxCapacity;
        internal T* _buffer;
        internal int _length;
        internal int _capacity;
        internal Entity _entity;
        public int Length => _length;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ComponentArray(int capacity) {
            _buffer = Unsafe.MallocTracked<T>(capacity, Allocator.Persistent);
            _capacity = capacity;
            _length = 0;
            _entity = default;
        }

        public ComponentArray(ref GenericPool pool, Entity index)
        {
            _buffer = (T*)pool.UnsafeBuffer->buffer + index.id * DefaultMaxCapacity;
            _length = 0;
            _capacity = DefaultMaxCapacity;
            _entity = index;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ComponentArray(ref ComponentArray<T> other, int index)
        {
            _entity = other._entity.worldPointer->GetEntity(index);
            _buffer = (T*)other._entity.worldPointer->GetPool<T>().UnsafeBuffer->buffer + _entity.id * DefaultMaxCapacity;
            _length = 0;
            _capacity = DefaultMaxCapacity;
            UnsafeUtility.MemCpy(_buffer, other._buffer, _length * sizeof(T));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ComponentArray(int capacity, Allocator allocator)
        {
            _buffer = Unsafe.MallocTracked<T>(capacity, allocator);
            _capacity = capacity;
            _length = 0;
            _entity = default;
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
            if (index < 0 || index >= _length)
                throw new IndexOutOfRangeException();
            return ref _buffer[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(in T item)
        {
            if(_length >= _capacity -1 ) return;
            if (_length == _capacity)
            {
                Resize(_capacity == 0 ? 4 : _capacity * 2);
            }
            _buffer[_length++] = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNoResize(in T item)
        {
            if (_length < _capacity)
            {
                _buffer[_length++] = item;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddParallel(in T item)
        {
            int idx = _length;
            if (idx < _capacity)
            {
                _buffer[idx] = item;
                Interlocked.Increment(ref _length);
            }
            // Note: parallel expansion requires additional synchronization
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _length)
                throw new IndexOutOfRangeException();

            _length--;
            if (index < _length)
            {
                UnsafeUtility.MemCpy(_buffer + index, _buffer + index + 1, (_length - index) * sizeof(T));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _length = 0;
        }
        public void Dispose(ref ComponentArray<T> value)
        {
            // if (value._buffer != null)
            // {
            //     Unsafe.FreeTracked(value._buffer, Allocator.Persistent);
            //     //UnsafeUtility.Free(value._buffer, Allocator.Persistent);
            //     value._buffer = null;
            // }
            value._buffer = null;
            value._length = 0;
            value._capacity = 0;
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
            return new Enumerator(_buffer, _length);
        }

        private void Resize(int newCapacity)
        {
            T* newBuffer = Unsafe.MallocTracked<T>(newCapacity, Allocator.Persistent);
            if (_buffer != null)
            {
                UnsafeUtility.MemCpy(newBuffer, _buffer, _length * sizeof(T));
                Unsafe.FreeTracked(_buffer, Allocator.Persistent);
            }
            _buffer = newBuffer;
            _capacity = newCapacity;
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
            for (int i = 0; i < _length; i++)
            {
                if (_buffer[i].Equals(item))
                {
                    if (i != _length - 1)
                    {
                        // Если удаляемый элемент не последний, 
                        // заменяем его последним элементом
                        _buffer[i] = _buffer[_length - 1];
                    }
                    _length--;
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
                        buffer._buffer[i] = buffer._buffer[buffer._length - 1];
                    }
                    buffer._length--;
                    break;
                }
            }
            return buffer.Length- 1;
        }
    }
}
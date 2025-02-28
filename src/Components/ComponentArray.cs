﻿using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    internal struct ComponentArray
    {
        internal const int DEFAULT_MAX_CAPACITY = 16;
    }

    public unsafe struct ComponentArray<T> : IComponent, IDisposable, ICopyable<ComponentArray<T>> 
        where T : unmanaged, IArrayComponent
    {
        internal const int DEFAULT_MAX_CAPACITY = ComponentArray.DEFAULT_MAX_CAPACITY;
        internal T* _buffer;
        internal int _length;
        internal int _capacity;
        internal Entity _entity;
        public int Length => _length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ComponentArray(int capacity)
        {
            _buffer = (T*)UnsafeUtility.MallocTracked(capacity* sizeof(T), UnsafeUtility.AlignOf<T>(), Allocator.Persistent, 0);
            _capacity = capacity;
            _length = 0;
            _entity = default;
        }
        
        internal ComponentArray(ref GenericPool pool, Entity index)
        {
            _buffer = (T*)pool.UnsafeBuffer->buffer + index.id * DEFAULT_MAX_CAPACITY;
            _length = 0;
            _capacity = DEFAULT_MAX_CAPACITY;
            _entity = index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ComponentArray(ref ComponentArray<T> other, int index)
        {
            _entity = other._entity.worldPointer->GetEntity(index);
            var elementTypeIndex = ComponentType<ComponentArray<T>>.Index + 1;
            _buffer = (T*)other._entity.worldPointer->GetUntypedPool(elementTypeIndex).UnsafeBuffer->buffer + _entity.id * DEFAULT_MAX_CAPACITY;
            _length = other._length;
            _capacity = other._capacity;
            UnsafeUtility.MemCpy(_buffer, other._buffer, _length * sizeof(T));
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
            if (_length >= _capacity - 1) return;
            if (_length == _capacity) Resize(_capacity == 0 ? 4 : _capacity * 2);
            _buffer[_length++] = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNoResize(in T item)
        {
            if (_length < _capacity) _buffer[_length++] = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddParallel(in T item)
        {
            var idx = _length;
            if (idx < _capacity)
            {
                _buffer[idx] = item;
                Interlocked.Increment(ref _length);
            }
            // Note: parallel expansion requires additional synchronization
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveRange(int index, int count)
        {
            if (_length <= index + count - 1) return;

            int elemSize = UnsafeUtility.SizeOf<T>();

            UnsafeUtility.MemMove(_buffer + index * elemSize, _buffer + (index + count) * elemSize, (long)elemSize * (Length - count - index));
            _length -= count;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _length)
                throw new IndexOutOfRangeException();
            RemoveRange(index, 1);
            // _length--;
            // if (index < _length)
            //     UnsafeUtility.MemCpy(_buffer + index, _buffer + index + 1, (_length - index) * sizeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _length = 0;
        }

        public void Dispose()
        {
            // if (value._buffer != null)
            // {
            //     Unsafe.FreeTracked(value._buffer, Allocator.Persistent);
            //     //UnsafeUtility.Free(value._buffer, Allocator.Persistent);
            //     value._buffer = null;
            // }
            _buffer = null;
            _length = 0;
            _capacity = 0;
        }

        public ComponentArray<T> Copy(int to)
        {
            return new ComponentArray<T>(ref this, to);
        }

        public void Fill(T* buffer, int length)
        {
            UnsafeUtility.MemCpy(_buffer, buffer, length * sizeof(T));
            _length = length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator()
        {
            return new Enumerator(_buffer, _length);
        }

        private void Resize(int newCapacity)
        {
            var w = _entity.worldPointer;
            var newBuffer = w->_allocate<T>(newCapacity);
            if (_buffer != null)
            {
                UnsafeUtility.MemCpy(newBuffer, _buffer, _length * sizeof(T));
                w->_free(_buffer, _capacity);
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

            public void Dispose()
            {
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveSwapBack(T item)
        {
            for (var i = 0; i < _length; i++)
                if (_buffer[i].Equals(item))
                {
                    if (i != _length - 1)
                        // If the element to be removed is not the last one, 
                        // replace it with the last element
                        _buffer[i] = _buffer[_length - 1];
                    _length--;
                    return true;
                }

            return false;
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
                    if (i != buffer.Length - 1) buffer._buffer[i] = buffer._buffer[buffer._length - 1];
                    buffer._length--;
                    break;
                }

            return buffer.Length - 1;
        }
    }
}
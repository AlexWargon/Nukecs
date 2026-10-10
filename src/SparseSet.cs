using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    public unsafe struct SparseSet : IDisposable
    {
        private int* _sparse;
        private Entity* _dense;
        private int _count;
        private int _capacity;
        private int _sparseCapacity;

        public SparseSet(int initialCapacity, int sparseCapacity)
        {
            _sparse = (int*)Mem.Malloc(sparseCapacity * sizeof(int), Mem.AlignOf<int>(), AllocatorHandle.Persistent);
            _dense = (Entity*)Mem.Malloc(initialCapacity * sizeof(Entity), Mem.AlignOf<Entity>(), AllocatorHandle.Persistent);
            _count = 0;
            _capacity = initialCapacity;
            _sparseCapacity = sparseCapacity;

            Mem.MemSet(_sparse, 0xFF, _sparseCapacity * sizeof(int)); // Initialize with -1
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(Entity item)
        {
            if (item.id < 0 || item.id >= _sparseCapacity)
                throw new ArgumentOutOfRangeException(nameof(item));

            if (_sparse[item.id] == -1)
            {
                if (_count == _capacity)
                {
                    Resize(_capacity * 2);
                }

                _dense[_count] = item;
                _sparse[item.id] = _count;
                _count++;
            }
        }

        public ref Entity Get(int index)
        {
            return ref _dense[index];
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int item)
        {
            if (item < 0 || item >= _sparseCapacity)
                throw new ArgumentOutOfRangeException(nameof(item));

            int index = _sparse[item];
            if (index != -1 && index < _count)
            {
                Entity lastItem = _dense[_count - 1];
                _dense[index] = lastItem;
                _sparse[lastItem.id] = index;
                _sparse[item] = -1;
                _count--;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(Entity item)
        {
            if (item.id < 0 || item.id >= _sparseCapacity)
                return false;

            int index = _sparse[item.id];
            return index != -1 && index < _count && _dense[index] == item;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(int item)
        {
            if (item < 0 || item >= _sparseCapacity)
                return false;

            int index = _sparse[item];
            return index != -1 && index < _count && _dense[index].id == item;
        }
        private void Resize(int newCapacity)
        {
            var newDense = (Entity*)Mem.Malloc(newCapacity * sizeof(Entity), Mem.AlignOf<Entity>(), AllocatorHandle.Persistent);
            Mem.MemCpy(newDense, _dense, _count * sizeof(Entity));
            Mem.Free(_dense, AllocatorHandle.Persistent);
            _dense = newDense;
            _capacity = newCapacity;
        }

        public void Dispose()
        {
            if (_sparse != null)
            {
                Mem.Free(_sparse, AllocatorHandle.Persistent);
                _sparse = null;
            }
            if (_dense != null)
            {
                Mem.Free(_dense, AllocatorHandle.Persistent);
                _dense = null;
            }
            _count = 0;
            _capacity = 0;
            _sparseCapacity = 0;
        }

        public int Count => _count;

        // Метод для перечисления элементов (может быть небезопасным в многопоточной среде)
        public Enumerator GetEnumerator() => new Enumerator(this);

        public struct Enumerator
        {
            private readonly SparseSet _set;
            private int _index;

            internal Enumerator(SparseSet set)
            {
                _set = set;
                _index = -1;
            }

            public bool MoveNext()
            {
                return ++_index < _set._count;
            }

            public ref Entity Current => ref _set._dense[_index];
        }
    }
}
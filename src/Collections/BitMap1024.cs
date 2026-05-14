using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs
{
    public unsafe struct BitMap1024<T>
        where T : unmanaged
    {
        public Bitmask1024 Mask;
        private MemoryArray<T> _values;

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Mask.Count;
        }

        public BitMap1024(
            int initialCapacity,
            ref MemAllocator allocator)
        {
            Mask = default;

            if (initialCapacity < 1)
                initialCapacity = 1;

            _values = new MemoryArray<T>(
                initialCapacity,
                ref allocator,
                clear: false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(int key)
        {
            return Mask.HasFast(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(int key, out T value)
        {
            if (!Mask.HasFast(key))
            {
                value = default;
                return false;
            }

            var index = Mask.CountBefore(key);

            value = _values[index];

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetRef(int key)
        {
            var index = Mask.CountBefore(key);

            return ref _values[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(
            int key,
            in T value,
            ref MemAllocator allocator)
        {
            if (Mask.HasFast(key))
            {
                var index = Mask.CountBefore(key);

                _values[index] = value;

                return;
            }

            InsertNew(
                key,
                value,
                ref allocator);
        }

        public bool Remove(int key)
        {
            if (!Mask.HasFast(key))
                return false;

            var index =
                Mask.CountBefore(key);

            RemoveAt(index);

            Mask.Remove(key);

            return true;
        }

        public void Clear()
        {
            Mask.Clear();
        }

        public void Dispose()
        {
            _values.Dispose();
            Mask.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InsertNew(
            int key,
            in T value,
            ref MemAllocator allocator)
        {
            var index =
                Mask.CountBefore(key);

            var oldCount =
                Mask.Count;

            _values.EnsureCapacity(
                oldCount + 1,
                ref allocator);

            var moveCount =
                oldCount - index;

            if (moveCount > 0)
            {
                UnsafeUtility.MemMove(
                    _values.Ptr + index + 1,
                    _values.Ptr + index,
                    sizeof(T) * moveCount);
            }

            _values[index] = value;

            Mask.Add(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RemoveAt(int index)
        {
            var count =
                Mask.Count;

            var moveCount =
                count - index - 1;

            if (moveCount > 0)
            {
                UnsafeUtility.MemMove(
                    _values.Ptr + index,
                    _values.Ptr + index + 1,
                    sizeof(T) * moveCount);
            }
        }
    }
}
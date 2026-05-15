using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Wargon.Nukecs.Collections;
using static Wargon.Nukecs.UnsafeStatic;
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnDeserialize(ref MemAllocator allocator)
        {
            _values.OnDeserialize(ref allocator);
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
        public long Size() => _values.GetMemorySizeUsed() + sizeof(BitMap1024<T>);
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
        public void Add(
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
    
    public readonly unsafe struct PerThreadValue<T> : IDisposable where T : unmanaged
    {
        private readonly T* _value;
        private readonly Allocator _allocator;
        public PerThreadValue(int threadCount, Allocator allocator)
        {
            _value = (T*)malloc_t<T>(allocator, threadCount);
            _allocator = allocator;
        }

        public ref T Ref(int threadIndex)
        {
            return ref _value[threadIndex];
        }

        public void Dispose()
        {
            free_t(_value, _allocator);
        }
    }
    
    public unsafe struct ZeroMoveBitMap1024<T>
        where T : unmanaged
    {
        public long Size()
        {
            return sizeof(ZeroMoveBitMap1024<T>) + values.GetMemorySizeUsed() + keys.GetMemorySizeUsed() +
                   indexByKey.GetMemorySizeUsed();
        }
        // dense storage
        public MemoryArray<T> values;

        // dense keys aligned with values
        public MemoryArray<int> keys;

        // inverse map: key -> index in dense arrays
        public MemoryArray<int> indexByKey;

        // occupancy
        public Bitmask1024 mask;

        public int count;

        public ZeroMoveBitMap1024(int capacity, ref MemAllocator allocator)
        {
            values = new MemoryArray<T>(capacity, ref allocator);
            keys = new MemoryArray<int>(capacity, ref allocator);
            indexByKey = new MemoryArray<int>(1024, ref allocator);
            mask = default;
            count = 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(
            int required,
            ref MemAllocator allocator)
        {
            values.EnsureCapacity(required, ref allocator);
            keys.EnsureCapacity(required, ref allocator);
        }
        public ref T Get(int key)
        {
            return ref values[indexByKey[key]];
        }
        public bool Has(int key)
        {
            return mask.HasFast(key);
        }
        public void Add(int key, in T value)
        {
            if (mask.HasFast(key))
                return;

            int idx = count;

            //EnsureCapacity(idx + 1);

            values[idx] = value;
            keys[idx] = key;

            indexByKey[key] = idx;

            mask.Add(key);
            count++;
        }
        public bool Remove(int key)
        {
            if (!mask.Remove(key))
                return false;

            int idx = indexByKey[key];
            int last = count - 1;

            if (idx != last)
            {
                // swap values
                values[idx] = values[last];

                // swap keys
                int lastKey = keys[last];
                keys[idx] = lastKey;

                indexByKey[lastKey] = idx;
            }

            count--;
            return true;
        }
    }
    
}
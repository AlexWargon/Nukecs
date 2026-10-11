using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs.Collections
{
    /// <summary>
    /// Heap <see cref="HashMap{TKey,TValue}"/> with handle semantics (like NativeHashMap): the
    /// map lives in its own allocation, so copies of this struct share it.
    /// </summary>
    public unsafe struct HeapHashMap<TKey, TValue> : IDisposable
        where TKey : unmanaged, IEquatable<TKey> where TValue : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        private HashMap<TKey, TValue>* map;
        private AllocatorHandle allocator;

        public HeapHashMap(int capacity, AllocatorHandle allocator)
        {
            this.allocator = allocator;
            map = (HashMap<TKey, TValue>*)Mem.Malloc(sizeof(HashMap<TKey, TValue>), Mem.AlignOf<HashMap<TKey, TValue>>(), allocator);
            *map = new HashMap<TKey, TValue>(capacity, allocator);
        }

        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => map != null;
        }

        public int Count => map->Count;

        public TValue this[TKey key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (*map)[key];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => (*map)[key] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue item) => map->TryGetValue(key, out item);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(TKey key, TValue item) => map->TryAdd(key, item);

        public void Add(TKey key, TValue item) => map->Add(key, item);

        public bool Remove(TKey key) => map->Remove(key);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(TKey key) => map->ContainsKey(key);

        public void Clear() => map->Clear();

        public HashMap<TKey, TValue>.Enumerator GetEnumerator() => map->GetEnumerator();

        public void Dispose()
        {
            if (map == null) return;
            map->Dispose();
            Mem.Free(map, allocator);
            map = null;
        }
    }
}

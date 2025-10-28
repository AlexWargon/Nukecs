using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wargon.Nukecs.Collections
{
    [StructLayout(LayoutKind.Sequential)]
    public struct AliveEntitiesSet
    {
        private MemoryList<int> dense;
        private MemoryList<int> sparse;

        public void OnDeserialize(ref MemAllocator allocator)
        {
            dense.OnDeserialize(ref allocator);
            sparse.OnDeserialize(ref allocator);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AliveEntitiesSet(int maxEntities, ref MemAllocator allocator)
        {
            dense = new MemoryList<int>(maxEntities, ref allocator);
            sparse = new MemoryList<int>(maxEntities, ref allocator, lenAsCapacity: true);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(int entityId)
        {
            if (entityId < 0 || entityId >= sparse.Length) return false;
            return sparse[entityId] > 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int entityId, ref MemAllocator allocator)
        {
            if (Contains(entityId)) return;

            var index = dense.Length;
            dense.Add(entityId, ref allocator);
            sparse[entityId] = index;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int entityId)
        {
            if (!Contains(entityId)) return;

            var idx = sparse[entityId];
            var lastIndex = dense.Length - 1;
            var lastEntity = dense[lastIndex];

            dense[idx] = lastEntity;      // swap back
            dense.ResizeToSmall(dense.Length - 1);

            sparse[lastEntity] = idx;
            sparse[entityId] = -1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<int> GetAliveEntities()
        {
            return dense.AsSpan();
        }
    }
}
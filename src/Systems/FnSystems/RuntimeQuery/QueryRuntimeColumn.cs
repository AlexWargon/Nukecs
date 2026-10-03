using System;
using System.Runtime.CompilerServices;

namespace Wargon.Nukecs
{
    internal static unsafe class QueryRuntimeSlot<T> where T : unmanaged
    {
        public static readonly bool IsEntity = typeof(T) == typeof(Entity);
        public static readonly bool IsComponent = ComponentType<T>.IsComponent;
        public static readonly bool IsPool = IsComponent && ComponentType<T>.Data.category == ComponentCategory.Pool;
        public static readonly bool IsTag = IsComponent && ComponentType<T>.Data.category == ComponentCategory.Tag;
        public static readonly bool IsSpecial = IsEntity || !IsComponent || IsTag;
    }

    public unsafe struct QueryRuntimeColumn
    {
        public ComponentPoolUntyped* Pool;
        public byte* Base;
        private int typeIndex;
        public bool Init<T>(World.WorldUnsafe* world) where T : unmanaged
        {
            if (QueryRuntimeSlot<T>.IsComponent)
            {
                var category = ComponentType<T>.Data.category;
                if ((category == ComponentCategory.Pool) != QueryRuntimeSlot<T>.IsPool ||
                    (category == ComponentCategory.Tag) != QueryRuntimeSlot<T>.IsTag)
                    throw new InvalidOperationException("Component storage category changed after tuple initialization.");
            }
            if (QueryRuntimeSlot<T>.IsSpecial)
            {
                // Tags/filters have no archetype payload. Entity keeps the stable world owner,
                // so a later entity-table resize cannot leave a cached table pointer dangling.
                typeIndex = -1;
                Pool = null;
                Base = QueryRuntimeSlot<T>.IsEntity ? (byte*)world : (byte*)TagSlotStub<T>.GetPtr();
                return false;
            }
            typeIndex = ComponentType<T>.Index;
            Pool = QueryRuntimeSlot<T>.IsPool ? world->GetUntypedPoolPtr(typeIndex)->UnsafeBuffer : null;
            return Pool != null;
        }
        public void SetArchetype(ref ArchetypeUnsafe arch)
        {
            if (Pool == null && typeIndex >= 0) Base = arch.data.Ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(typeIndex));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* SpecialAddress<T>(int entity) where T : unmanaged => QueryRuntimeSlot<T>.IsEntity
            ? (byte*)(((World.WorldUnsafe*)Base)->entities.Ptr + entity) : Base;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* Page(int index)
        {
            if ((uint)index < (uint)Pool->Chunks.capacity)
            {
                ref var page = ref Pool->Chunks.Ptr[index];
                if (page.isCreated == 1) return page.buffer.Ptr;
            }
            return MissingPage(index);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private byte* MissingPage(int index) => Pool->GetPtr(index * Chunk.MAX_CHUNK_SIZE);
    }
}

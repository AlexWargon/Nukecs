using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wargon.Nukecs.Tests
{
    // Separate experiment: iter_compact_runtime remains the unchanged inline control.
    public static unsafe class RuntimeQueryIter4MixedApi
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static RuntimeMixedIter<RuntimeMixedRefs<T1, T2, T3, T4>> iter_mixed_runtime<T1, T2, T3, T4>(
            this in Query<T1, T2, T3, T4> query)
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent
        {
            query.TryGetQuery(out var raw);
            return new RuntimeMixedIter<RuntimeMixedRefs<T1, T2, T3, T4>>(raw.Ptr);
        }

        // Four data components plus a filter (including a bare tag). Tags have no tuple slot.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static RuntimeMixedIter<RuntimeMixedRefs<T1, T2, T3, T4>> iter_mixed_runtime<T1, T2, T3, T4, TFilter>(
            this in Query<T1, T2, T3, T4, TFilter> query)
            where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
            where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where TFilter : unmanaged
        {
            if (QueryParamInfo<TFilter>.IsComponent && ComponentType<TFilter>.Data.category != ComponentCategory.Tag)
                throw new InvalidOperationException("The fifth parameter must be a filter or tag, not a data component.");
            query.TryGetQuery(out var raw);
            return new RuntimeMixedIter<RuntimeMixedRefs<T1, T2, T3, T4>>(raw.Ptr);
        }
    }

    public unsafe struct RuntimeMixedColumn
    {
        private byte* data;
        // The pool table can grow while subsequent columns are initialized.
        // The separately allocated buffer owner stays stable across that resize.
        private ComponentPoolUntyped* pool;
        private int typeIndex;
        private int size;

        public bool Init<T>(World.WorldUnsafe* world) where T : unmanaged, IComponent
        {
            var type = ComponentType<T>.Data;
            if (type.category == ComponentCategory.Tag)
                throw new InvalidOperationException("Tags are filters only. Put the tag in the final filter parameter.");
            typeIndex = ComponentType<T>.Index;
            size = sizeof(T);
            pool = type.category == ComponentCategory.Pool ? world->GetUntypedPoolPtr(typeIndex)->UnsafeBuffer : null;
            return pool != null;
        }

        public void SetArchetype(ref ArchetypeUnsafe storage)
        {
            // A pool has no column in the physical storage: never request its offset.
            if (pool == null)
                data = storage.data.Ptr + storage.GetComponentOffset(storage.GetComponentLocalIndex(typeIndex));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* At(int row, int entityId) => pool != null ? pool->GetPtr(entityId) : data + row * size;
    }

    public unsafe struct RuntimeMixedRows
    {
        public RuntimeMixedColumn A, B, C, D;
        public int* PackedEntities;
        public bool HasPools;

        public void SetArchetype(ref ArchetypeUnsafe storage)
        {
            A.SetArchetype(ref storage); B.SetArchetype(ref storage);
            C.SetArchetype(ref storage); D.SetArchetype(ref storage);
            PackedEntities = storage.packedEntities.Ptr;
        }
    }

    public unsafe interface IRuntimeMixedTuple : IRuntimeDenseTuple
    {
        void Init(ref RuntimeMixedRows rows, World.WorldUnsafe* world);
        void Gather(ref RuntimeMixedRows rows, int row);
        void SetArchetype(ref ArchetypeUnsafe archetype);
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct RuntimeMixedRefs<T1, T2, T3, T4> : IRuntimeMixedTuple
        where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent
    {
        private byte* a;
        private long b, c, d;

        public void Init(ref RuntimeMixedRows rows, World.WorldUnsafe* world)
        {
            // Evaluate all four, without short-circuiting, once per enumeration.
            rows.HasPools = rows.A.Init<T1>(world) | rows.B.Init<T2>(world) |
                            rows.C.Init<T3>(world) | rows.D.Init<T4>(world);
        }

        public void Gather(ref RuntimeMixedRows rows, int row)
        {
            var entityId = rows.HasPools ? rows.PackedEntities[row] : 0;
            a = rows.A.At(row, entityId);
            b = rows.B.At(row, entityId) - a;
            c = rows.C.At(row, entityId) - a;
            d = rows.D.At(row, entityId) - a;
        }

        public void SetStorage(ref StorageArchetype storage)
        {
            var firstOffset = storage.GetComponentOffset(storage.GetComponentLocalIndex(ComponentType<T1>.Index));
            a = storage.data.Ptr + firstOffset;
            b = storage.GetComponentOffset(storage.GetComponentLocalIndex(ComponentType<T2>.Index)) - firstOffset;
            c = storage.GetComponentOffset(storage.GetComponentLocalIndex(ComponentType<T3>.Index)) - firstOffset;
            d = storage.GetComponentOffset(storage.GetComponentLocalIndex(ComponentType<T4>.Index)) - firstOffset;
        }

        public void SetArchetype(ref ArchetypeUnsafe archetype)
        {
            var firstOffset = archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T1>.Index));
            a = archetype.data.Ptr + firstOffset;
            b = archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T2>.Index)) - firstOffset;
            c = archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T3>.Index)) - firstOffset;
            d = archetype.GetComponentOffset(archetype.GetComponentLocalIndex(ComponentType<T4>.Index)) - firstOffset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetEnd(int count) => a + count * sizeof(T1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Advance(byte* end)
        {
            var next = a + sizeof(T1);
            a = next;
            if (sizeof(T2) != sizeof(T1)) b += sizeof(T2) - sizeof(T1);
            if (sizeof(T3) != sizeof(T1)) c += sizeof(T3) - sizeof(T1);
            if (sizeof(T4) != sizeof(T1)) d += sizeof(T4) - sizeof(T1);
            return next < end;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Deconstruct(out Ref<T1> p1, out Ref<T2> p2, out Ref<T3> p3, out Ref<T4> p4)
        { p1.data = (T1*)a; p2.data = (T2*)(a + b); p3.data = (T3*)(a + c); p4.data = (T4*)(a + d); }
    }

    public unsafe ref struct RuntimeMixedIter<TTuple> where TTuple : unmanaged, IRuntimeMixedTuple
    {
        private TTuple current;
        private readonly World.WorldUnsafe* world;
        private readonly int* matches;
        private readonly int matchCount;
        private int block;
        private byte* end;
        private readonly bool storageMode;
        private RuntimeMixedRows columns;
        private int* rows;
        private int rowIndex, rowCount;

        public RuntimeMixedIter(QueryUnsafe* query)
        {
            current = default;
            world = query->world;
            columns = default;
            current.Init(ref columns, world);
            storageMode = !columns.HasPools && query->TryUseStorageIteration();
            var list = storageMode ? query->GetMatchingStorages() : query->matchingArchetypes;
            matches = list.Ptr;
            matchCount = list.Length;
            block = -1;
            end = null;
            rows = null;
            rowIndex = rowCount = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RuntimeMixedIter<TTuple> GetEnumerator() => this;
        public readonly TTuple Current { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => current; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() => current.Advance(end) || Next();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool Next()
        {
            // end == null disables the dense step for mixed/sparse blocks. Advance
            // only did arithmetic; Gather replaces every address before Current is read.
            if (rowIndex < rowCount)
            {
                current.Gather(ref columns, rows != null ? rows[rowIndex] : rowIndex);
                rowIndex++;
                return true;
            }

            while (++block < matchCount)
            {
                if (storageMode)
                {
                    ref var storage = ref world->storagesList.Ptr[matches[block]].Ref;
                    var count = storage.count;
                    if (count == 0) continue;
                    current.SetStorage(ref storage);
                    end = current.GetEnd(count);
                    return true;
                }

                ref var arch = ref world->archetypesList.Ptr[matches[block]].Ref;
                var archCount = arch.count;
                if (archCount == 0) continue;
                if (!columns.HasPools && arch.RowsAreDense)
                {
                    current.SetArchetype(ref arch);
                    end = current.GetEnd(archCount);
                    return true;
                }

                end = null;
                columns.SetArchetype(ref arch);
                rows = arch.RowsAreDense ? null : arch.rows.Ptr;
                rowCount = archCount; // Snapshot, including in the pool/gather path.
                rowIndex = 1;
                current.Gather(ref columns, rows != null ? rows[0] : 0);
                return true;
            }
            end = null;
            return false;
        }
    }
}

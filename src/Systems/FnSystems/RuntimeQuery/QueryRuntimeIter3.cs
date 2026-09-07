using System;
using System.Runtime.CompilerServices;

namespace Wargon.Nukecs
{
    // Arity 3; the traversal and tuple representation follow the measured four-component candidate.
    public unsafe struct QueryRuntimeRows3
    {
        public QueryRuntimeColumn C0, C1, C2;
        public int* Entities;
        public int PageIndex;
        public void SetArchetype(ref ArchetypeUnsafe arch)
        {
            C0.SetArchetype(ref arch);
            C1.SetArchetype(ref arch);
            C2.SetArchetype(ref arch);
            Entities = arch.packedEntities.Ptr;
            PageIndex = -1;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void LoadPage(int page)
        {
            if (C0.Pool != null) C0.Base = C0.Page(page);
            if (C1.Pool != null) C1.Base = C1.Page(page);
            if (C2.Pool != null) C2.Base = C2.Page(page);
            PageIndex = page;
        }
    }
    public unsafe interface IQueryRuntimeTuple3
    {
        int Init(ref QueryRuntimeRows3 columns, World.WorldUnsafe* world);
        bool UsesPools();
        void SetStorage(ref StorageArchetype storage, int start);
        void SetArchetype(ref ArchetypeUnsafe arch, int start);
        bool Advance(byte* end);
        byte* GetEnd(int count);
        void GatherInline(ref QueryRuntimeRows3 columns, int row);
        void Gather(ref QueryRuntimeRows3 columns, int row);
    }
    public unsafe struct QueryRuntimeRefs<T1, T2, T3> : IQueryRuntimeTuple3
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged
    {
        private static readonly bool Pool0 = QueryRuntimeSlot<T1>.IsPool;
        private static readonly bool Pool1 = QueryRuntimeSlot<T2>.IsPool;
        private static readonly bool Pool2 = QueryRuntimeSlot<T3>.IsPool;
        private static readonly bool HasPools = Pool0 | Pool1 | Pool2;
        private static readonly bool HasSpecial = QueryRuntimeSlot<T1>.IsSpecial | QueryRuntimeSlot<T2>.IsEntity | QueryRuntimeSlot<T3>.IsEntity;
        private static readonly bool UsesGather = HasPools | HasSpecial;
        // Inline: one base plus relative offsets. Mixed/pool: absolute addresses.
        private T1* p0;
        private T2* p1;
        private T3* p2;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool UsesPools() => UsesGather;
        public int Init(ref QueryRuntimeRows3 columns, World.WorldUnsafe* world)
        {
            var mask = (columns.C0.Init<T1>(world) ? 1 : 0) | (columns.C1.Init<T2>(world) ? 2 : 0) | (columns.C2.Init<T3>(world) ? 4 : 0);
            if (mask != ((Pool0 ? 1 : 0) | (Pool1 ? 2 : 0) | (Pool2 ? 4 : 0)))
                throw new InvalidOperationException("Component storage category changed after tuple initialization.");
            return mask | (HasSpecial ? 512 : 0);
        }
        public void SetStorage(ref StorageArchetype storage, int start)
        {
            Set((QueryRuntimeSlot<T1>.IsSpecial ? (byte*)TagSlotStub<T1>.GetPtr() : storage.data.Ptr + storage.GetComponentOffset(storage.GetComponentLocalIndex(ComponentType<T1>.Index)) + start * sizeof(T1)), (QueryRuntimeSlot<T2>.IsSpecial ? (byte*)TagSlotStub<T2>.GetPtr() : storage.data.Ptr + storage.GetComponentOffset(storage.GetComponentLocalIndex(ComponentType<T2>.Index)) + start * sizeof(T2)), (QueryRuntimeSlot<T3>.IsSpecial ? (byte*)TagSlotStub<T3>.GetPtr() : storage.data.Ptr + storage.GetComponentOffset(storage.GetComponentLocalIndex(ComponentType<T3>.Index)) + start * sizeof(T3)));
        }
        public void SetArchetype(ref ArchetypeUnsafe arch, int start)
        {
            Set((QueryRuntimeSlot<T1>.IsSpecial ? (byte*)TagSlotStub<T1>.GetPtr() : arch.data.Ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T1>.Index)) + start * sizeof(T1)), (QueryRuntimeSlot<T2>.IsSpecial ? (byte*)TagSlotStub<T2>.GetPtr() : arch.data.Ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T2>.Index)) + start * sizeof(T2)), (QueryRuntimeSlot<T3>.IsSpecial ? (byte*)TagSlotStub<T3>.GetPtr() : arch.data.Ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T3>.Index)) + start * sizeof(T3)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetEnd(int count) => (byte*)(p0 + count);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Advance(byte* end)
        {
            p0++;
            if (UsesGather) { p1++; p2++; }
            else
            {
                if (QueryRuntimeSlot<T2>.IsSpecial) p1 = (T2*)((byte*)p1 - sizeof(T1));
                else if (sizeof(T2) != sizeof(T1)) p1 = (T2*)((byte*)p1 + sizeof(T2) - sizeof(T1));
                if (QueryRuntimeSlot<T3>.IsSpecial) p2 = (T3*)((byte*)p2 - sizeof(T1));
                else if (sizeof(T3) != sizeof(T1)) p2 = (T3*)((byte*)p2 + sizeof(T3) - sizeof(T1));
            }
            return (byte*)p0 < end;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Set(byte* a0, byte* a1, byte* a2)
        {
            p0 = (T1*)a0;
            if (UsesGather)
            {
                p1 = (T2*)a1;
                p2 = (T3*)a2;
            }
            else
            {
                p1 = (T2*)(a1 - a0);
                p2 = (T3*)(a2 - a0);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GatherInline(ref QueryRuntimeRows3 columns, int row)
        {
            Set((QueryRuntimeSlot<T1>.IsSpecial ? columns.C0.Base : columns.C0.Base + row * sizeof(T1)), (QueryRuntimeSlot<T2>.IsSpecial ? columns.C1.Base : columns.C1.Base + row * sizeof(T2)), (QueryRuntimeSlot<T3>.IsSpecial ? columns.C2.Base : columns.C2.Base + row * sizeof(T3)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Gather(ref QueryRuntimeRows3 columns, int row)
        {
            var slot = 0;
            var entity = 0;
            if (UsesGather)
            {
                entity = columns.Entities[row];
                var page = entity >> Chunk.CHUNK_INDEX_BITSFIFT;
                slot = entity & (Chunk.MAX_CHUNK_SIZE - 1);
                if (HasPools && page != columns.PageIndex) columns.LoadPage(page);
            }
            Set((QueryRuntimeSlot<T1>.IsSpecial ? columns.C0.SpecialAddress<T1>(entity) : columns.C0.Base + (Pool0 ? slot : row) * sizeof(T1)), (QueryRuntimeSlot<T2>.IsSpecial ? columns.C1.SpecialAddress<T2>(entity) : columns.C1.Base + (Pool1 ? slot : row) * sizeof(T2)), (QueryRuntimeSlot<T3>.IsSpecial ? columns.C2.SpecialAddress<T3>(entity) : columns.C2.Base + (Pool2 ? slot : row) * sizeof(T3)));
        }
        public readonly Ref<T1> _p1 => C0;
        public readonly Ref<T2> _p2 => C1;
        public readonly Ref<T3> _p3 => C2;
        public readonly Ref<T1> C0 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Ref<T1>(p0); }
        public readonly Ref<T2> C1 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Ref<T2>(UsesGather ? p1 : (T2*)((byte*)p0 + (long)p1)); }
        public readonly Ref<T3> C2 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Ref<T3>(UsesGather ? p2 : (T3*)((byte*)p0 + (long)p2)); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Deconstruct(out Ref<T1> c0, out Ref<T2> c1, out Ref<T3> c2)
        {
            c0.data = p0;
            if (UsesGather) { c1.data = p1; c2.data = p2; }
            else
            {
                c1.data = (T2*)((byte*)p0 + (long)p1);
                c2.data = (T3*)((byte*)p0 + (long)p2);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Deconstruct(out Ref<T1> c0, out Ref<T2> c1)
        {
            c0.data = p0;
            if (UsesGather) { c1.data = p1; }
            else
            {
                c1.data = (T2*)((byte*)p0 + (long)p1);
            }
        }
    }
    public unsafe ref struct QueryRuntimeIter3<TTuple> where TTuple : unmanaged, IQueryRuntimeTuple3
    {
        private TTuple current;
        private QueryRuntimeRows3 columns;
        private readonly World.WorldUnsafe* world;
        private readonly int* matches;
        private readonly int matchCount;
        private readonly bool storageMode;
        private readonly int mask;
        private int block, rowIndex, rowCount;
        private int* rows;
        private byte* end;
        // Range bookkeeping is used only when entering a block, never in the row hot path.
        private int skip, remaining;

        public QueryRuntimeIter3(QueryUnsafe* query) : this(query, 0, int.MaxValue, true) { }
        public QueryRuntimeIter3(QueryUnsafe* query, in Range range) : this(query, range.start, range.end, false) { }
        private QueryRuntimeIter3(QueryUnsafe* query, int start, int stop, bool allowStorage)
        {
            current = default; columns = default; world = query->world;
            mask = current.Init(ref columns, world);
            // Ranged jobs retain matching-archetype order, including shared physical storages.
            storageMode = allowStorage && mask == 0 && query->TryUseStorageIteration();
            var list = storageMode ? query->GetMatchingStorages() : query->matchingArchetypes;
            matches = list.Ptr; matchCount = list.Length;
            block = -1; rowIndex = rowCount = 0; rows = null; end = null;
            skip = Math.Max(0, start);
            remaining = stop > skip ? stop - skip : 0;
        }
        public readonly QueryRuntimeIter3<TTuple> GetEnumerator() => this;
        public readonly TTuple Current { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => current; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (!current.UsesPools()) return current.Advance(end) || MoveNextInlineSparse();
            if (rowIndex >= rowCount) return NextBlock();
            var row = rows != null ? rows[rowIndex] : rowIndex;
            current.Gather(ref columns, row);
            rowIndex++;
            return true;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool MoveNextInlineSparse()
        {
            if (rowIndex >= rowCount) return NextBlock();
            current.GatherInline(ref columns, rows != null ? rows[rowIndex] : rowIndex);
            rowIndex++;
            return true;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool NextBlock()
        {
            while (remaining > 0 && ++block < matchCount)
            {
                if (storageMode)
                {
                    ref var storage = ref world->storagesList.Ptr[matches[block]].Ref;
                    var count = storage.count;
                    if (count == 0) continue;
                    current.SetStorage(ref storage, 0);
                    end = current.GetEnd(count);
                    return true;
                }
                ref var arch = ref world->archetypesList.Ptr[matches[block]].Ref;
                var snapshotCount = arch.count;
                if (skip >= snapshotCount) { skip -= snapshotCount; continue; }
                var start = skip;
                var countInRange = Math.Min(snapshotCount - start, remaining);
                skip = 0;
                remaining -= countInRange;
                if (mask == 0 && arch.RowsAreDense)
                {
                    current.SetArchetype(ref arch, start);
                    end = current.GetEnd(countInRange);
                    return true;
                }
                columns.SetArchetype(ref arch);
                end = null;
                rows = arch.RowsAreDense ? null : arch.rows.Ptr;
                rowCount = start + countInRange; rowIndex = start + 1;
                current.Gather(ref columns, rows != null ? rows[start] : start);
                return true;
            }
            end = null; rowIndex = rowCount;
            return false;
        }
    }
}

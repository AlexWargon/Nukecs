using System;
using System.Runtime.CompilerServices;

namespace Wargon.Nukecs
{
    // Arity 5; the traversal and tuple representation follow the measured four-component candidate.
    public unsafe struct QueryRuntimeRows5
    {
        public QueryRuntimeColumn C0, C1, C2, C3, C4;
        public int* Entities;
        public int PageIndex;
        public void SetArchetype(ref ArchetypeUnsafe arch)
        {
            C0.SetArchetype(ref arch);
            C1.SetArchetype(ref arch);
            C2.SetArchetype(ref arch);
            C3.SetArchetype(ref arch);
            C4.SetArchetype(ref arch);
            Entities = arch.packedEntities.Ptr;
            PageIndex = -1;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void LoadPage(int page)
        {
            if (C0.Pool != null) C0.Base = C0.Page(page);
            if (C1.Pool != null) C1.Base = C1.Page(page);
            if (C2.Pool != null) C2.Base = C2.Page(page);
            if (C3.Pool != null) C3.Base = C3.Page(page);
            if (C4.Pool != null) C4.Base = C4.Page(page);
            PageIndex = page;
        }
    }
    public unsafe interface IQueryRuntimeTuple5
    {
        int Init(ref QueryRuntimeRows5 columns, World.WorldUnsafe* world);
        bool UsesPools();
        void SetStorage(ref StorageArchetype storage, int start);
        void SetArchetype(ref ArchetypeUnsafe arch, int start);
        bool Advance(byte* end);
        byte* GetEnd(int count);
        void GatherInline(ref QueryRuntimeRows5 columns, int row);
        void Gather(ref QueryRuntimeRows5 columns, int row);
    }
    public unsafe struct QueryRuntimeRefs<T1, T2, T3, T4, T5> : IQueryRuntimeTuple5
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged where T4 : unmanaged where T5 : unmanaged
    {
        private static readonly bool Pool0 = QueryRuntimeSlot<T1>.IsPool;
        private static readonly bool Pool1 = QueryRuntimeSlot<T2>.IsPool;
        private static readonly bool Pool2 = QueryRuntimeSlot<T3>.IsPool;
        private static readonly bool Pool3 = QueryRuntimeSlot<T4>.IsPool;
        private static readonly bool Pool4 = QueryRuntimeSlot<T5>.IsPool;
        private static readonly bool HasPools = Pool0 | Pool1 | Pool2 | Pool3 | Pool4;
        private static readonly bool HasSpecial = QueryRuntimeSlot<T1>.IsSpecial | QueryRuntimeSlot<T2>.IsEntity | QueryRuntimeSlot<T3>.IsEntity | QueryRuntimeSlot<T4>.IsEntity | QueryRuntimeSlot<T5>.IsEntity;
        internal static readonly bool UsesGather = HasPools | HasSpecial;
        // Inline: one base plus relative offsets. Mixed/pool: absolute addresses.
        internal T1* p0;
        internal T2* p1;
        internal T3* p2;
        internal T4* p3;
        internal T5* p4;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool UsesPools() => UsesGather;
        public int Init(ref QueryRuntimeRows5 columns, World.WorldUnsafe* world)
        {
            var mask = (columns.C0.Init<T1>(world) ? 1 : 0) | (columns.C1.Init<T2>(world) ? 2 : 0) | (columns.C2.Init<T3>(world) ? 4 : 0) | (columns.C3.Init<T4>(world) ? 8 : 0) | (columns.C4.Init<T5>(world) ? 16 : 0);
            if (mask != ((Pool0 ? 1 : 0) | (Pool1 ? 2 : 0) | (Pool2 ? 4 : 0) | (Pool3 ? 8 : 0) | (Pool4 ? 16 : 0)))
                throw new InvalidOperationException("Component storage category changed after tuple initialization.");
            return mask | (HasSpecial ? 512 : 0);
        }
        public void SetStorage(ref StorageArchetype storage, int start)
        {
            Set((QueryRuntimeSlot<T1>.IsSpecial ? (byte*)TagSlotStub<T1>.GetPtr() : storage.data.Ptr + storage.GetComponentOffset(storage.GetComponentLocalIndex(ComponentType<T1>.Index)) + start * sizeof(T1)), (QueryRuntimeSlot<T2>.IsSpecial ? (byte*)TagSlotStub<T2>.GetPtr() : storage.data.Ptr + storage.GetComponentOffset(storage.GetComponentLocalIndex(ComponentType<T2>.Index)) + start * sizeof(T2)), (QueryRuntimeSlot<T3>.IsSpecial ? (byte*)TagSlotStub<T3>.GetPtr() : storage.data.Ptr + storage.GetComponentOffset(storage.GetComponentLocalIndex(ComponentType<T3>.Index)) + start * sizeof(T3)), (QueryRuntimeSlot<T4>.IsSpecial ? (byte*)TagSlotStub<T4>.GetPtr() : storage.data.Ptr + storage.GetComponentOffset(storage.GetComponentLocalIndex(ComponentType<T4>.Index)) + start * sizeof(T4)), (QueryRuntimeSlot<T5>.IsSpecial ? (byte*)TagSlotStub<T5>.GetPtr() : storage.data.Ptr + storage.GetComponentOffset(storage.GetComponentLocalIndex(ComponentType<T5>.Index)) + start * sizeof(T5)));
        }
        public void SetArchetype(ref ArchetypeUnsafe arch, int start)
        {
            Set((QueryRuntimeSlot<T1>.IsSpecial ? (byte*)TagSlotStub<T1>.GetPtr() : arch.data.Ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T1>.Index)) + start * sizeof(T1)), (QueryRuntimeSlot<T2>.IsSpecial ? (byte*)TagSlotStub<T2>.GetPtr() : arch.data.Ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T2>.Index)) + start * sizeof(T2)), (QueryRuntimeSlot<T3>.IsSpecial ? (byte*)TagSlotStub<T3>.GetPtr() : arch.data.Ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T3>.Index)) + start * sizeof(T3)), (QueryRuntimeSlot<T4>.IsSpecial ? (byte*)TagSlotStub<T4>.GetPtr() : arch.data.Ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T4>.Index)) + start * sizeof(T4)), (QueryRuntimeSlot<T5>.IsSpecial ? (byte*)TagSlotStub<T5>.GetPtr() : arch.data.Ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T5>.Index)) + start * sizeof(T5)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetEnd(int count) => (byte*)(p0 + count);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Advance(byte* end)
        {
            p0++;
            if (UsesGather) { p1++; p2++; p3++; p4++; }
            else
            {
                if (QueryRuntimeSlot<T2>.IsSpecial) p1 = (T2*)((byte*)p1 - sizeof(T1));
                else if (sizeof(T2) != sizeof(T1)) p1 = (T2*)((byte*)p1 + sizeof(T2) - sizeof(T1));
                if (QueryRuntimeSlot<T3>.IsSpecial) p2 = (T3*)((byte*)p2 - sizeof(T1));
                else if (sizeof(T3) != sizeof(T1)) p2 = (T3*)((byte*)p2 + sizeof(T3) - sizeof(T1));
                if (QueryRuntimeSlot<T4>.IsSpecial) p3 = (T4*)((byte*)p3 - sizeof(T1));
                else if (sizeof(T4) != sizeof(T1)) p3 = (T4*)((byte*)p3 + sizeof(T4) - sizeof(T1));
                if (QueryRuntimeSlot<T5>.IsSpecial) p4 = (T5*)((byte*)p4 - sizeof(T1));
                else if (sizeof(T5) != sizeof(T1)) p4 = (T5*)((byte*)p4 + sizeof(T5) - sizeof(T1));
            }
            return (byte*)p0 < end;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Set(byte* a0, byte* a1, byte* a2, byte* a3, byte* a4)
        {
            p0 = (T1*)a0;
            if (UsesGather)
            {
                p1 = (T2*)a1;
                p2 = (T3*)a2;
                p3 = (T4*)a3;
                p4 = (T5*)a4;
            }
            else
            {
                p1 = (T2*)(a1 - a0);
                p2 = (T3*)(a2 - a0);
                p3 = (T4*)(a3 - a0);
                p4 = (T5*)(a4 - a0);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GatherInline(ref QueryRuntimeRows5 columns, int row)
        {
            Set((QueryRuntimeSlot<T1>.IsSpecial ? columns.C0.Base : columns.C0.Base + row * sizeof(T1)), (QueryRuntimeSlot<T2>.IsSpecial ? columns.C1.Base : columns.C1.Base + row * sizeof(T2)), (QueryRuntimeSlot<T3>.IsSpecial ? columns.C2.Base : columns.C2.Base + row * sizeof(T3)), (QueryRuntimeSlot<T4>.IsSpecial ? columns.C3.Base : columns.C3.Base + row * sizeof(T4)), (QueryRuntimeSlot<T5>.IsSpecial ? columns.C4.Base : columns.C4.Base + row * sizeof(T5)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Gather(ref QueryRuntimeRows5 columns, int row)
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
            Set((QueryRuntimeSlot<T1>.IsSpecial ? columns.C0.SpecialAddress<T1>(entity) : columns.C0.Base + (Pool0 ? slot : row) * sizeof(T1)), (QueryRuntimeSlot<T2>.IsSpecial ? columns.C1.SpecialAddress<T2>(entity) : columns.C1.Base + (Pool1 ? slot : row) * sizeof(T2)), (QueryRuntimeSlot<T3>.IsSpecial ? columns.C2.SpecialAddress<T3>(entity) : columns.C2.Base + (Pool2 ? slot : row) * sizeof(T3)), (QueryRuntimeSlot<T4>.IsSpecial ? columns.C3.SpecialAddress<T4>(entity) : columns.C3.Base + (Pool3 ? slot : row) * sizeof(T4)), (QueryRuntimeSlot<T5>.IsSpecial ? columns.C4.SpecialAddress<T5>(entity) : columns.C4.Base + (Pool4 ? slot : row) * sizeof(T5)));
        }
        public readonly Ref<T1> _p1 => C0;
        public readonly Ref<T2> _p2 => C1;
        public readonly Ref<T3> _p3 => C2;
        public readonly Ref<T4> _p4 => C3;
        public readonly Ref<T5> _p5 => C4;
        public readonly Ref<T1> C0 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Ref<T1>(p0); }
        public readonly Ref<T2> C1 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Ref<T2>(UsesGather ? p1 : (T2*)((byte*)p0 + (long)p1)); }
        public readonly Ref<T3> C2 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Ref<T3>(UsesGather ? p2 : (T3*)((byte*)p0 + (long)p2)); }
        public readonly Ref<T4> C3 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Ref<T4>(UsesGather ? p3 : (T4*)((byte*)p0 + (long)p3)); }
        public readonly Ref<T5> C4 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Ref<T5>(UsesGather ? p4 : (T5*)((byte*)p0 + (long)p4)); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly void DeconstructRefs(out Ref<T1> c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3, out Ref<T5> c4)
        {
            c0.data = p0;
            if (UsesGather) { c1.data = p1; c2.data = p2; c3.data = p3; c4.data = p4; }
            else
            {
                c1.data = (T2*)((byte*)p0 + (long)p1);
                c2.data = (T3*)((byte*)p0 + (long)p2);
                c3.data = (T4*)((byte*)p0 + (long)p3);
                c4.data = (T5*)((byte*)p0 + (long)p4);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly void DeconstructRefs(out Ref<T1> c0, out Ref<T2> c1, out Ref<T3> c2, out Ref<T4> c3)
        {
            c0.data = p0;
            if (UsesGather) { c1.data = p1; c2.data = p2; c3.data = p3; }
            else
            {
                c1.data = (T2*)((byte*)p0 + (long)p1);
                c2.data = (T3*)((byte*)p0 + (long)p2);
                c3.data = (T4*)((byte*)p0 + (long)p3);
            }
        }
    }
    public unsafe ref struct QueryRuntimeIter5<TTuple> where TTuple : unmanaged, IQueryRuntimeTuple5
    {
        private TTuple current;
        private QueryRuntimeRows5 columns;
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

        public QueryRuntimeIter5(QueryUnsafe* query) : this(query, 0, int.MaxValue, true) { }
        public QueryRuntimeIter5(QueryUnsafe* query, in Range range) : this(query, range.start, range.end, false) { }
        private QueryRuntimeIter5(QueryUnsafe* query, int start, int stop, bool allowStorage)
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryRuntimeIter5<TTuple> GetEnumerator() => this;
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

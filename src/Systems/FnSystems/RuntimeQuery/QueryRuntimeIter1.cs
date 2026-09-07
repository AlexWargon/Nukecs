using System;
using System.Runtime.CompilerServices;

namespace Wargon.Nukecs
{
    // Arity 1; the traversal and tuple representation follow the measured four-component candidate.
    public unsafe struct QueryRuntimeRows1
    {
        public QueryRuntimeColumn C0;
        public int* Entities;
        public int PageIndex;
        public void SetArchetype(ref ArchetypeUnsafe arch)
        {
            C0.SetArchetype(ref arch);
            Entities = arch.packedEntities.Ptr;
            PageIndex = -1;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void LoadPage(int page)
        {
            if (C0.Pool != null) C0.Base = C0.Page(page);
            PageIndex = page;
        }
    }
    public unsafe interface IQueryRuntimeTuple1
    {
        int Init(ref QueryRuntimeRows1 columns, World.WorldUnsafe* world);
        bool UsesPools();
        void SetStorage(ref StorageArchetype storage, int start);
        void SetArchetype(ref ArchetypeUnsafe arch, int start);
        bool Advance(byte* end);
        byte* GetEnd(int count);
        void GatherInline(ref QueryRuntimeRows1 columns, int row);
        void Gather(ref QueryRuntimeRows1 columns, int row);
    }
    public unsafe struct QueryRuntimeRefs<T1> : IQueryRuntimeTuple1
        where T1 : unmanaged
    {
        private static readonly bool Pool0 = QueryRuntimeSlot<T1>.IsPool;
        private static readonly bool HasPools = Pool0;
        private static readonly bool HasSpecial = QueryRuntimeSlot<T1>.IsSpecial;
        private static readonly bool UsesGather = HasPools | HasSpecial;
        // Inline: one base plus relative offsets. Mixed/pool: absolute addresses.
        private T1* p0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool UsesPools() => UsesGather;
        public int Init(ref QueryRuntimeRows1 columns, World.WorldUnsafe* world)
        {
            var mask = (columns.C0.Init<T1>(world) ? 1 : 0);
            if (mask != ((Pool0 ? 1 : 0)))
                throw new InvalidOperationException("Component storage category changed after tuple initialization.");
            return mask | (HasSpecial ? 512 : 0);
        }
        public void SetStorage(ref StorageArchetype storage, int start)
        {
            Set((QueryRuntimeSlot<T1>.IsSpecial ? (byte*)TagSlotStub<T1>.GetPtr() : storage.data.Ptr + storage.GetComponentOffset(storage.GetComponentLocalIndex(ComponentType<T1>.Index)) + start * sizeof(T1)));
        }
        public void SetArchetype(ref ArchetypeUnsafe arch, int start)
        {
            Set((QueryRuntimeSlot<T1>.IsSpecial ? (byte*)TagSlotStub<T1>.GetPtr() : arch.data.Ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T1>.Index)) + start * sizeof(T1)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetEnd(int count) => (byte*)(p0 + count);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Advance(byte* end)
        {
            p0++;
            return (byte*)p0 < end;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Set(byte* a0)
        {
            p0 = (T1*)a0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GatherInline(ref QueryRuntimeRows1 columns, int row)
        {
            Set((QueryRuntimeSlot<T1>.IsSpecial ? columns.C0.Base : columns.C0.Base + row * sizeof(T1)));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Gather(ref QueryRuntimeRows1 columns, int row)
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
            Set((QueryRuntimeSlot<T1>.IsSpecial ? columns.C0.SpecialAddress<T1>(entity) : columns.C0.Base + (Pool0 ? slot : row) * sizeof(T1)));
        }
        public readonly Ref<T1> _p1 => C0;
        public readonly Ref<T1> C0 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => new Ref<T1>(p0); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Deconstruct(out Ref<T1> c0)
        {
            c0.data = p0;
        }
    }
    public unsafe ref struct QueryRuntimeIter1<TTuple> where TTuple : unmanaged, IQueryRuntimeTuple1
    {
        private TTuple current;
        private QueryRuntimeRows1 columns;
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

        public QueryRuntimeIter1(QueryUnsafe* query) : this(query, 0, int.MaxValue, true) { }
        public QueryRuntimeIter1(QueryUnsafe* query, in Range range) : this(query, range.start, range.end, false) { }
        private QueryRuntimeIter1(QueryUnsafe* query, int start, int stop, bool allowStorage)
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
        public readonly QueryRuntimeIter1<TTuple> GetEnumerator() => this;
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

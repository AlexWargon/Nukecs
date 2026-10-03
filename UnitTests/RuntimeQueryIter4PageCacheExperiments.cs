using System;
using System.Runtime.CompilerServices;

namespace Wargon.Nukecs.Tests
{
    // Four-component experiments only. Existing compact/paged controls are unchanged.
    public static unsafe class RuntimeQueryIter4PageCacheApi
    {
        public static RuntimePageIter<RuntimeOnePoolRefs<T1,T2,T3,T4>> iter_cached_aaap_runtime<T1,T2,T3,T4>(this in Query<T1,T2,T3,T4> query)
            where T1 : unmanaged,IComponent where T2 : unmanaged,IComponent where T3 : unmanaged,IComponent where T4 : unmanaged,IComponent
        {
            query.TryGetQuery(out var raw);
            return new RuntimePageIter<RuntimeOnePoolRefs<T1,T2,T3,T4>>(raw.Ptr);
        }
        public static RuntimePageIter<RuntimeTwoPoolsRefs<T1,T2,T3,T4>> iter_cached_apap_runtime<T1,T2,T3,T4>(this in Query<T1,T2,T3,T4> query)
            where T1 : unmanaged,IComponent where T2 : unmanaged,IComponent where T3 : unmanaged,IComponent where T4 : unmanaged,IComponent
        {
            query.TryGetQuery(out var raw);
            return new RuntimePageIter<RuntimeTwoPoolsRefs<T1,T2,T3,T4>>(raw.Ptr);
        }
        public static RuntimePageIter<RuntimeDirectRefs<T1,T2,T3,T4>> iter_direct_pool_runtime<T1,T2,T3,T4>(this in Query<T1,T2,T3,T4> query)
        where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent
        {
            query.TryGetQuery(out var raw);
            return new RuntimePageIter<RuntimeDirectRefs<T1,T2,T3,T4>>(raw.Ptr);
        }
        public static RuntimePageIter<RuntimeDirectRefs<T1,T2,T3,T4>> iter_direct_pool_runtime<T1,T2,T3,T4,TFilter>(this in Query<T1,T2,T3,T4,TFilter> query)
        where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where TFilter : unmanaged
        {
            if (QueryParamInfo<TFilter>.IsComponent && ComponentType<TFilter>.Data.category != ComponentCategory.Tag)
                throw new InvalidOperationException("The fifth parameter must be a filter or tag.");
            query.TryGetQuery(out var raw);
            return new RuntimePageIter<RuntimeDirectRefs<T1,T2,T3,T4>>(raw.Ptr);
        }
        public static RuntimePageIter<RuntimeCachedRefs<T1,T2,T3,T4>> iter_cached_runtime<T1,T2,T3,T4>(this in Query<T1,T2,T3,T4> query)
        where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent
        {
            query.TryGetQuery(out var raw);
            return new RuntimePageIter<RuntimeCachedRefs<T1,T2,T3,T4>>(raw.Ptr);
        }
        public static RuntimePageIter<RuntimeCachedRefs<T1,T2,T3,T4>> iter_cached_runtime<T1,T2,T3,T4,TFilter>(this in Query<T1,T2,T3,T4,TFilter> query)
        where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where TFilter : unmanaged
        {
            if (QueryParamInfo<TFilter>.IsComponent && ComponentType<TFilter>.Data.category != ComponentCategory.Tag)
                throw new InvalidOperationException("The fifth parameter must be a filter or tag.");
            query.TryGetQuery(out var raw);
            return new RuntimePageIter<RuntimeCachedRefs<T1,T2,T3,T4>>(raw.Ptr);
        }
        public static RuntimePageIter<RuntimeAllPoolRefs<T1,T2,T3,T4>> iter_cached_allpool_runtime<T1,T2,T3,T4>(this in Query<T1,T2,T3,T4> query)
        where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent
        {
            query.TryGetQuery(out var raw);
            return new RuntimePageIter<RuntimeAllPoolRefs<T1,T2,T3,T4>>(raw.Ptr);
        }
        public static RuntimePageIter<RuntimeAllPoolRefs<T1,T2,T3,T4>> iter_cached_allpool_runtime<T1,T2,T3,T4,TFilter>(this in Query<T1,T2,T3,T4,TFilter> query)
        where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent where TFilter : unmanaged
        {
            if (QueryParamInfo<TFilter>.IsComponent && ComponentType<TFilter>.Data.category != ComponentCategory.Tag)
                throw new InvalidOperationException("The fifth parameter must be a filter or tag.");
            query.TryGetQuery(out var raw);
            return new RuntimePageIter<RuntimeAllPoolRefs<T1,T2,T3,T4>>(raw.Ptr);
        }
    }
    public unsafe struct RuntimePageColumn
    {
        public ComponentPoolUntyped* Pool;
        public byte* Base;
        private int typeIndex;
        public bool Init<T>(World.WorldUnsafe* world) where T : unmanaged, IComponent
        {
            var type = ComponentType<T>.Data;
            if (type.category == ComponentCategory.Tag) throw new InvalidOperationException("Tags are filters only.");
            typeIndex = ComponentType<T>.Index;
            Pool = type.category == ComponentCategory.Pool ? world->GetUntypedPoolPtr(typeIndex)->UnsafeBuffer : null;
            return Pool != null;
        }
        public void SetArchetype(ref ArchetypeUnsafe arch)
        {
            if (Pool == null) Base = arch.data.Ptr + arch.GetComponentOffset(arch.GetComponentLocalIndex(typeIndex));
        }
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
    public unsafe struct RuntimePageRows
    {
        public RuntimePageColumn A, B, C, D;
        public int* Entities;
        public int PageIndex;
        public bool HasPools;
        public void SetArchetype(ref ArchetypeUnsafe arch)
        {
            A.SetArchetype(ref arch); B.SetArchetype(ref arch); C.SetArchetype(ref arch); D.SetArchetype(ref arch);
            Entities = arch.packedEntities.Ptr;
            PageIndex = -1;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void LoadPage(int page)
        {
            // Cache component buffers, never the relocatable page-table pointer.
            if (A.Pool != null) A.Base = A.Page(page);
            if (B.Pool != null) B.Base = B.Page(page);
            if (C.Pool != null) C.Base = C.Page(page);
            if (D.Pool != null) D.Base = D.Page(page);
            PageIndex = page;
        }
    }
    // Prototype of a source-generated storage-mask specialization (8).
    public unsafe struct RuntimeOnePoolRefs<T1,T2,T3,T4> : IRuntimePageTuple
        where T1 : unmanaged,IComponent where T2 : unmanaged,IComponent where T3 : unmanaged,IComponent where T4 : unmanaged,IComponent
    {
        private T1* a; private T2* b; private T3* c; private T4* d;
        public void Init(ref RuntimePageRows rows, World.WorldUnsafe* world)
        {
            rows.HasPools = rows.A.Init<T1>(world) | rows.B.Init<T2>(world) | rows.C.Init<T3>(world) | rows.D.Init<T4>(world);
            if (rows.A.Pool != null || rows.B.Pool != null || rows.C.Pool != null || rows.D.Pool == null)
                throw new InvalidOperationException("Unexpected storage combination for this prototype.");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Gather(ref RuntimePageRows rows, int row)
        {
            var entity=rows.Entities[row];
            var page=entity >> Chunk.CHUNK_INDEX_BITSFIFT;
            var slot=entity & (Chunk.MAX_CHUNK_SIZE-1);
            if (page!=rows.PageIndex) rows.LoadPage(page);
            a=(T1*)rows.A.Base + row;
            b=(T2*)rows.B.Base + row;
            c=(T3*)rows.C.Base + row;
            d=(T4*)rows.D.Base + slot;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Deconstruct(out Ref<T1> p1,out Ref<T2> p2,out Ref<T3> p3,out Ref<T4> p4)
        { p1.data=a; p2.data=b; p3.data=c; p4.data=d; }
    }
    // Prototype of a source-generated storage-mask specialization (10).
    public unsafe struct RuntimeTwoPoolsRefs<T1,T2,T3,T4> : IRuntimePageTuple
        where T1 : unmanaged,IComponent where T2 : unmanaged,IComponent where T3 : unmanaged,IComponent where T4 : unmanaged,IComponent
    {
        private T1* a; private T2* b; private T3* c; private T4* d;
        public void Init(ref RuntimePageRows rows, World.WorldUnsafe* world)
        {
            rows.HasPools = rows.A.Init<T1>(world) | rows.B.Init<T2>(world) | rows.C.Init<T3>(world) | rows.D.Init<T4>(world);
            if (rows.A.Pool != null || rows.B.Pool == null || rows.C.Pool != null || rows.D.Pool == null)
                throw new InvalidOperationException("Unexpected storage combination for this prototype.");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Gather(ref RuntimePageRows rows, int row)
        {
            var entity=rows.Entities[row];
            var page=entity >> Chunk.CHUNK_INDEX_BITSFIFT;
            var slot=entity & (Chunk.MAX_CHUNK_SIZE-1);
            if (page!=rows.PageIndex) rows.LoadPage(page);
            a=(T1*)rows.A.Base + row;
            b=(T2*)rows.B.Base + slot;
            c=(T3*)rows.C.Base + row;
            d=(T4*)rows.D.Base + slot;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Deconstruct(out Ref<T1> p1,out Ref<T2> p2,out Ref<T3> p3,out Ref<T4> p4)
        { p1.data=a; p2.data=b; p3.data=c; p4.data=d; }
    }
    public unsafe interface IRuntimePageTuple
    {
        void Init(ref RuntimePageRows rows, World.WorldUnsafe* world);
        void Gather(ref RuntimePageRows rows, int row);
    }
    public unsafe struct RuntimeDirectRefs<T1,T2,T3,T4> : IRuntimePageTuple
        where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent
    {
        private T1* a; private T2* b; private T3* c; private T4* d;
        public void Init(ref RuntimePageRows rows, World.WorldUnsafe* world)
        {
            rows.HasPools = rows.A.Init<T1>(world) | rows.B.Init<T2>(world) | rows.C.Init<T3>(world) | rows.D.Init<T4>(world);
            if (!rows.HasPools) throw new InvalidOperationException("This experiment requires a pool component.");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Gather(ref RuntimePageRows rows, int row)
        {
            var entity = rows.HasPools ? rows.Entities[row] : 0;
            var page = entity >> Chunk.CHUNK_INDEX_BITSFIFT;
            var slot = entity & (Chunk.MAX_CHUNK_SIZE - 1);
            a = rows.A.Pool == null ? (T1*)rows.A.Base + row : (T1*)rows.A.Page(page) + slot;
            b = rows.B.Pool == null ? (T2*)rows.B.Base + row : (T2*)rows.B.Page(page) + slot;
            c = rows.C.Pool == null ? (T3*)rows.C.Base + row : (T3*)rows.C.Page(page) + slot;
            d = rows.D.Pool == null ? (T4*)rows.D.Base + row : (T4*)rows.D.Page(page) + slot;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Deconstruct(out Ref<T1> p1, out Ref<T2> p2, out Ref<T3> p3, out Ref<T4> p4)
        { p1.data = a; p2.data = b; p3.data = c; p4.data = d; }
    }
    public unsafe struct RuntimeCachedRefs<T1,T2,T3,T4> : IRuntimePageTuple
        where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent
    {
        private T1* a; private T2* b; private T3* c; private T4* d;
        public void Init(ref RuntimePageRows rows, World.WorldUnsafe* world)
        {
            rows.HasPools = rows.A.Init<T1>(world) | rows.B.Init<T2>(world) | rows.C.Init<T3>(world) | rows.D.Init<T4>(world);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Gather(ref RuntimePageRows rows, int row)
        {
            var entity = rows.HasPools ? rows.Entities[row] : 0;
            var page = entity >> Chunk.CHUNK_INDEX_BITSFIFT;
            var slot = entity & (Chunk.MAX_CHUNK_SIZE - 1);
            if (page != rows.PageIndex) rows.LoadPage(page);
            a = rows.A.Pool == null ? (T1*)rows.A.Base + row : (T1*)rows.A.Base + slot;
            b = rows.B.Pool == null ? (T2*)rows.B.Base + row : (T2*)rows.B.Base + slot;
            c = rows.C.Pool == null ? (T3*)rows.C.Base + row : (T3*)rows.C.Base + slot;
            d = rows.D.Pool == null ? (T4*)rows.D.Base + row : (T4*)rows.D.Base + slot;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Deconstruct(out Ref<T1> p1, out Ref<T2> p2, out Ref<T3> p3, out Ref<T4> p4)
        { p1.data = a; p2.data = b; p3.data = c; p4.data = d; }
    }
    public unsafe struct RuntimeAllPoolRefs<T1,T2,T3,T4> : IRuntimePageTuple
        where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent where T4 : unmanaged, IComponent
    {
        private T1* a; private T2* b; private T3* c; private T4* d;
        public void Init(ref RuntimePageRows rows, World.WorldUnsafe* world)
        {
            rows.HasPools = rows.A.Init<T1>(world) | rows.B.Init<T2>(world) | rows.C.Init<T3>(world) | rows.D.Init<T4>(world);
            if (rows.A.Pool == null || rows.B.Pool == null || rows.C.Pool == null || rows.D.Pool == null)
                throw new InvalidOperationException("This experiment requires four pool components.");
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Gather(ref RuntimePageRows rows, int row)
        {
            var entity = rows.Entities[row];
            var page = entity >> Chunk.CHUNK_INDEX_BITSFIFT;
            var slot = entity & (Chunk.MAX_CHUNK_SIZE - 1);
            if (page != rows.PageIndex) rows.LoadPage(page);
            a = (T1*)rows.A.Base + slot;
            b = (T2*)rows.B.Base + slot;
            c = (T3*)rows.C.Base + slot;
            d = (T4*)rows.D.Base + slot;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Deconstruct(out Ref<T1> p1, out Ref<T2> p2, out Ref<T3> p3, out Ref<T4> p4)
        { p1.data = a; p2.data = b; p3.data = c; p4.data = d; }
    }
    public unsafe ref struct RuntimePageIter<TTuple> where TTuple : unmanaged, IRuntimePageTuple
    {
        private TTuple current;
        private RuntimePageRows columns;
        private readonly World.WorldUnsafe* world;
        private readonly int* matches;
        private readonly int matchCount;
        private int block;
        private int* rows;
        private int rowIndex, rowCount;
        public RuntimePageIter(QueryUnsafe* query)
        {
            current = default; columns = default;
            world = query->world;
            current.Init(ref columns, world);
            matches = query->matchingArchetypes.Ptr; matchCount = query->matchingArchetypes.Length;
            block = -1; rows = null; rowIndex = rowCount = 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RuntimePageIter<TTuple> GetEnumerator() => this;
        public readonly TTuple Current { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => current; }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            if (rowIndex >= rowCount) return NextArchetype();
            current.Gather(ref columns, rows != null ? rows[rowIndex] : rowIndex);
            rowIndex++;
            return true;
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool NextArchetype()
        {
            while (++block < matchCount)
            {
                ref var arch = ref world->archetypesList.Ptr[matches[block]].Ref;
                var count = arch.count;
                if (count == 0) continue;
                columns.SetArchetype(ref arch);
                rows = arch.RowsAreDense ? null : arch.rows.Ptr;
                rowCount = count; rowIndex = 1;
                current.Gather(ref columns, rows != null ? rows[0] : 0);
                return true;
            }
            return false;
        }
    }
}

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

// ReSharper disable InconsistentNaming

// ReSharper disable RedundantDiscardDesignation

// ReSharper disable SuspiciousTypeConversion.Global

namespace Wargon.Nukecs
{
    // ===========================================================================

    // Query<T1>

    // ===========================================================================
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Query<T1> : IQuery, ISystemParam
        where T1 : unmanaged, IComponent
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIterT1<T1> GetEnumerator()
        {
            return new QueryIterT1<T1>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        public ptr<QueryUnsafe> _query;
        internal int id;
        private Range _range;

        public override int GetHashCode()
        {
            unchecked { return typeof(T1).GetHashCode(); }
        }
        
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();
            _query.Ref.With(ComponentType<T1>.Index);
            id = _query.Ref.Id;
        }

        public void FixPointers(ref MemAllocator allocator)
        {
            _query.OnDeserialize(ref allocator);
        }

        public void SetQueryPtr(ptr<QueryUnsafe> q)
        {
            _query = q;
            id = q.Ref.Id;
        }

        public void Update(ref World world, IntPtr data)
        {
            _query = world.UnsafeWorldRef.queries.ElementAt(id);
            _range = *(Range*)data;
        }

        public IntPtr GetData()
        {
            return (IntPtr)UnsafeUtility.AddressOf(ref _range);
        }

        public bool TryGetQuery(out ptr<QueryUnsafe> query)
        {
            query = _query;
            return true;
        }

        public struct WithEntity : IQuery, ISystemParam
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public QueryParIterWithEntity<EntityRefTuple<T1>> GetEnumerator()
            {
                return new QueryParIterWithEntity<EntityRefTuple<T1>>(_range, in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            public readonly QueryChunkIter<Chunk<T1>> iter_chunk()
            {
                return new QueryChunkIter<Chunk<T1>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly QueryIterWithEntity<EntityPtrTuple<T1>> iter_unsafe()
            {
                return new QueryIterWithEntity<EntityPtrTuple<T1>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly QueryParIterWithEntity<EntityPtrTuple<T1>> par_iter_unsafe()
            {
                return new QueryParIterWithEntity<EntityPtrTuple<T1>>(in _range, in _query.Ref.matchingArchetypes,
                    _query.Ref.world);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly QueryParIterWithEntity<EntityRefTuple<T1>> par_iter()
            {
                return new QueryParIterWithEntity<EntityRefTuple<T1>>(in _range, in _query.Ref.matchingArchetypes,
                    _query.Ref.world);
            }

            public QueryIterWithEntity<EntityRefTuple<T1>> iter()
            {
                return new QueryIterWithEntity<EntityRefTuple<T1>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            public ptr<QueryUnsafe> _query;
            private Range _range;

            public override int GetHashCode()
            {
                unchecked { return typeof(T1).GetHashCode(); }
            }

            public SystemParamMetaType MetaType => SystemParamMetaType.Query;

            public int Count

            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _query.Ref.count;
            }

            public void Init(ref ptr<World.WorldUnsafe> world)
            {
                _query = world.Ref.CreateQueryPtr();
                _query.Ref.With(ComponentType<T1>.Index);
            }

            public void FixPointers(ref MemAllocator allocator)
            {
                _query.OnDeserialize(ref allocator);
            }

            public void SetQueryPtr(ptr<QueryUnsafe> q)
            {
                _query = q;
            }

            public void Update(ref World world, IntPtr data)
            {
                _range = *(Range*)(void*)data;
            }

            public IntPtr GetData()
            {
                return (IntPtr)UnsafeUtility.AddressOf(ref _range);
            }

            public bool TryGetQuery(out ptr<QueryUnsafe> query)
            {
                query = _query;
                return true;
            }
        }
    }

    // ===========================================================================

    // Query<T1, TOption>

    // ===========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Query<T1, TOption> : IQuery, ISystemParam
        where T1 : unmanaged, IComponent
        where TOption : unmanaged
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryParIter<RefTuple<T1, TOption>> GetEnumerator()
        {
            return new (_range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity First()
        {
            if (Count > 0)
            {
                var len = _query.Ref.matchingArchetypes.length;
                var ptr = _query.Ref.matchingArchetypes.Ptr;
                var arches = _query.Ref.world->archetypesList.Ptr;
                for (var i = 0; i < len; i++)
                {
                    ref var arch = ref arches[ptr[i]].Ref;
                    if (arches[ptr[i]].Ref.count > 0)
                        return ref _query.Ref.world->entities.Ptr[arch.packedEntities.Ptr[0]];
                }
            }

            throw new Exception("No entities found");
        }

        public readonly QueryChunkIter<Chunk<T1>> iter_chunk()
        {
            return new QueryChunkIter<Chunk<T1>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<PtrTuple<T1, TOption>> iter_unsafe()
        {
            return new QueryIter<PtrTuple<T1, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<PtrTuple<T1, TOption>> par_iter_unsafe()
        {
            return new QueryParIter<PtrTuple<T1, TOption>>(in _range, in _query.Ref.matchingArchetypes,
                _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<RefTuple<T1, TOption>> par_iter()
        {
            return new QueryParIter<RefTuple<T1, TOption>>(in _range, in _query.Ref.matchingArchetypes,
                _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<RefTuple<T1, TOption>> iter()
        {
            return new QueryIter<RefTuple<T1, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        public ptr<QueryUnsafe> _query;
        internal int id;
        private Range _range;
        public Range Range => _range;

        public override int GetHashCode()
        {
            unchecked { return typeof(T1).GetHashCode() * 397 ^ typeof(TOption).GetHashCode(); }
        }

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public int Count

        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

        public void Init(ref ptr<World.WorldUnsafe> world)

        {
            _query = world.Ref.CreateQueryPtr();
            _query.Ref.With(ComponentType<T1>.Index);
            id = _query.Ref.Id;
            TOption option = default;
            switch (option)
            {
                case IComponent _:
                    _query.Ref.With(ComponentType<TOption>.Index);
                    QueryParamInfo<TOption>.IsComponent = true;
                    break;
                case IFilter filter:
                    filter.Setup(_query.Ptr);
                    break;
                case ITuple tuple:
                    for (var i = 0; i < tuple.Length; i++)
                        if (tuple[i] is IFilter f)
                            f.Setup(_query.Ptr);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FixPointers(ref MemAllocator allocator)
        {
            _query.OnDeserialize(ref allocator);
        }

        public void SetQueryPtr(ptr<QueryUnsafe> q)
        {
            _query = q;
            id = q.Ref.Id;
        }

        public void Update(ref World world, IntPtr data)
        {
            _query = world.UnsafeWorldRef.queries.ElementAt(id);
            _range = *(Range*)(void*)data;
        }

        public IntPtr GetData()
        {
            return (IntPtr)UnsafeUtility.AddressOf(ref _range);
        }

        public bool TryGetQuery(out ptr<QueryUnsafe> query)
        {
            query = _query;
            return true;
        }

        public struct WithEntity : IQuery, ISystemParam
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public QueryParIterWithEntity<EntityRefTuple<T1, TOption>> GetEnumerator()
            {
                return new QueryParIterWithEntity<EntityRefTuple<T1, TOption>>(_range, in _query.Ref.matchingArchetypes,
                    _query.Ref.world);
            }

            public readonly QueryChunkIter<Chunk<T1>> iter_chunk()
            {
                return new QueryChunkIter<Chunk<T1>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly QueryIterWithEntity<EntityPtrTuple<T1, TOption>> iter_unsafe()
            {
                return new QueryIterWithEntity<EntityPtrTuple<T1, TOption>>(in _query.Ref.matchingArchetypes,
                    _query.Ref.world);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly QueryParIterWithEntity<EntityPtrTuple<T1, TOption>> par_iter_unsafe()
            {
                return new QueryParIterWithEntity<EntityPtrTuple<T1, TOption>>(in _range,
                    in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly QueryParIterWithEntity<EntityRefTuple<T1, TOption>> par_iter()
            {
                return new QueryParIterWithEntity<EntityRefTuple<T1, TOption>>(in _range,
                    in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly QueryIterWithEntity<EntityRefTuple<T1, TOption>> iter()
            {
                return new QueryIterWithEntity<EntityRefTuple<T1, TOption>>(in _query.Ref.matchingArchetypes,
                    _query.Ref.world);
            }

            public ptr<QueryUnsafe> _query;
            private Range _range;

            public override int GetHashCode()
            {
                unchecked { return typeof(T1).GetHashCode() * 397 ^ typeof(TOption).GetHashCode(); }
            }

            public void SetRange(Range range)
            {
                _range = range;
            }

            public SystemParamMetaType MetaType => SystemParamMetaType.Query;

            public int Count
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _query.Ref.count;
            }

            public void Init(ref ptr<World.WorldUnsafe> world)
            {
                _query = world.Ref.CreateQueryPtr();
                _query.Ref.With(ComponentType<T1>.Index);
                TOption option = default;
                switch (option)
                {
                    case IComponent _:
                        _query.Ref.With(ComponentType<TOption>.Index);
                        QueryParamInfo<TOption>.IsComponent = true;
                        break;

                    case IFilter filter:
                        filter.Setup(_query.Ptr);
                        break;

                    case ITuple tuple:
                        for (var i = 0; i < tuple.Length; i++)
                            if (tuple[i] is IFilter f)
                                f.Setup(_query.Ptr);
                        break;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [BurstCompile]
            public void FixPointers(ref MemAllocator allocator)
            {
                _query.OnDeserialize(ref allocator);
            }

            public void SetQueryPtr(ptr<QueryUnsafe> q)
            {
                _query = q;
            }

            public void Update(ref World world, IntPtr data)
            {
                _range = *(Range*)(void*)data;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public IntPtr GetData()
            {
                return (IntPtr)UnsafeStatic.as_ptr(ref _range);
            }

            public bool TryGetQuery(out ptr<QueryUnsafe> query)
            {
                query = _query;
                return true;
            }
        }
    }

    // ===========================================================================

    // Query<T1,T2, TOption>

    // ===========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Query<T1, T2, TOption> : IQuery, ISystemParam
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where TOption : unmanaged

    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryParIter<RefTuple<T1, T2, TOption>> GetEnumerator()
        {
            return new (_range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryChunkIter<Chunk<T1, T2>> iter_chunk()
        {
            return new QueryChunkIter<Chunk<T1, T2>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<PtrTuple<T1, T2, TOption>> iter_unsafe()
        {
            return new QueryIter<PtrTuple<T1, T2, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<PtrTuple<T1, T2, TOption>> par_iter_unsafe()
        {
            return new QueryParIter<PtrTuple<T1, T2, TOption>>(in _range, in _query.Ref.matchingArchetypes,
                _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<RefTuple<T1, T2, TOption>> iter()
        {
            return new QueryIter<RefTuple<T1, T2, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<RefTuple<T1, T2, TOption>> par_iter()
        {
            return new QueryParIter<RefTuple<T1, T2, TOption>>(in _range, in _query.Ref.matchingArchetypes,
                _query.Ref.world);
        }

        public ptr<QueryUnsafe> _query;
        internal int id;
        private Range _range;

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = typeof(T1).GetHashCode();
                hash = hash * 397 ^ typeof(T2).GetHashCode();
                hash = hash * 397 ^ typeof(TOption).GetHashCode();
                return hash;
            }
        }

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

        public void Init(ref ptr<World.WorldUnsafe> world)

        {
            _query = world.Ref.CreateQueryPtr();
            id = _query.Ref.Id;
            _query.Ref.With(ComponentType<T1>.Index);

            _query.Ref.With(ComponentType<T2>.Index);

            TOption option = default;

            switch (option)

            {
                case IComponent _:

                    _query.Ref.With(ComponentType<TOption>.Index);

                    QueryParamInfo<TOption>.IsComponent = true;

                    break;

                case IFilter filter:

                    filter.Setup(_query.Ptr);

                    break;

                case ITuple tuple:

                    for (var i = 0; i < tuple.Length; i++)

                        if (tuple[i] is IFilter f)
                            f.Setup(_query.Ptr);

                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FixPointers(ref MemAllocator allocator)
        {
            _query.OnDeserialize(ref allocator);
        }

        public void SetQueryPtr(ptr<QueryUnsafe> q)
        {
            _query = q;
            id = q.Ref.Id;
        }

        public void Update(ref World world, IntPtr data)
        {
            _query = world.UnsafeWorldRef.queries.ElementAt(id);
            _range = *(Range*)(void*)data;
        }

        public IntPtr GetData()
        {
            return (IntPtr)UnsafeUtility.AddressOf(ref _range);
        }

        public bool TryGetQuery(out ptr<QueryUnsafe> query)

        {
            query = _query;

            return true;
        }

        public struct WithEntity : IQuery, ISystemParam
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public QueryParIterWithEntity<EntityRefTuple<T1, T2, TOption>> GetEnumerator()
            {
                return new (_range, in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly QueryIterWithEntity<EntityPtrTuple<T1, T2, TOption>> iter_unsafe()
            {
                return new QueryIterWithEntity<EntityPtrTuple<T1, T2, TOption>>(in _query.Ref.matchingArchetypes,
                    _query.Ref.world);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly QueryParIterWithEntity<EntityPtrTuple<T1, T2, TOption>> par_iter_unsafe()
            {
                return new QueryParIterWithEntity<EntityPtrTuple<T1, T2, TOption>>(in _range,
                    in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly QueryIterWithEntity<EntityRefTuple<T1, T2, TOption>> iter()
            {
                return new QueryIterWithEntity<EntityRefTuple<T1, T2, TOption>>(in _query.Ref.matchingArchetypes,
                    _query.Ref.world);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly QueryParIterWithEntity<EntityRefTuple<T1, T2, TOption>> par_iter()
            {
                return new QueryParIterWithEntity<EntityRefTuple<T1, T2, TOption>>(in _range,
                    in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            public ptr<QueryUnsafe> _query;
            private Range _range;

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = typeof(T1).GetHashCode();
                    hash = hash * 397 ^ typeof(T2).GetHashCode();
                    hash = hash * 397 ^ typeof(TOption).GetHashCode();
                    return hash;
                }
            }

            public SystemParamMetaType MetaType => SystemParamMetaType.Query;

            public int Count

            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _query.Ref.count;
            }

            public void Init(ref ptr<World.WorldUnsafe> world)
            {
                _query = world.Ref.CreateQueryPtr();
                _query.Ref.With(ComponentType<T1>.Index);
                _query.Ref.With(ComponentType<T2>.Index);

                TOption option = default;

                switch (option)
                {
                    case IComponent _:
                        _query.Ref.With(ComponentType<TOption>.Index);
                        QueryParamInfo<TOption>.IsComponent = true;
                        break;
                    case IFilter filter:
                        filter.Setup(_query.Ptr);
                        break;
                    case ITuple tuple:
                        for (var i = 0; i < tuple.Length; i++)
                            if (tuple[i] is IFilter f)
                                f.Setup(_query.Ptr);
                        break;
                }
            }

            public void FixPointers(ref MemAllocator allocator)
            {
                _query.OnDeserialize(ref allocator);
            }

            public void SetQueryPtr(ptr<QueryUnsafe> q)
            {
                _query = q;
            }

            public void Update(ref World world, IntPtr data)
            {
                _range = *(Range*)(void*)data;
            }

            public IntPtr GetData()
            {
                return (IntPtr)UnsafeUtility.AddressOf(ref _range);
            }

            public bool TryGetQuery(out ptr<QueryUnsafe> query)

            {
                query = _query;

                return true;
            }
        }
    }

    // ===========================================================================

    // Query<T1..T3, TOption>

    // ===========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Query<T1, T2, T3, TOption> : IQuery, ISystemParam
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where TOption : unmanaged
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryParIter<RefTuple<T1, T2, T3, TOption>> GetEnumerator()
        {
            return new (_range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryChunkIter<Chunk<T1, T2, T3>> iter_chunk()
        {
            return new QueryChunkIter<Chunk<T1, T2, T3>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<PtrTuple<T1, T2, T3, TOption>> iter_unsafe()
        {
            return new QueryIter<PtrTuple<T1, T2, T3, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<PtrTuple<T1, T2, T3, TOption>> par_iter_unsafe()
        {
            return new QueryParIter<PtrTuple<T1, T2, T3, TOption>>(in _range, in _query.Ref.matchingArchetypes,
                _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<RefTuple<T1, T2, T3, TOption>> par_iter()
        {
            return new QueryParIter<RefTuple<T1, T2, T3, TOption>>(in _range, in _query.Ref.matchingArchetypes,
                _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<RefTuple<T1, T2, T3, TOption>> iter()
        {
            return new QueryIter<RefTuple<T1, T2, T3, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        public ptr<QueryUnsafe> _query;
        internal int id;
        private Range _range;

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = typeof(T1).GetHashCode();
                hash = hash * 397 ^ typeof(T2).GetHashCode();
                hash = hash * 397 ^ typeof(T3).GetHashCode();
                hash = hash * 397 ^ typeof(TOption).GetHashCode();
                return hash;
            }
        }

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public int Count

        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();
            _query.Ref.With(ComponentType<T1>.Index);
            _query.Ref.With(ComponentType<T2>.Index);
            _query.Ref.With(ComponentType<T3>.Index);
            id = _query.Ref.Id;
            TOption option = default;

            switch (option)
            {
                case IComponent _:
                    _query.Ref.With(ComponentType<TOption>.Index);
                    QueryParamInfo<TOption>.IsComponent = true;
                    break;

                case IFilter filter:
                    filter.Setup(_query.Ptr);
                    break;
                
                case ITuple tuple:
                    for (var i = 0; i < tuple.Length; i++)
                        if (tuple[i] is IFilter f)
                            f.Setup(_query.Ptr);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FixPointers(ref MemAllocator allocator)
        {
            _query.OnDeserialize(ref allocator);
        }

        public void SetQueryPtr(ptr<QueryUnsafe> q)
        {
            _query = q;
            id = q.Ref.Id;
        }

        public void Update(ref World world, IntPtr data)

        {
            _query = world.UnsafeWorldRef.queries.ElementAt(id);
            _range = *(Range*)(void*)data;
        }

        public IntPtr GetData()
        {
            return (IntPtr)UnsafeUtility.AddressOf(ref _range);
        }

        public bool TryGetQuery(out ptr<QueryUnsafe> query)
        {
            query = _query;
            return true;
        }

        public struct WithEntity : IQuery, ISystemParam

        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public QueryParIterWithEntity<EntityRefTuple<T1, T2, T3, TOption>> GetEnumerator()
            {
                return new (_range, in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly QueryIterWithEntity<EntityPtrTuple<T1, T2, T3, TOption>> iter_unsafe()
            {
                return new QueryIterWithEntity<EntityPtrTuple<T1, T2, T3, TOption>>(in _query.Ref.matchingArchetypes,
                    _query.Ref.world);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly QueryChunkIter<Chunk<T1, T2, T3>> iter_chunk()
            {
                return new QueryChunkIter<Chunk<T1, T2, T3>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly QueryParIterWithEntity<EntityPtrTuple<T1, T2, T3, TOption>> par_iter_unsafe()
            {
                return new QueryParIterWithEntity<EntityPtrTuple<T1, T2, T3, TOption>>(in _range,
                    in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly QueryParIterWithEntity<EntityRefTuple<T1, T2, T3, TOption>> par_iter()
            {
                return new QueryParIterWithEntity<EntityRefTuple<T1, T2, T3, TOption>>(in _range,
                    in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly QueryIterWithEntity<EntityRefTuple<T1, T2, T3, TOption>> iter()
            {
                return new QueryIterWithEntity<EntityRefTuple<T1, T2, T3, TOption>>(in _query.Ref.matchingArchetypes,
                    _query.Ref.world);
            }

            public ptr<QueryUnsafe> _query;
            private Range _range;

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = typeof(T1).GetHashCode();
                    hash = hash * 397 ^ typeof(T2).GetHashCode();
                    hash = hash * 397 ^ typeof(T3).GetHashCode();
                    hash = hash * 397 ^ typeof(TOption).GetHashCode();
                    return hash;
                }
            }

            public SystemParamMetaType MetaType => SystemParamMetaType.Query;

            public int Count
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _query.Ref.count;
            }

            public void Init(ref ptr<World.WorldUnsafe> world)
            {
                _query = world.Ref.CreateQueryPtr();

                _query.Ref.With(ComponentType<T1>.Index);

                _query.Ref.With(ComponentType<T2>.Index);

                _query.Ref.With(ComponentType<T3>.Index);

                TOption option = default;

                switch (option)
                {
                    case IComponent _:
                        _query.Ref.With(ComponentType<TOption>.Index);
                        QueryParamInfo<TOption>.IsComponent = true;
                        break;

                    case IFilter filter:
                        filter.Setup(_query.Ptr);
                        break;

                    case ITuple tuple:
                        for (var i = 0; i < tuple.Length; i++)
                            if (tuple[i] is IFilter f)
                                f.Setup(_query.Ptr);
                        break;
                }
            }

            public void FixPointers(ref MemAllocator allocator)
            {
                _query.OnDeserialize(ref allocator);
            }

            public void SetQueryPtr(ptr<QueryUnsafe> q)
            {
                _query = q;
            }

            public void Update(ref World world, IntPtr data)
            {
                _range = *(Range*)(void*)data;
            }

            public IntPtr GetData()
            {
                return (IntPtr)UnsafeUtility.AddressOf(ref _range);
            }

            public bool TryGetQuery(out ptr<QueryUnsafe> query)
            {
                query = _query;
                return true;
            }
        }
    }

    // ===========================================================================

    // Query<T1..T4, TOption>

    // ===========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Query<T1, T2, T3, T4, TOption> : IQuery, ISystemParam
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where TOption : unmanaged
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryParIter<RefTuple<T1, T2, T3, T4, TOption>> GetEnumerator()
        {
            return new (_range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryChunkIter<Chunk<T1, T2, T3, T4>> iter_chunk()
        {
            return new QueryChunkIter<Chunk<T1, T2, T3, T4>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<PtrTuple<T1, T2, T3, T4, TOption>> iter_unsafe()
        {
            return new QueryIter<PtrTuple<T1, T2, T3, T4, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<PtrTuple<T1, T2, T3, T4, TOption>> par_iter_unsafe()
        {
            return new QueryParIter<PtrTuple<T1, T2, T3, T4, TOption>>(in _range, in _query.Ref.matchingArchetypes,
                _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<RefTuple<T1, T2, T3, T4, TOption>> par_iter()
        {
            return new QueryParIter<RefTuple<T1, T2, T3, T4, TOption>>(in _range, in _query.Ref.matchingArchetypes,
                _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<RefTuple<T1, T2, T3, T4, TOption>> iter()
        {
            return new QueryIter<RefTuple<T1, T2, T3, T4, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        public ptr<QueryUnsafe> _query;
        internal int id;
        private Range _range;

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = typeof(T1).GetHashCode();
                hash = hash * 397 ^ typeof(T2).GetHashCode();
                hash = hash * 397 ^ typeof(T3).GetHashCode();
                hash = hash * 397 ^ typeof(T4).GetHashCode();
                hash = hash * 397 ^ typeof(TOption).GetHashCode();
                return hash;
            }
        }

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();
            _query.Ref.With(ComponentType<T1>.Index);
            _query.Ref.With(ComponentType<T2>.Index);
            _query.Ref.With(ComponentType<T3>.Index);
            _query.Ref.With(ComponentType<T4>.Index);

            id = _query.Ref.Id;
            TOption option = default;

            switch (option)
            {
                case IComponent _:
                    _query.Ref.With(ComponentType<TOption>.Index);
                    QueryParamInfo<TOption>.IsComponent = true;
                    break;

                case IFilter filter:
                    filter.Setup(_query.Ptr);
                    break;

                case ITuple tuple:
                    for (var i = 0; i < tuple.Length; i++)
                        if (tuple[i] is IFilter f)
                            f.Setup(_query.Ptr);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FixPointers(ref MemAllocator allocator)
        {
            _query.OnDeserialize(ref allocator);
        }

        public void SetQueryPtr(ptr<QueryUnsafe> q)
        {
            _query = q;
            id = q.Ref.Id;
        }

        public void Update(ref World world, IntPtr data)
        {
            _query = world.UnsafeWorldRef.queries.ElementAt(id);
            _range = *(Range*)(void*)data;
        }

        public IntPtr GetData()
        {
            return (IntPtr)UnsafeUtility.AddressOf(ref _range);
        }

        public bool TryGetQuery(out ptr<QueryUnsafe> query)
        {
            query = _query;
            return true;
        }

        public struct WithEntity : IQuery, ISystemParam
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public QueryParIterWithEntity<EntityRefTuple<T1, T2, T3, T4, TOption>> GetEnumerator()
            {
                return new (_range, in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly QueryChunkIter<Chunk<T1, T2, T3, T4>> iter_chunk()
            {
                return new QueryChunkIter<Chunk<T1, T2, T3, T4>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly QueryIterWithEntity<EntityPtrTuple<T1, T2, T3, T4, TOption>> iter_unsafe()
            {
                return new QueryIterWithEntity<EntityPtrTuple<T1, T2, T3, T4, TOption>>(
                    in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly QueryParIterWithEntity<EntityPtrTuple<T1, T2, T3, T4, TOption>> par_iter_unsafe()
            {
                return new QueryParIterWithEntity<EntityPtrTuple<T1, T2, T3, T4, TOption>>(in _range,
                    in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly QueryParIterWithEntity<EntityRefTuple<T1, T2, T3, T4, TOption>> par_iter()
            {
                return new QueryParIterWithEntity<EntityRefTuple<T1, T2, T3, T4, TOption>>(in _range,
                    in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly QueryIterWithEntity<EntityRefTuple<T1, T2, T3, T4, TOption>> iter()
            {
                return new QueryIterWithEntity<EntityRefTuple<T1, T2, T3, T4, TOption>>(
                    in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            public ptr<QueryUnsafe> _query;
            private Range _range;

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = typeof(T1).GetHashCode();
                    hash = hash * 397 ^ typeof(T2).GetHashCode();
                    hash = hash * 397 ^ typeof(T3).GetHashCode();
                    hash = hash * 397 ^ typeof(T4).GetHashCode();
                    hash = hash * 397 ^ typeof(TOption).GetHashCode();
                    return hash;
                }
            }

            public SystemParamMetaType MetaType => SystemParamMetaType.Query;

            public int Count
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _query.Ref.count;
            }

            public void Init(ref ptr<World.WorldUnsafe> world)
            {
                _query = world.Ref.CreateQueryPtr();
                _query.Ref.With(ComponentType<T1>.Index);
                _query.Ref.With(ComponentType<T2>.Index);
                _query.Ref.With(ComponentType<T3>.Index);
                _query.Ref.With(ComponentType<T4>.Index);

                TOption option = default;

                switch (option)
                {
                    case IComponent _:
                        _query.Ref.With(ComponentType<TOption>.Index);
                        QueryParamInfo<TOption>.IsComponent = true;
                        break;

                    case IFilter filter:
                        filter.Setup(_query.Ptr);
                        break;

                    case ITuple tuple:
                        for (var i = 0; i < tuple.Length; i++)
                            if (tuple[i] is IFilter f)
                                f.Setup(_query.Ptr);
                        break;
                }
            }

            public void FixPointers(ref MemAllocator allocator)
            {
                _query.OnDeserialize(ref allocator);
            }

            public void SetQueryPtr(ptr<QueryUnsafe> q)
            {
                _query = q;
            }

            public void Update(ref World world, IntPtr data)
            {
                _range = *(Range*)(void*)data;
            }

            public IntPtr GetData()
            {
                return (IntPtr)UnsafeUtility.AddressOf(ref _range);
            }

            public bool TryGetQuery(out ptr<QueryUnsafe> query)
            {
                query = _query;
                return true;
            }
        }
    }

    // ===========================================================================

    // Query<T1..T5, TOption>

    // ===========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Query<T1, T2, T3, T4, T5, TOption> : IQuery, ISystemParam
        where T1 : unmanaged
        where T2 : unmanaged
        where T3 : unmanaged
        where T4 : unmanaged
        where T5 : unmanaged
        where TOption : unmanaged

    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryParIter<RefTuple<T1, T2, T3, T4, T5, TOption>> GetEnumerator()
        {
            return new (_range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<RefTuple<T1, T2, T3, T4, T5, TOption>> par_iter()
        {
            return new QueryParIter<RefTuple<T1, T2, T3, T4, T5, TOption>>(in _range, in _query.Ref.matchingArchetypes,
                _query.Ref.world);
        }

        public ptr<QueryUnsafe> _query;
        internal int id;
        private Range _range;

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = typeof(T1).GetHashCode();
                hash = hash * 397 ^ typeof(T2).GetHashCode();
                hash = hash * 397 ^ typeof(T3).GetHashCode();
                hash = hash * 397 ^ typeof(T4).GetHashCode();
                hash = hash * 397 ^ typeof(T5).GetHashCode();
                hash = hash * 397 ^ typeof(TOption).GetHashCode();
                return hash;
            }
        }

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();
            _query.Ref.With(ComponentType<T1>.Index);
            _query.Ref.With(ComponentType<T2>.Index);
            _query.Ref.With(ComponentType<T3>.Index);
            _query.Ref.With(ComponentType<T4>.Index);
            _query.Ref.With(ComponentType<T5>.Index);

            id = _query.Ref.Id;
            TOption option = default;

            switch (option)
            {
                case IComponent _:
                    _query.Ref.With(ComponentType<TOption>.Index);
                    QueryParamInfo<TOption>.IsComponent = true;
                    break;

                case IFilter filter:
                    filter.Setup(_query.Ptr);
                    break;

                case ITuple tuple:
                    for (var i = 0; i < tuple.Length; i++)
                        if (tuple[i] is IFilter f)
                            f.Setup(_query.Ptr);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FixPointers(ref MemAllocator allocator)
        {
            _query.OnDeserialize(ref allocator);
        }

        public void SetQueryPtr(ptr<QueryUnsafe> q)
        {
            _query = q;
            id = q.Ref.Id;
        }

        public void Update(ref World world, IntPtr data)
        {
            _query = world.UnsafeWorldRef.queries.ElementAt(id);
            _range = *(Range*)(void*)data;
        }

        public IntPtr GetData()
        {
            return (IntPtr)UnsafeUtility.AddressOf(ref _range);
        }

        public bool TryGetQuery(out ptr<QueryUnsafe> query)
        {
            query = _query;
            return true;
        }

        public struct WithEntity : IQuery, ISystemParam
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public QueryParIterWithEntity<EntityRefTuple<T1, T2, T3, T4, T5, TOption>> GetEnumerator()
            {
                return new (_range, in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            public ptr<QueryUnsafe> _query;
            private Range _range;

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = typeof(T1).GetHashCode();
                    hash = hash * 397 ^ typeof(T2).GetHashCode();
                    hash = hash * 397 ^ typeof(T3).GetHashCode();
                    hash = hash * 397 ^ typeof(T4).GetHashCode();
                    hash = hash * 397 ^ typeof(T5).GetHashCode();
                    hash = hash * 397 ^ typeof(TOption).GetHashCode();
                    return hash;
                }
            }

            public SystemParamMetaType MetaType => SystemParamMetaType.Query;

            public int Count
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _query.Ref.count;
            }

            public void Init(ref ptr<World.WorldUnsafe> world)
            {
                _query = world.Ref.CreateQueryPtr();

                _query.Ref.With(ComponentType<T1>.Index);
                _query.Ref.With(ComponentType<T2>.Index);
                _query.Ref.With(ComponentType<T3>.Index);
                _query.Ref.With(ComponentType<T4>.Index);
                _query.Ref.With(ComponentType<T5>.Index);

                TOption option = default;

                switch (option)
                {
                    case IComponent _:
                        _query.Ref.With(ComponentType<TOption>.Index);
                        QueryParamInfo<TOption>.IsComponent = true;
                        break;

                    case IFilter filter:
                        filter.Setup(_query.Ptr);
                        break;

                    case ITuple tuple:
                        for (var i = 0; i < tuple.Length; i++)
                            if (tuple[i] is IFilter f)
                                f.Setup(_query.Ptr);
                        break;
                }
            }

            public void FixPointers(ref MemAllocator allocator)
            {
                _query.OnDeserialize(ref allocator);
            }

            public void SetQueryPtr(ptr<QueryUnsafe> q)
            {
                _query = q;
            }

            public void Update(ref World world, IntPtr data)
            {
                _range = *(Range*)(void*)data;
            }

            public IntPtr GetData()
            {
                return (IntPtr)UnsafeUtility.AddressOf(ref _range);
            }

            public bool TryGetQuery(out ptr<QueryUnsafe> query)
            {
                query = _query;
                return true;
            }
        }
    }

    // ===========================================================================

    // Query<T1..T6, TOption>

    // ===========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Query<T1, T2, T3, T4, T5, T6, TOption> : IQuery, ISystemParam
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged, IComponent
        where T6 : unmanaged, IComponent
        where TOption : unmanaged
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryParIter<RefTuple<T1, T2, T3, T4, T5, T6, TOption>> GetEnumerator()
        {
            return new (_range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        public ptr<QueryUnsafe> _query;
        internal int id;
        private Range _range;

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = typeof(T1).GetHashCode();
                hash = hash * 397 ^ typeof(T2).GetHashCode();
                hash = hash * 397 ^ typeof(T3).GetHashCode();
                hash = hash * 397 ^ typeof(T4).GetHashCode();
                hash = hash * 397 ^ typeof(T5).GetHashCode();
                hash = hash * 397 ^ typeof(T6).GetHashCode();
                hash = hash * 397 ^ typeof(TOption).GetHashCode();
                return hash;
            }
        }

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();

            _query.Ref.With(ComponentType<T1>.Index);
            _query.Ref.With(ComponentType<T2>.Index);
            _query.Ref.With(ComponentType<T3>.Index);
            _query.Ref.With(ComponentType<T4>.Index);
            _query.Ref.With(ComponentType<T5>.Index);
            _query.Ref.With(ComponentType<T6>.Index);

            id = _query.Ref.Id;
            TOption option = default;

            switch (option)
            {
                case IComponent _:
                    _query.Ref.With(ComponentType<TOption>.Index);
                    QueryParamInfo<TOption>.IsComponent = true;
                    break;

                case IFilter filter:
                    filter.Setup(_query.Ptr);
                    break;

                case ITuple tuple:
                    for (var i = 0; i < tuple.Length; i++)
                        if (tuple[i] is IFilter f)
                            f.Setup(_query.Ptr);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FixPointers(ref MemAllocator allocator)
        {
            _query.OnDeserialize(ref allocator);
        }

        public void SetQueryPtr(ptr<QueryUnsafe> q)
        {
            _query = q;
            id = q.Ref.Id;
        }

        public void Update(ref World world, IntPtr data)
        {
            _query = world.UnsafeWorldRef.queries.ElementAt(id);
            _range = *(Range*)(void*)data;
        }

        public IntPtr GetData()
        {
            return (IntPtr)UnsafeUtility.AddressOf(ref _range);
        }

        public bool TryGetQuery(out ptr<QueryUnsafe> query)
        {
            query = _query;
            return true;
        }

        public struct WithEntity : IQuery, ISystemParam
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public QueryParIterWithEntity<EntityRefTuple<T1, T2, T3, T4, T5, T6, TOption>> GetEnumerator()
            {
                return new (_range, in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            public ptr<QueryUnsafe> _query;
            private Range _range;

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = typeof(T1).GetHashCode();
                    hash = hash * 397 ^ typeof(T2).GetHashCode();
                    hash = hash * 397 ^ typeof(T3).GetHashCode();
                    hash = hash * 397 ^ typeof(T4).GetHashCode();
                    hash = hash * 397 ^ typeof(T5).GetHashCode();
                    hash = hash * 397 ^ typeof(T6).GetHashCode();
                    hash = hash * 397 ^ typeof(TOption).GetHashCode();
                    return hash;
                }
            }

            public SystemParamMetaType MetaType => SystemParamMetaType.Query;

            public int Count
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _query.Ref.count;
            }

            public void Init(ref ptr<World.WorldUnsafe> world)
            {
                _query = world.Ref.CreateQueryPtr();

                _query.Ref.With(ComponentType<T1>.Index);
                _query.Ref.With(ComponentType<T2>.Index);
                _query.Ref.With(ComponentType<T3>.Index);
                _query.Ref.With(ComponentType<T4>.Index);
                _query.Ref.With(ComponentType<T5>.Index);
                _query.Ref.With(ComponentType<T6>.Index);

                TOption option = default;

                switch (option)
                {
                    case IComponent _:
                        _query.Ref.With(ComponentType<TOption>.Index);
                        QueryParamInfo<TOption>.IsComponent = true;
                        break;

                    case IFilter filter:
                        filter.Setup(_query.Ptr);
                        break;

                    case ITuple tuple:
                        for (var i = 0; i < tuple.Length; i++)
                            if (tuple[i] is IFilter f)
                                f.Setup(_query.Ptr);
                        break;
                }
            }

            public void FixPointers(ref MemAllocator allocator)
            {
                _query.OnDeserialize(ref allocator);
            }

            public void SetQueryPtr(ptr<QueryUnsafe> q)
            {
                _query = q;
            }

            public void Update(ref World world, IntPtr data)
            {
                _range = *(Range*)(void*)data;
            }

            public IntPtr GetData()
            {
                return (IntPtr)UnsafeUtility.AddressOf(ref _range);
            }

            public bool TryGetQuery(out ptr<QueryUnsafe> query)
            {
                query = _query;
                return true;
            }
        }
    }

    // ===========================================================================

    // Query<T1..T7, TOption>

    // ===========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Query<T1, T2, T3, T4, T5, T6, T7, TOption> : IQuery, ISystemParam
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged, IComponent
        where T6 : unmanaged, IComponent
        where T7 : unmanaged, IComponent
        where TOption : unmanaged
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIter<RefTuple<T1, T2, T3, T4, T5, T6, T7, TOption>> GetEnumerator()
        {
            return new QueryIter<RefTuple<T1, T2, T3, T4, T5, T6, T7, TOption>>(in _query.Ref.matchingArchetypes,
                _query.Ref.world);
        }

        public ptr<QueryUnsafe> _query;
        internal int id;
        private Range _range;

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = typeof(T1).GetHashCode();
                hash = hash * 397 ^ typeof(T2).GetHashCode();
                hash = hash * 397 ^ typeof(T3).GetHashCode();
                hash = hash * 397 ^ typeof(T4).GetHashCode();
                hash = hash * 397 ^ typeof(T5).GetHashCode();
                hash = hash * 397 ^ typeof(T6).GetHashCode();
                hash = hash * 397 ^ typeof(T7).GetHashCode();
                hash = hash * 397 ^ typeof(TOption).GetHashCode();
                return hash;
            }
        }

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();

            _query.Ref.With(ComponentType<T1>.Index);
            _query.Ref.With(ComponentType<T2>.Index);
            _query.Ref.With(ComponentType<T3>.Index);
            _query.Ref.With(ComponentType<T4>.Index);
            _query.Ref.With(ComponentType<T5>.Index);
            _query.Ref.With(ComponentType<T6>.Index);
            _query.Ref.With(ComponentType<T7>.Index);

            id = _query.Ref.Id;
            TOption option = default;

            switch (option)
            {
                case IComponent _:
                    _query.Ref.With(ComponentType<TOption>.Index);
                    QueryParamInfo<TOption>.IsComponent = true;
                    break;

                case IFilter filter:
                    filter.Setup(_query.Ptr);
                    break;

                case ITuple tuple:
                    for (var i = 0; i < tuple.Length; i++)
                        if (tuple[i] is IFilter f)
                            f.Setup(_query.Ptr);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FixPointers(ref MemAllocator allocator)
        {
            _query.OnDeserialize(ref allocator);
        }

        public void SetQueryPtr(ptr<QueryUnsafe> q)
        {
            _query = q;
            id = q.Ref.Id;
        }

        public void Update(ref World world, IntPtr data)
        {
            _query = world.UnsafeWorldRef.queries.ElementAt(id);
            _range = *(Range*)(void*)data;
        }

        public IntPtr GetData()
        {
            return (IntPtr)UnsafeUtility.AddressOf(ref _range);
        }

        public bool TryGetQuery(out ptr<QueryUnsafe> query)
        {
            query = _query;
            return true;
        }

        public struct WithEntity : IQuery, ISystemParam
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public QueryIterWithEntity<EntityRefTuple<T1, T2, T3, T4, T5, T6, T7, TOption>> GetEnumerator()
            {
                return new QueryIterWithEntity<EntityRefTuple<T1, T2, T3, T4, T5, T6, T7, TOption>>(
                    in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            public ptr<QueryUnsafe> _query;
            private Range _range;

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = typeof(T1).GetHashCode();
                    hash = hash * 397 ^ typeof(T2).GetHashCode();
                    hash = hash * 397 ^ typeof(T3).GetHashCode();
                    hash = hash * 397 ^ typeof(T4).GetHashCode();
                    hash = hash * 397 ^ typeof(T5).GetHashCode();
                    hash = hash * 397 ^ typeof(T6).GetHashCode();
                    hash = hash * 397 ^ typeof(T7).GetHashCode();
                    hash = hash * 397 ^ typeof(TOption).GetHashCode();
                    return hash;
                }
            }

            public SystemParamMetaType MetaType => SystemParamMetaType.Query;

            public int Count
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _query.Ref.count;
            }

            public void Init(ref ptr<World.WorldUnsafe> world)
            {
                _query = world.Ref.CreateQueryPtr();

                _query.Ref.With(ComponentType<T1>.Index);
                _query.Ref.With(ComponentType<T2>.Index);
                _query.Ref.With(ComponentType<T3>.Index);
                _query.Ref.With(ComponentType<T4>.Index);
                _query.Ref.With(ComponentType<T5>.Index);
                _query.Ref.With(ComponentType<T6>.Index);
                _query.Ref.With(ComponentType<T7>.Index);

                TOption option = default;

                switch (option)
                {
                    case IComponent _:
                        _query.Ref.With(ComponentType<TOption>.Index);
                        QueryParamInfo<TOption>.IsComponent = true;
                        break;

                    case IFilter filter:
                        filter.Setup(_query.Ptr);
                        break;

                    case ITuple tuple:
                        for (var i = 0; i < tuple.Length; i++)
                            if (tuple[i] is IFilter f)
                                f.Setup(_query.Ptr);
                        break;
                }
            }

            public void FixPointers(ref MemAllocator allocator)
            {
                _query.OnDeserialize(ref allocator);
            }

            public void SetQueryPtr(ptr<QueryUnsafe> q)
            {
                _query = q;
            }

            public void Update(ref World world, IntPtr data)
            {
                _range = *(Range*)(void*)data;
            }

            public IntPtr GetData()
            {
                return (IntPtr)UnsafeUtility.AddressOf(ref _range);
            }

            public bool TryGetQuery(out ptr<QueryUnsafe> query)
            {
                query = _query;
                return true;
            }
        }
    }

    // ===========================================================================

    // Query<T1..T8, TOption>

    // ===========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Query<T1, T2, T3, T4, T5, T6, T7, T8, TOption> : IQuery, ISystemParam
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged, IComponent
        where T6 : unmanaged, IComponent
        where T7 : unmanaged, IComponent
        where T8 : unmanaged, IComponent
        where TOption : unmanaged
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIter<RefTuple<T1, T2, T3, T4, T5, T6, T7, T8, TOption>> GetEnumerator()
        {
            return new QueryIter<RefTuple<T1, T2, T3, T4, T5, T6, T7, T8, TOption>>(in _query.Ref.matchingArchetypes,
                _query.Ref.world);
        }

        public ptr<QueryUnsafe> _query;
        internal int id;
        private Range _range;

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = typeof(T1).GetHashCode();
                hash = hash * 397 ^ typeof(T2).GetHashCode();
                hash = hash * 397 ^ typeof(T3).GetHashCode();
                hash = hash * 397 ^ typeof(T4).GetHashCode();
                hash = hash * 397 ^ typeof(T5).GetHashCode();
                hash = hash * 397 ^ typeof(T6).GetHashCode();
                hash = hash * 397 ^ typeof(T7).GetHashCode();
                hash = hash * 397 ^ typeof(T8).GetHashCode();
                hash = hash * 397 ^ typeof(TOption).GetHashCode();
                return hash;
            }
        }

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();

            _query.Ref.With(ComponentType<T1>.Index);
            _query.Ref.With(ComponentType<T2>.Index);
            _query.Ref.With(ComponentType<T3>.Index);
            _query.Ref.With(ComponentType<T4>.Index);
            _query.Ref.With(ComponentType<T5>.Index);
            _query.Ref.With(ComponentType<T6>.Index);
            _query.Ref.With(ComponentType<T7>.Index);
            _query.Ref.With(ComponentType<T8>.Index);

            id = _query.Ref.Id;
            TOption option = default;

            switch (option)
            {
                case IComponent _:
                    _query.Ref.With(ComponentType<TOption>.Index);
                    QueryParamInfo<TOption>.IsComponent = true;
                    break;

                case IFilter filter:
                    filter.Setup(_query.Ptr);
                    break;

                case ITuple tuple:
                    for (var i = 0; i < tuple.Length; i++)
                        if (tuple[i] is IFilter f)
                            f.Setup(_query.Ptr);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FixPointers(ref MemAllocator allocator)
        {
            _query.OnDeserialize(ref allocator);
        }

        public void SetQueryPtr(ptr<QueryUnsafe> q)
        {
            _query = q;
            id = q.Ref.Id;
        }

        public void Update(ref World world, IntPtr data)
        {
            _query = world.UnsafeWorldRef.queries.ElementAt(id);
            _range = *(Range*)(void*)data;
        }

        public IntPtr GetData()
        {
            return (IntPtr)UnsafeUtility.AddressOf(ref _range);
        }

        public bool TryGetQuery(out ptr<QueryUnsafe> query)
        {
            query = _query;
            return true;
        }

        public struct WithEntity : IQuery, ISystemParam
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public QueryIterWithEntity<EntityRefTuple<T1, T2, T3, T4, T5, T6, T7, T8, TOption>> GetEnumerator()
            {
                return new QueryIterWithEntity<EntityRefTuple<T1, T2, T3, T4, T5, T6, T7, T8, TOption>>(
                    in _query.Ref.matchingArchetypes, _query.Ref.world);
            }

            public ptr<QueryUnsafe> _query;
            private Range _range;

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = typeof(T1).GetHashCode();
                    hash = hash * 397 ^ typeof(T2).GetHashCode();
                    hash = hash * 397 ^ typeof(T3).GetHashCode();
                    hash = hash * 397 ^ typeof(T4).GetHashCode();
                    hash = hash * 397 ^ typeof(T5).GetHashCode();
                    hash = hash * 397 ^ typeof(T6).GetHashCode();
                    hash = hash * 397 ^ typeof(T7).GetHashCode();
                    hash = hash * 397 ^ typeof(T8).GetHashCode();
                    hash = hash * 397 ^ typeof(TOption).GetHashCode();
                    return hash;
                }
            }

            public SystemParamMetaType MetaType => SystemParamMetaType.Query;

            public int Count
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _query.Ref.count;
            }

            public void Init(ref ptr<World.WorldUnsafe> world)
            {
                _query = world.Ref.CreateQueryPtr();

                _query.Ref.With(ComponentType<T1>.Index);
                _query.Ref.With(ComponentType<T2>.Index);
                _query.Ref.With(ComponentType<T3>.Index);
                _query.Ref.With(ComponentType<T4>.Index);
                _query.Ref.With(ComponentType<T5>.Index);
                _query.Ref.With(ComponentType<T6>.Index);
                _query.Ref.With(ComponentType<T7>.Index);
                _query.Ref.With(ComponentType<T8>.Index);

                TOption option = default;

                switch (option)
                {
                    case IComponent _:
                        _query.Ref.With(ComponentType<TOption>.Index);
                        QueryParamInfo<TOption>.IsComponent = true;
                        break;

                    case IFilter filter:
                        filter.Setup(_query.Ptr);
                        break;

                    case ITuple tuple:
                        for (var i = 0; i < tuple.Length; i++)
                            if (tuple[i] is IFilter f)
                                f.Setup(_query.Ptr);
                        break;
                }
            }

            public void FixPointers(ref MemAllocator allocator)
            {
                _query.OnDeserialize(ref allocator);
            }

            public void SetQueryPtr(ptr<QueryUnsafe> q)
            {
                _query = q;
            }

            public void Update(ref World world, IntPtr data)
            {
                _range = *(Range*)(void*)data;
            }

            public IntPtr GetData()
            {
                return (IntPtr)UnsafeUtility.AddressOf(ref _range);
            }

            public bool TryGetQuery(out ptr<QueryUnsafe> query)
            {
                query = _query;
                return true;
            }
        }
    }
}
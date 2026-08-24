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
        where T1 : unmanaged
    {
        private static readonly bool T1IsEntity = typeof(T1) == typeof(Entity);

        public ptr<QueryUnsafe> _query;
        internal int id;
        public Range _range;

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
            if (!T1IsEntity) _query.Ref.With(ComponentType<T1>.Index);
            id = _query.Ref.Id;
            foreach (var ptr in world.Ref.archetypesList)
            {
                ptr.Ref.CheckQuery(in _query);
            }
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

        [BurstDiscard]
        internal void ReinitLostQuery(ref ptr<World.WorldUnsafe> world)
        {
            // Managed-only recovery for a query lost after world deserialization.
            // Init boxes TOption for interface checks (BC1020 under Burst), so this
            // path must stay out of Burst-compiled jobs; under Burst it is a no-op.
            Init(ref world);
        }

        public void Update(ref World world, IntPtr data)
        {
            var worldPtr = world.unsafeWorldPtr;
            var queriesList = worldPtr.Ref.queries;
            ptr<QueryUnsafe> resolved = default;
            if ((uint)id < (uint)queriesList.Length) resolved = queriesList.ElementAt(id);
            if (resolved.IsNull || resolved.Ref.Id != id)
            {
                // Query no longer exists in this world (e.g. the world was deserialized
                // from a snapshot taken before the query was created) — recreate it
                // (managed only; no-op under Burst).
                ReinitLostQuery(ref worldPtr);
            }
            else
            {
                _query = resolved;
            }
            if (data == IntPtr.Zero) _range = new Range(0, Count);
            else _range = *(Range*)(void*)data;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIterT1<T1> iter_refs()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryIterT1<T1>(_query.Ptr);
            return new QueryIterT1<T1>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<RefTuple<T1>> iter()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryIter<RefTuple<T1>>(_query.Ptr);
            return new QueryIter<RefTuple<T1>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryParIter<RefTuple<T1>> par_iter()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryParIter<RefTuple<T1>>(_range, _query.Ptr);
            return new QueryParIter<RefTuple<T1>>(in _range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryChunkIter<Chunk<T1>> iter_chunk()
        {
            return new QueryChunkIter<Chunk<T1>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }
    }

    // ===========================================================================

    // Query<T1, TOption>

    // ===========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Query<T1, TOption> : IQuery, ISystemParam
        where T1 : unmanaged
        where TOption : unmanaged
    {
        private static readonly bool T1IsEntity = typeof(T1) == typeof(Entity);

        public ptr<QueryUnsafe> _query;
        internal int id;
        public Range _range;
        public Range Range => _range;
        
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
                    {
                        var rowsPtr = arch.RowsAreDense ? null : arch.rows.Ptr;
                        var row0 = rowsPtr != null ? rowsPtr[0] : 0;
                        return ref _query.Ref.world->entities.Ptr[arch.packedEntities.Ptr[row0]];
                    }
                }
            }

            throw new Exception("No entities found");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<PtrTuple<T1, TOption>> iter_unsafe()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryIter<PtrTuple<T1, TOption>>(_query.Ptr);
            return new QueryIter<PtrTuple<T1, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryChunkIter<Chunk<T1>> iter_chunk()
        {
            return new QueryChunkIter<Chunk<T1>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<PtrTuple<T1, TOption>> par_iter_unsafe()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryParIter<PtrTuple<T1, TOption>>(_range, _query.Ptr);
            return new QueryParIter<PtrTuple<T1, TOption>>(in _range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<RefTuple<T1, TOption>> par_iter()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryParIter<RefTuple<T1, TOption>>(_range, _query.Ptr);
            return new QueryParIter<RefTuple<T1, TOption>>(in _range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<RefTuple<T1, TOption>> iter()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryIter<RefTuple<T1, TOption>>(_query.Ptr);
            return new QueryIter<RefTuple<T1, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryParIter<RefTuple<T1, TOption>> iter_refs()
        {
            if (_query.Ref.TryUseStorageIteration()) return new (_range, _query.Ptr);
            return new (_range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

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
            if (!T1IsEntity) _query.Ref.With(ComponentType<T1>.Index);
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
            foreach (var ptr in world.Ref.archetypesList)
            {
                ptr.Ref.CheckQuery(in _query);
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

        [BurstDiscard]
        internal void ReinitLostQuery(ref ptr<World.WorldUnsafe> world)
        {
            // Managed-only recovery for a query lost after world deserialization.
            // Init boxes TOption for interface checks (BC1020 under Burst), so this
            // path must stay out of Burst-compiled jobs; under Burst it is a no-op.
            Init(ref world);
        }

        public void Update(ref World world, IntPtr data)
        {
            var worldPtr = world.unsafeWorldPtr;
            var queriesList = worldPtr.Ref.queries;
            ptr<QueryUnsafe> resolved = default;
            if ((uint)id < (uint)queriesList.Length) resolved = queriesList.ElementAt(id);
            if (resolved.IsNull || resolved.Ref.Id != id)
            {
                // Query no longer exists in this world (e.g. the world was deserialized
                // from a snapshot taken before the query was created) — recreate it
                // (managed only; no-op under Burst).
                ReinitLostQuery(ref worldPtr);
            }
            else
            {
                _query = resolved;
            }
            if (data == IntPtr.Zero) _range = new Range(0, Count);
            else _range = *(Range*)(void*)data;
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

    // ===========================================================================

    // Query<T1,T2, TOption>

    // ===========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Query<T1, T2, TOption> : IQuery, ISystemParam
        where T1 : unmanaged
        where T2 : unmanaged, IComponent
        where TOption : unmanaged

    {
        private static readonly bool T1IsEntity = typeof(T1) == typeof(Entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<PtrTuple<T1, T2, TOption>> iter_unsafe()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryIter<PtrTuple<T1, T2, TOption>>(_query.Ptr);
            return new QueryIter<PtrTuple<T1, T2, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryChunkIter<Chunk<T1, T2>> iter_chunk()
        {
            return new QueryChunkIter<Chunk<T1, T2>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<PtrTuple<T1, T2, TOption>> par_iter_unsafe()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryParIter<PtrTuple<T1, T2, TOption>>(_range, _query.Ptr);
            return new QueryParIter<PtrTuple<T1, T2, TOption>>(in _range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<RefTuple<T1, T2, TOption>> iter()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryIter<RefTuple<T1, T2, TOption>>(_query.Ptr);
            return new QueryIter<RefTuple<T1, T2, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryParIter<RefTuple<T1, T2, TOption>> iter_refs()
        {
            if (_query.Ref.TryUseStorageIteration()) return new (_range, _query.Ptr);
            return new (_range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<RefTuple<T1, T2, TOption>> par_iter()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryParIter<RefTuple<T1, T2, TOption>>(_range, _query.Ptr);
            return new QueryParIter<RefTuple<T1, T2, TOption>>(in _range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        public ptr<QueryUnsafe> _query;
        internal int id;
        public Range _range;

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

        [BurstDiscard]
        internal void ReinitLostQuery(ref ptr<World.WorldUnsafe> world)
        {
            // Managed-only recovery for a query lost after world deserialization.
            // Init boxes TOption for interface checks (BC1020 under Burst), so this
            // path must stay out of Burst-compiled jobs; under Burst it is a no-op.
            Init(ref world);
        }

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

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();
            id = _query.Ref.Id;
            if (!T1IsEntity)
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
            
            foreach (var ptr in world.Ref.archetypesList)
            {
                ptr.Ref.CheckQuery(in _query);
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
            var worldPtr = world.unsafeWorldPtr;
            var queriesList = worldPtr.Ref.queries;
            ptr<QueryUnsafe> resolved = default;
            if ((uint)id < (uint)queriesList.Length) resolved = queriesList.ElementAt(id);
            if (resolved.IsNull || resolved.Ref.Id != id)
            {
                // Query no longer exists in this world (e.g. the world was deserialized
                // from a snapshot taken before the query was created) — recreate it
                // (managed only; no-op under Burst).
                ReinitLostQuery(ref worldPtr);
            }
            else
            {
                _query = resolved;
            }
            if (data == IntPtr.Zero) _range = new Range(0, Count);
            else _range = *(Range*)(void*)data;
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
    // ===========================================================================

    // Query<T1..T3, TOption>

    // ===========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Query<T1, T2, T3, TOption> : IQuery, ISystemParam
        where T1 : unmanaged
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where TOption : unmanaged
    {
        private static readonly bool T1IsEntity = typeof(T1) == typeof(Entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<PtrTuple<T1, T2, T3, TOption>> iter_unsafe()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryIter<PtrTuple<T1, T2, T3, TOption>>(_query.Ptr);
            return new QueryIter<PtrTuple<T1, T2, T3, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryChunkIter<Chunk<T1, T2, T3>> iter_chunk()
        {
            return new QueryChunkIter<Chunk<T1, T2, T3>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // ReSharper disable once MethodOverloadWithOptionalParameter
        public readonly QueryChunkIter<Chunk<T1, T2, T3, TOption>> iter_chunk(bool withOption = true)
        {
            return new QueryChunkIter<Chunk<T1, T2, T3, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<PtrTuple<T1, T2, T3, TOption>> par_iter_unsafe()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryParIter<PtrTuple<T1, T2, T3, TOption>>(_range, _query.Ptr);
            return new QueryParIter<PtrTuple<T1, T2, T3, TOption>>(in _range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<RefTuple<T1, T2, T3, TOption>> par_iter()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryParIter<RefTuple<T1, T2, T3, TOption>>(_range, _query.Ptr);
            return new QueryParIter<RefTuple<T1, T2, T3, TOption>>(in _range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<RefTuple<T1, T2, T3, TOption>> iter()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryIter<RefTuple<T1, T2, T3, TOption>>(_query.Ptr);
            return new QueryIter<RefTuple<T1, T2, T3, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryParIter<RefTuple<T1, T2, T3, TOption>> iter_refs()
        {
            if (_query.Ref.TryUseStorageIteration()) return new (_range, _query.Ptr);
            return new (_range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        public ptr<QueryUnsafe> _query;
        internal int id;
        public Range _range;


        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

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

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();
            if (!T1IsEntity)
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
                        
            foreach (var ptr in world.Ref.archetypesList)
            {
                ptr.Ref.CheckQuery(in _query);
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
            var worldPtr = world.unsafeWorldPtr;
            var queriesList = worldPtr.Ref.queries;
            ptr<QueryUnsafe> resolved = default;
            if ((uint)id < (uint)queriesList.Length) resolved = queriesList.ElementAt(id);
            if (resolved.IsNull || resolved.Ref.Id != id)
            {
                // Query no longer exists in this world (e.g. the world was deserialized
                // from a snapshot taken before the query was created) — recreate it
                // (managed only; no-op under Burst).
                ReinitLostQuery(ref worldPtr);
            }
            else
            {
                _query = resolved;
            }
            if (data == IntPtr.Zero) _range = new Range(0, Count);
            else _range = *(Range*)(void*)data;
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

        [BurstDiscard]
        internal void ReinitLostQuery(ref ptr<World.WorldUnsafe> world)
        {
            // Managed-only recovery for a query lost after world deserialization.
            // Init boxes TOption for interface checks (BC1020 under Burst), so this
            // path must stay out of Burst-compiled jobs; under Burst it is a no-op.
            Init(ref world);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Query<T1, T2, T3, T4, T5> : IQuery, ISystemParam
        where T1 : unmanaged
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged
    {
        private static readonly bool T1IsEntity = typeof(T1) == typeof(Entity);

        public ptr<QueryUnsafe> _query;
        internal int id;
        public Range _range;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter5<T1, T2, T3, T4, T5> iter_new()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryIter5<T1, T2, T3, T4, T5>(_query.Ptr);
            return new QueryIter5<T1, T2, T3, T4, T5>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryRefIter5<T1, T2, T3, T4, T5> iter_new_ref()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryRefIter5<T1, T2, T3, T4, T5>(_query.Ptr);
            return new QueryRefIter5<T1, T2, T3, T4, T5>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter5<T1, T2, T3, T4, T5> iter_unsafe() => iter_new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryRefIter5<T1, T2, T3, T4, T5> iter() => iter_new_ref();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<RefTuple<T1, T2, T3, T4, T5>> par_iter()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryParIter<RefTuple<T1, T2, T3, T4, T5>>(_range, _query.Ptr);
            return new QueryParIter<RefTuple<T1, T2, T3, T4, T5>>(in _range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<PtrTuple<T1, T2, T3, T4, T5>> par_iter_unsafe()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryParIter<PtrTuple<T1, T2, T3, T4, T5>>(_range, _query.Ptr);
            return new QueryParIter<PtrTuple<T1, T2, T3, T4, T5>>(in _range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryParIter<RefTuple<T1, T2, T3, T4, T5>> iter_refs()
        {
            if (_query.Ref.TryUseStorageIteration()) return new (_range, _query.Ptr);
            return new (_range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryChunkIter<Chunk<T1, T2, T3, T4>> iter_chunk()
        {
            return new QueryChunkIter<Chunk<T1, T2, T3, T4>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }
        
        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = typeof(T1).GetHashCode();
                hash = hash * 397 ^ typeof(T2).GetHashCode();
                hash = hash * 397 ^ typeof(T3).GetHashCode();
                hash = hash * 397 ^ typeof(T4).GetHashCode();
                hash = hash * 397 ^ typeof(T5).GetHashCode();
                return hash;
            }
        }

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();
            if (!T1IsEntity) _query.Ref.With(ComponentType<T1>.Index);
            _query.Ref.With(ComponentType<T2>.Index);
            _query.Ref.With(ComponentType<T3>.Index);
            _query.Ref.With(ComponentType<T4>.Index);
            id = _query.Ref.Id;
            T5 option = default;
            switch (option)
            {
                case IComponent _:
                    _query.Ref.With(ComponentType<T5>.Index);
                    QueryParamInfo<T5>.IsComponent = true;
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

            foreach (var ptr in world.Ref.archetypesList)
            {
                ptr.Ref.CheckQuery(in _query);
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
            var worldPtr = world.unsafeWorldPtr;
            var queriesList = worldPtr.Ref.queries;
            ptr<QueryUnsafe> resolved = default;
            if ((uint)id < (uint)queriesList.Length) resolved = queriesList.ElementAt(id);
            if (resolved.IsNull || resolved.Ref.Id != id)
            {
                // Query no longer exists in this world (e.g. the world was deserialized
                // from a snapshot taken before the query was created) — recreate it
                // (managed only; no-op under Burst).
                ReinitLostQuery(ref worldPtr);
            }
            else
            {
                _query = resolved;
            }
            if (data == IntPtr.Zero) _range = new Range(0, Count);
            else _range = *(Range*)(void*)data;
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

        [BurstDiscard]
        internal void ReinitLostQuery(ref ptr<World.WorldUnsafe> world)
        {
            // Managed-only recovery for a query lost after world deserialization.
            // Init boxes TOption for interface checks (BC1020 under Burst), so this
            // path must stay out of Burst-compiled jobs; under Burst it is a no-op.
            Init(ref world);
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
        private static readonly bool T1IsEntity = typeof(T1) == typeof(Entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<RefTuple<T1, T2, T3, T4, T5, TOption>> par_iter()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryParIter<RefTuple<T1, T2, T3, T4, T5, TOption>>(_range, _query.Ptr);
            return new QueryParIter<RefTuple<T1, T2, T3, T4, T5, TOption>>(in _range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<RefTuple<T1, T2, T3, T4, T5, TOption>> iter()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryIter<RefTuple<T1, T2, T3, T4, T5, TOption>>(_query.Ptr);
            return new QueryIter<RefTuple<T1, T2, T3, T4, T5, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<PtrTuple<T1, T2, T3, T4, T5, TOption>> iter_unsafe()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryIter<PtrTuple<T1, T2, T3, T4, T5, TOption>>(_query.Ptr);
            return new QueryIter<PtrTuple<T1, T2, T3, T4, T5, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<PtrTuple<T1, T2, T3, T4, T5, TOption>> par_iter_unsafe()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryParIter<PtrTuple<T1, T2, T3, T4, T5, TOption>>(_range, _query.Ptr);
            return new QueryParIter<PtrTuple<T1, T2, T3, T4, T5, TOption>>(in _range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<RefTuple<T1, T2, T3, T4, T5, TOption>> iter_refs() => par_iter();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryChunkIter<Chunk<T1, T2, T3, T4, T5>> iter_chunk()
        {
            return new QueryChunkIter<Chunk<T1, T2, T3, T4, T5>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        public ptr<QueryUnsafe> _query;
        internal int id;
        public Range _range;


        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

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

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();
            if (!T1IsEntity)
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
                        
            foreach (var ptr in world.Ref.archetypesList)
            {
                ptr.Ref.CheckQuery(in _query);
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
            var worldPtr = world.unsafeWorldPtr;
            var queriesList = worldPtr.Ref.queries;
            ptr<QueryUnsafe> resolved = default;
            if ((uint)id < (uint)queriesList.Length) resolved = queriesList.ElementAt(id);
            if (resolved.IsNull || resolved.Ref.Id != id)
            {
                // Query no longer exists in this world (e.g. the world was deserialized
                // from a snapshot taken before the query was created) — recreate it
                // (managed only; no-op under Burst).
                ReinitLostQuery(ref worldPtr);
            }
            else
            {
                _query = resolved;
            }
            if (data == IntPtr.Zero) _range = new Range(0, Count);
            else _range = *(Range*)(void*)data;
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

        [BurstDiscard]
        internal void ReinitLostQuery(ref ptr<World.WorldUnsafe> world)
        {
            // Managed-only recovery for a query lost after world deserialization.
            // Init boxes TOption for interface checks (BC1020 under Burst), so this
            // path must stay out of Burst-compiled jobs; under Burst it is a no-op.
            Init(ref world);
        }
    }

    // ===========================================================================

    // Query<T1..T6, TOption>

    // ===========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Query<T1, T2, T3, T4, T5, T6, TOption> : IQuery, ISystemParam
        where T1 : unmanaged
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged, IComponent
        where T6 : unmanaged, IComponent
        where TOption : unmanaged
    {
        private static readonly bool T1IsEntity = typeof(T1) == typeof(Entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryChunkIter<Chunk<T1, T2, T3, T4, T5, T6>> iter_chunk()
        {
            return new QueryChunkIter<Chunk<T1, T2, T3, T4, T5, T6>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<PtrTuple<T1, T2, T3, T4, T5, T6, TOption>> iter_unsafe()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryIter<PtrTuple<T1, T2, T3, T4, T5, T6, TOption>>(_query.Ptr);
            return new QueryIter<PtrTuple<T1, T2, T3, T4, T5, T6, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<PtrTuple<T1, T2, T3, T4, T5, T6, TOption>> par_iter_unsafe()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryParIter<PtrTuple<T1, T2, T3, T4, T5, T6, TOption>>(_range, _query.Ptr);
            return new QueryParIter<PtrTuple<T1, T2, T3, T4, T5, T6, TOption>>(in _range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<RefTuple<T1, T2, T3, T4, T5, T6, TOption>> par_iter()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryParIter<RefTuple<T1, T2, T3, T4, T5, T6, TOption>>(_range, _query.Ptr);
            return new QueryParIter<RefTuple<T1, T2, T3, T4, T5, T6, TOption>>(in _range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<RefTuple<T1, T2, T3, T4, T5, T6, TOption>> iter()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryIter<RefTuple<T1, T2, T3, T4, T5, T6, TOption>>(_query.Ptr);
            return new QueryIter<RefTuple<T1, T2, T3, T4, T5, T6, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIter<RefTuple<T1, T2, T3, T4, T5, T6, TOption>> iter_refs()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryIter<RefTuple<T1, T2, T3, T4, T5, T6, TOption>>(_query.Ptr);
            return new QueryIter<RefTuple<T1, T2, T3, T4, T5, T6, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        public ptr<QueryUnsafe> _query;
        internal int id;
        public Range _range;

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

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

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();

            if (!T1IsEntity)
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
                        
            foreach (var ptr in world.Ref.archetypesList)
            {
                ptr.Ref.CheckQuery(in _query);
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
            var worldPtr = world.unsafeWorldPtr;
            var queriesList = worldPtr.Ref.queries;
            ptr<QueryUnsafe> resolved = default;
            if ((uint)id < (uint)queriesList.Length) resolved = queriesList.ElementAt(id);
            if (resolved.IsNull || resolved.Ref.Id != id)
            {
                // Query no longer exists in this world (e.g. the world was deserialized
                // from a snapshot taken before the query was created) — recreate it
                // (managed only; no-op under Burst).
                ReinitLostQuery(ref worldPtr);
            }
            else
            {
                _query = resolved;
            }
            if (data == IntPtr.Zero) _range = new Range(0, Count);
            else _range = *(Range*)(void*)data;
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

        [BurstDiscard]
        internal void ReinitLostQuery(ref ptr<World.WorldUnsafe> world)
        {
            // Managed-only recovery for a query lost after world deserialization.
            // Init boxes TOption for interface checks (BC1020 under Burst), so this
            // path must stay out of Burst-compiled jobs; under Burst it is a no-op.
            Init(ref world);
        }
    }

    // ===========================================================================

    // Query<T1..T7, TOption>

    // ===========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Query<T1, T2, T3, T4, T5, T6, T7, TOption> : IQuery, ISystemParam
        where T1 : unmanaged
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged, IComponent
        where T6 : unmanaged, IComponent
        where T7 : unmanaged, IComponent
        where TOption : unmanaged
    {
        private static readonly bool T1IsEntity = typeof(T1) == typeof(Entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryChunkIter<Chunk<T1, T2, T3, T4, T5, T6, T7>> iter_chunk()
        {
            return new QueryChunkIter<Chunk<T1, T2, T3, T4, T5, T6, T7>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<PtrTuple<T1, T2, T3, T4, T5, T6, T7, TOption>> iter_unsafe()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryIter<PtrTuple<T1, T2, T3, T4, T5, T6, T7, TOption>>(_query.Ptr);
            return new QueryIter<PtrTuple<T1, T2, T3, T4, T5, T6, T7, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<PtrTuple<T1, T2, T3, T4, T5, T6, T7, TOption>> par_iter_unsafe()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryParIter<PtrTuple<T1, T2, T3, T4, T5, T6, T7, TOption>>(_range, _query.Ptr);
            return new QueryParIter<PtrTuple<T1, T2, T3, T4, T5, T6, T7, TOption>>(in _range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<RefTuple<T1, T2, T3, T4, T5, T6, T7, TOption>> par_iter()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryParIter<RefTuple<T1, T2, T3, T4, T5, T6, T7, TOption>>(_range, _query.Ptr);
            return new QueryParIter<RefTuple<T1, T2, T3, T4, T5, T6, T7, TOption>>(in _range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<RefTuple<T1, T2, T3, T4, T5, T6, T7, TOption>> iter()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryIter<RefTuple<T1, T2, T3, T4, T5, T6, T7, TOption>>(_query.Ptr);
            return new QueryIter<RefTuple<T1, T2, T3, T4, T5, T6, T7, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIter<RefTuple<T1, T2, T3, T4, T5, T6, T7, TOption>> iter_refs()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryIter<RefTuple<T1, T2, T3, T4, T5, T6, T7, TOption>>(_query.Ptr);
            return new QueryIter<RefTuple<T1, T2, T3, T4, T5, T6, T7, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        public ptr<QueryUnsafe> _query;
        internal int id;
        public Range _range;

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

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

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();

            if (!T1IsEntity)
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
                        
            foreach (var ptr in world.Ref.archetypesList)
            {
                ptr.Ref.CheckQuery(in _query);
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
            var worldPtr = world.unsafeWorldPtr;
            var queriesList = worldPtr.Ref.queries;
            ptr<QueryUnsafe> resolved = default;
            if ((uint)id < (uint)queriesList.Length) resolved = queriesList.ElementAt(id);
            if (resolved.IsNull || resolved.Ref.Id != id)
            {
                // Query no longer exists in this world (e.g. the world was deserialized
                // from a snapshot taken before the query was created) — recreate it
                // (managed only; no-op under Burst).
                ReinitLostQuery(ref worldPtr);
            }
            else
            {
                _query = resolved;
            }
            if (data == IntPtr.Zero) _range = new Range(0, Count);
            else _range = *(Range*)(void*)data;
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

        [BurstDiscard]
        internal void ReinitLostQuery(ref ptr<World.WorldUnsafe> world)
        {
            // Managed-only recovery for a query lost after world deserialization.
            // Init boxes TOption for interface checks (BC1020 under Burst), so this
            // path must stay out of Burst-compiled jobs; under Burst it is a no-op.
            Init(ref world);
        }
    }

    // ===========================================================================

    // Query<T1..T8, TOption>

    // ===========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Query<T1, T2, T3, T4, T5, T6, T7, T8, TOption> : IQuery, ISystemParam
        where T1 : unmanaged
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged, IComponent
        where T6 : unmanaged, IComponent
        where T7 : unmanaged, IComponent
        where T8 : unmanaged, IComponent
        where TOption : unmanaged
    {
        private static readonly bool T1IsEntity = typeof(T1) == typeof(Entity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryChunkIter<Chunk<T1, T2, T3, T4, T5, T6, T7, T8>> iter_chunk()
        {
            return new QueryChunkIter<Chunk<T1, T2, T3, T4, T5, T6, T7, T8>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<PtrTuple<T1, T2, T3, T4, T5, T6, T7, T8, TOption>> iter_unsafe()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryIter<PtrTuple<T1, T2, T3, T4, T5, T6, T7, T8, TOption>>(_query.Ptr);
            return new QueryIter<PtrTuple<T1, T2, T3, T4, T5, T6, T7, T8, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<PtrTuple<T1, T2, T3, T4, T5, T6, T7, T8, TOption>> par_iter_unsafe()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryParIter<PtrTuple<T1, T2, T3, T4, T5, T6, T7, T8, TOption>>(_range, _query.Ptr);
            return new QueryParIter<PtrTuple<T1, T2, T3, T4, T5, T6, T7, T8, TOption>>(in _range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryParIter<RefTuple<T1, T2, T3, T4, T5, T6, T7, T8, TOption>> par_iter()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryParIter<RefTuple<T1, T2, T3, T4, T5, T6, T7, T8, TOption>>(_range, _query.Ptr);
            return new QueryParIter<RefTuple<T1, T2, T3, T4, T5, T6, T7, T8, TOption>>(in _range, in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly QueryIter<RefTuple<T1, T2, T3, T4, T5, T6, T7, T8, TOption>> iter()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryIter<RefTuple<T1, T2, T3, T4, T5, T6, T7, T8, TOption>>(_query.Ptr);
            return new QueryIter<RefTuple<T1, T2, T3, T4, T5, T6, T7, T8, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public QueryIter<RefTuple<T1, T2, T3, T4, T5, T6, T7, T8, TOption>> iter_refs()
        {
            if (_query.Ref.TryUseStorageIteration()) return new QueryIter<RefTuple<T1, T2, T3, T4, T5, T6, T7, T8, TOption>>(_query.Ptr);
            return new QueryIter<RefTuple<T1, T2, T3, T4, T5, T6, T7, T8, TOption>>(in _query.Ref.matchingArchetypes, _query.Ref.world);
        }

        public ptr<QueryUnsafe> _query;
        internal int id;
        public Range _range;

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

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

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();

            if (!T1IsEntity)
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
                        
            foreach (var ptr in world.Ref.archetypesList)
            {
                ptr.Ref.CheckQuery(in _query);
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
            var worldPtr = world.unsafeWorldPtr;
            var queriesList = worldPtr.Ref.queries;
            ptr<QueryUnsafe> resolved = default;
            if ((uint)id < (uint)queriesList.Length) resolved = queriesList.ElementAt(id);
            if (resolved.IsNull || resolved.Ref.Id != id)
            {
                // Query no longer exists in this world (e.g. the world was deserialized
                // from a snapshot taken before the query was created) — recreate it
                // (managed only; no-op under Burst).
                ReinitLostQuery(ref worldPtr);
            }
            else
            {
                _query = resolved;
            }
            if (data == IntPtr.Zero) _range = new Range(0, Count);
            else _range = *(Range*)(void*)data;
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

        [BurstDiscard]
        internal void ReinitLostQuery(ref ptr<World.WorldUnsafe> world)
        {
            // Managed-only recovery for a query lost after world deserialization.
            // Init boxes TOption for interface checks (BC1020 under Burst), so this
            // path must stay out of Burst-compiled jobs; under Burst it is a no-op.
            Init(ref world);
        }
    }
}
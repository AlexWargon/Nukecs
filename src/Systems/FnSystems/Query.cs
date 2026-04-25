using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

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
        private ArchetypeRef<T1> _t1;

        private ptr<QueryUnsafe> _query;

        private Range _range;

        private int _current;

        private int _archIdx;
        private int _archRow;
        private int _archEntityEnd;

        static readonly bool T1IsPool = ComponentType<T1>.Data.storageType == StorageType.Pool;

        public readonly void Deconstruct(out ArchetypeRef<T1> c)
        {
            c = _t1;
        }

        public ref Entity GetEntity(int index)
        {
            return ref _query.cached->GetEntity(index);
        }

        public ref T1 Current => ref _t1.Val;

        public int Count

        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

        public bool MoveNext()

        {
            if (++_current >= _range.end) return false;

            if (++_archRow >= _archEntityEnd)
            {
                if (++_archIdx >= _query.Ref.matchingArchetypes.length) return false;
                SetupArchetypeRefs();
                return true;
            }

            AdvanceRefs();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupArchetypeRefs()
        {
            ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
            _archRow = 0;
            _archEntityEnd = arch.count;
            if (T1IsPool)
                _t1.SetPool(_query.Ref.world->GetUntypedPool(ComponentType<T1>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
            else
            {
                var li = arch.GetComponentLocalIndex(ComponentType<T1>.Index);
                _t1.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdvanceRefs()
        {
            if (T1IsPool)
            {
                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                _t1.AdvancePool(arch.packedEntities.Ptr[_archRow]);
            }
            else
                _t1.AdvanceArchetype(_archRow);
        }

        public void Init(ref ptr<World.WorldUnsafe> world)

        {
            _query = world.Ref.CreateQueryPtr();

            _query.Ref.With(ComponentType<T1>.Index);
        }

        public void Update(ref World world, IntPtr data)

        {
            _range = *(Range*)data;

            _current = _range.start - 1;

            _archIdx = -1;
            _archRow = 0;
            _archEntityEnd = 0;

            if (_query.Ref.matchingArchetypes.length > 0)
            {
                var remaining = _range.start;
                for (int i = 0; i < _query.Ref.matchingArchetypes.length; i++)
                {
                    ref var arch = ref world.UnsafeWorld->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[i]].Ref;
                    if (remaining < arch.count)
                    {
                        _archIdx = i;
                        _archEntityEnd = arch.count;
                        if (T1IsPool)
                            _t1.SetPool(world.UnsafeWorld->GetUntypedPool(ComponentType<T1>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
                        else
                        {
                            var li = arch.GetComponentLocalIndex(ComponentType<T1>.Index);
                            _t1.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
                        }
                        _archRow = remaining - 1;
                        break;
                    }
                    remaining -= arch.count;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupArchetypeRefsFirst(ref World world)
        {
            ref var arch = ref world.UnsafeWorld->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[0]].Ref;
            _archEntityEnd = arch.count;
            if (T1IsPool)
                _t1.SetPool(world.UnsafeWorld->GetUntypedPool(ComponentType<T1>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
            else
            {
                var li = arch.GetComponentLocalIndex(ComponentType<T1>.Index);
                _t1.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
            }
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

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public struct WithEntity : IQuery, ISystemParam

        {
            private ArchetypeRef<T1> _t1;

            private ptr<QueryUnsafe> _query;

            private Range _range;

            private int _current;
            private int _archIdx;
            private int _archRow;
            private int _archEntityEnd;

            static readonly bool T1IsPool = ComponentType<T1>.Data.storageType == StorageType.Pool;

            public SystemParamMetaType MetaType => SystemParamMetaType.Query;

            public ref Entity GetEntity(int index)
            {
                return ref _query.cached->GetEntity(index);
            }

            public WithEntity Current => this;

            public int Count

            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _query.Ref.count;
            }

            public bool MoveNext()

            {
                if (++_current >= _range.end) return false;

                if (++_archRow >= _archEntityEnd)
                {
                    if (++_archIdx >= _query.Ref.matchingArchetypes.length) return false;
                    SetupArchetypeRefs();
                    return true;
                }

                AdvanceRefs();
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetupArchetypeRefs()
            {
                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                _archRow = 0;
                _archEntityEnd = arch.count;
                if (T1IsPool)
                    _t1.SetPool(_query.Ref.world->GetUntypedPool(ComponentType<T1>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
                else
                {
                    var li = arch.GetComponentLocalIndex(ComponentType<T1>.Index);
                    _t1.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void AdvanceRefs()
            {
                if (T1IsPool)
                {
                    ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                    _t1.AdvancePool(arch.packedEntities.Ptr[_archRow]);
                }
                else
                    _t1.AdvanceArchetype(_archRow);
            }

            public readonly void Deconstruct(out Entity e, out ArchetypeRef<T1> c)

            {
                c = _t1;

                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                e = _query.Ref.world->entities.Ptr[arch.packedEntities.Ptr[_archRow]];
            }

            public void Init(ref ptr<World.WorldUnsafe> world)

            {
                _query = world.Ref.CreateQueryPtr();

                _query.Ref.With(ComponentType<T1>.Index);
            }

            public void Update(ref World world, IntPtr data)

            {
                _range = *(Range*)(void*)data;

                _current = _range.start - 1;

                _archIdx = -1;
                _archRow = 0;
                _archEntityEnd = 0;

                if (_query.Ref.matchingArchetypes.length > 0)
                {
                    var remaining = _range.start;
                    for (int i = 0; i < _query.Ref.matchingArchetypes.length; i++)
                    {
                        ref var arch = ref world.UnsafeWorld->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[i]].Ref;
                        if (remaining < arch.count)
                        {
                            _archIdx = i;
                            _archEntityEnd = arch.count;
                            if (T1IsPool)
                                _t1.SetPool(world.UnsafeWorld->GetUntypedPool(ComponentType<T1>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
                            else
                            {
                                var li = arch.GetComponentLocalIndex(ComponentType<T1>.Index);
                                _t1.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
                            }
                            _archRow = remaining - 1;
                            break;
                        }
                        remaining -= arch.count;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetupArchetypeRefsFirst(ref World world)
            {
                ref var arch = ref world.UnsafeWorld->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[0]].Ref;
                _archEntityEnd = arch.count;
                if (T1IsPool)
                    _t1.SetPool(world.UnsafeWorld->GetUntypedPool(ComponentType<T1>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
                else
                {
                    var li = arch.GetComponentLocalIndex(ComponentType<T1>.Index);
                    _t1.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
                }
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
        private ArchetypeRef<T1> _t1;

        private ArchetypeRef<TOption> _tOption;

        private ptr<QueryUnsafe> _query;

        private int _current;

        private Range _range;

        private int _archIdx;
        private int _archRow;
        private int _archEntityEnd;

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public readonly void Deconstruct(out ArchetypeRef<T1> c)
        {
            c = _t1;
        }

        public readonly void Deconstruct(out ArchetypeRef<T1> c, out ArchetypeRef<TOption> opt)
        {
            c = _t1;
            opt = _tOption;
        }

        public Query<T1, TOption> Current => this;

        public int Count

        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()

        {
            if (++_current >= _range.end) return false;

            if (++_archRow >= _archEntityEnd)
            {
                if (++_archIdx >= _query.Ref.matchingArchetypes.length) return false;
                SetupArchetypeRefs();
                return true;
            }

            AdvanceRefs();
            return true;
        }

        static readonly bool T1IsPool = ComponentType<T1>.Data.storageType == StorageType.Pool;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupArchetypeRefs()
        {
            ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
            _archRow = 0;
            _archEntityEnd = arch.count;
            SetupT1(ref arch);
            if (QueryParamInfo<TOption>.IsComponent)
                SetupTOption(ref arch);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupT1(ref ArchetypeUnsafe arch)
        {
            if (T1IsPool)
                _t1.SetPool(_query.Ref.world->GetUntypedPool(ComponentType<T1>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
            else
            {
                var li = arch.GetComponentLocalIndex(ComponentType<T1>.Index);
                _t1.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupTOption(ref ArchetypeUnsafe arch)
        {
            var li = arch.GetComponentLocalIndex(ComponentType<TOption>.Index);
            if (li >= 0)
                _tOption.Set(arch.data.Ptr, arch.GetComponentOffset(li), 0, arch.GetComponentSize(li));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdvanceRefs()
        {
            if (T1IsPool)
            {
                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                _t1.AdvancePool(arch.packedEntities.Ptr[_archRow]);
            }
            else
            {
                _t1.AdvanceArchetype(_archRow);
            }
            if (QueryParamInfo<TOption>.IsComponent) _tOption.SetRow(_archRow);
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
        public void Update(ref World world, IntPtr data)

        {
            _range = *(Range*)(void*)data;

            _current = _range.start - 1;

            _archIdx = -1;
            _archRow = 0;
            _archEntityEnd = 0;

            if (_query.Ref.matchingArchetypes.length > 0)
            {
                var remaining = _range.start;
                for (int i = 0; i < _query.Ref.matchingArchetypes.length; i++)
                {
                    ref var arch = ref world.UnsafeWorld->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[i]].Ref;
                    if (remaining < arch.count)
                    {
                        _archIdx = i;
                        _archEntityEnd = arch.count;
                        SetupT1(ref arch);
                        if (QueryParamInfo<TOption>.IsComponent)
                            SetupTOption(ref arch);
                        _archRow = remaining - 1;
                        break;
                    }
                    remaining -= arch.count;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupArchetypeRefsFirst(ref World world)
        {
            ref var arch = ref world.UnsafeWorld->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[0]].Ref;
            _archEntityEnd = arch.count;
            SetupT1(ref arch);
            if (QueryParamInfo<TOption>.IsComponent)
                SetupTOption(ref arch);
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

        [BurstCompile]
        public struct WithEntity : IQuery, ISystemParam

        {
            private ArchetypeRef<T1> _t1;

            private ArchetypeRef<TOption> _tOption;

            private ptr<QueryUnsafe> _query;

            private int _current;

            private Range _range;

            private int _archIdx;
            private int _archRow;
            private int _archEntityEnd;

            public void SetRange(Range range)
            {
                _range = range;
            }

            public SystemParamMetaType MetaType => SystemParamMetaType.Query;

            public WithEntity Current

            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                [BurstCompile]
                get => this;
            }

            public int Count

            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _query.Ref.count;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            [BurstCompile]
            public bool MoveNext()

            {
                if (++_current >= _range.end) return false;

                if (++_archRow >= _archEntityEnd)
                {
                    if (++_archIdx >= _query.Ref.matchingArchetypes.length) return false;
                    SetupArchetypeRefs();
                    return true;
                }

                AdvanceRefs();
                return true;
            }

            static readonly bool T1IsPool = ComponentType<T1>.Data.storageType == StorageType.Pool;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetupArchetypeRefs()
            {
                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                _archRow = 0;
                _archEntityEnd = arch.count;
                SetupT1(ref arch);
                if (QueryParamInfo<TOption>.IsComponent)
                    SetupTOption(ref arch);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetupT1(ref ArchetypeUnsafe arch)
            {
                if (T1IsPool)
                    _t1.SetPool(_query.Ref.world->GetUntypedPool(ComponentType<T1>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
                else
                {
                    var li = arch.GetComponentLocalIndex(ComponentType<T1>.Index);
                    _t1.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetupTOption(ref ArchetypeUnsafe arch)
            {
                var li = arch.GetComponentLocalIndex(ComponentType<TOption>.Index);
                if (li >= 0)
                    _tOption.Set(arch.data.Ptr, arch.GetComponentOffset(li), 0, arch.GetComponentSize(li));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void AdvanceRefs()
            {
                if (T1IsPool)
                {
                    ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                    _t1.AdvancePool(arch.packedEntities.Ptr[_archRow]);
                }
                else
                {
                    _t1.AdvanceArchetype(_archRow);
                }
                if (QueryParamInfo<TOption>.IsComponent) _tOption.SetRow(_archRow);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void Deconstruct(out Entity e, out ArchetypeRef<T1> c)

            {
                c = _t1;

                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                e = _query.Ref.world->entities.Ptr[arch.packedEntities.Ptr[_archRow]];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void Deconstruct(out Entity e, out ArchetypeRef<T1> c, out ArchetypeRef<TOption> opt)

            {
                c = _t1;
                opt = _tOption;

                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                e = _query.Ref.world->entities.Ptr[arch.packedEntities.Ptr[_archRow]];
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
            public void Update(ref World world, IntPtr data)

            {
                _range = *(Range*)(void*)data;

                _current = _range.start - 1;

                _archIdx = -1;
                _archRow = 0;
                _archEntityEnd = 0;

                if (_query.Ref.matchingArchetypes.length > 0)
                {
                    var remaining = _range.start;
                    for (int i = 0; i < _query.Ref.matchingArchetypes.length; i++)
                    {
                        ref var arch = ref world.UnsafeWorld->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[i]].Ref;
                        if (remaining < arch.count)
                        {
                            _archIdx = i;
                            SetupArchetypeRefs();
                            _archRow = remaining - 1;
                            break;
                        }
                        remaining -= arch.count;
                    }
                }
            }

            public void UpdateInner()

            {
                _current = _range.start - 1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public IntPtr GetData()
            {
                return (IntPtr)UnsafeStatic.to_ptr(ref _range);
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
        private ArchetypeRef<T1> _t1;
        private ArchetypeRef<T2> _t2;

        private ArchetypeRef<TOption> _tOption;

        private ptr<QueryUnsafe> _query;

        private int _current;

        private Range _range;

        private int _archIdx;
        private int _archRow;
        private int _archEntityEnd;

        static readonly bool T1IsPool = ComponentType<T1>.Data.storageType == StorageType.Pool;
        static readonly bool T2IsPool = ComponentType<T2>.Data.storageType == StorageType.Pool;
        static readonly bool TOptIsComponent = QueryParamInfo<TOption>.IsComponent;

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public readonly void Deconstruct(out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2)
        {
            c1 = _t1;
            c2 = _t2;
        }

        public readonly void Deconstruct(out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<TOption> opt)
        {
            c1 = _t1;
            c2 = _t2;
            opt = _tOption;
        }

        public Query<T1, T2, TOption> Current => this;

        public int Count

        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()

        {
            if (++_current >= _range.end) return false;

            if (++_archRow >= _archEntityEnd)
            {
                if (++_archIdx >= _query.Ref.matchingArchetypes.length) return false;
                SetupArchetypeRefs();
                return true;
            }

            AdvanceRefs();
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupArchetypeRefs()
        {
            ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
            _archRow = 0;
            _archEntityEnd = arch.count;
            SetupT1(ref arch);
            SetupT2(ref arch);
            if (QueryParamInfo<TOption>.IsComponent)
                SetupTOption(ref arch);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupT1(ref ArchetypeUnsafe arch)
        {
            if (T1IsPool)
                _t1.SetPool(_query.Ref.world->GetUntypedPool(ComponentType<T1>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
            else
            {
                var li = arch.GetComponentLocalIndex(ComponentType<T1>.Index);
                _t1.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupT2(ref ArchetypeUnsafe arch)
        {
            if (T2IsPool)
                _t2.SetPool(_query.Ref.world->GetUntypedPool(ComponentType<T2>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
            else
            {
                var li = arch.GetComponentLocalIndex(ComponentType<T2>.Index);
                _t2.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupTOption(ref ArchetypeUnsafe arch)
        {
            var li = arch.GetComponentLocalIndex(ComponentType<TOption>.Index);
            if (li >= 0)
                _tOption.Set(arch.data.Ptr, arch.GetComponentOffset(li), 0, arch.GetComponentSize(li));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdvanceRefs()
        {
            if (T1IsPool)
            {
                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                _t1.AdvancePool(arch.packedEntities.Ptr[_archRow]);
            }
            else
                _t1.AdvanceArchetype(_archRow);
            if (T2IsPool)
            {
                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                _t2.AdvancePool(arch.packedEntities.Ptr[_archRow]);
            }
            else
                _t2.AdvanceArchetype(_archRow);
            if (QueryParamInfo<TOption>.IsComponent) _tOption.SetRow(_archRow);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(ref World world, IntPtr data)

        {
            _range = *(Range*)(void*)data;

            _current = _range.start - 1;

            _archIdx = -1;
            _archRow = 0;
            _archEntityEnd = 0;

            if (_query.Ref.matchingArchetypes.length > 0)
            {
                var remaining = _range.start;
                for (int i = 0; i < _query.Ref.matchingArchetypes.length; i++)
                {
                    ref var arch = ref world.UnsafeWorld->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[i]].Ref;
                    if (remaining < arch.count)
                    {
                        _archIdx = i;
                        SetupArchetypeRefs();
                        _archRow = remaining - 1;
                        break;
                    }
                    remaining -= arch.count;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupArchetypeRefsFirst(ref World world)
        {
            ref var arch = ref world.UnsafeWorld->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[0]].Ref;
            _archEntityEnd = arch.count;
            SetupT1(ref arch);
            SetupT2(ref arch);
            if (QueryParamInfo<TOption>.IsComponent)
                SetupTOption(ref arch);
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
            private ArchetypeRef<T1> _t1;
            private ArchetypeRef<T2> _t2;

            private ArchetypeRef<TOption> _tOption;

            private ptr<QueryUnsafe> _query;

            private int _current;

            private Range _range;

            private int _archIdx;
            private int _archRow;
            private int _archEntityEnd;

            static readonly bool T1IsPool = ComponentType<T1>.Data.storageType == StorageType.Pool;
            static readonly bool T2IsPool = ComponentType<T2>.Data.storageType == StorageType.Pool;
            static readonly bool TOptIsComponent = QueryParamInfo<TOption>.IsComponent;

            public SystemParamMetaType MetaType => SystemParamMetaType.Query;

            public WithEntity Current => this;

            public int Count

            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _query.Ref.count;
            }

            public bool MoveNext()
            {
                if (++_current >= _range.end) return false;

                if (++_archRow >= _archEntityEnd)
                {
                    if (++_archIdx >= _query.Ref.matchingArchetypes.length) return false;
                    SetupArchetypeRefs();
                    return true;
                }

                AdvanceRefs();
                return true;
            }

            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetupArchetypeRefs()
            {
                if (_archIdx < 0 || _archIdx >= _query.Ref.matchingArchetypes.length)
                {
                    UnityEngine.Debug.LogError($"Arch index {_archIdx} is out of range. Len {_query.Ref.matchingArchetypes.length}.");
                    return;
                }

                var archIndex = _query.Ref.matchingArchetypes.Ptr[_archIdx];
                if (archIndex == 0)
                {
                    UnityEngine.Debug.LogError($"Arch is root.");
                }
                ref var arch = ref _query.Ref.world->archetypesList.Ptr[archIndex].Ref;
                _archRow = 0;
                _archEntityEnd = arch.count;
                SetupT1(ref arch);
                SetupT2(ref arch);
                if (QueryParamInfo<TOption>.IsComponent)
                    SetupTOption(ref arch);
            }

            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetupT1(ref ArchetypeUnsafe arch)
            {
                if (T1IsPool)
                    _t1.SetPool(_query.Ref.world->GetUntypedPool(ComponentType<T1>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
                else
                {
                    var li = arch.GetComponentLocalIndex(ComponentType<T1>.Index);
                    if (li < 0 || arch.componentOffsets.Ptr == null)
                    {
                        UnityEngine.Debug.LogError($"Archetype has no component {typeof(T1).Name}. {arch.ToString()}");
                    }
                    _t1.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetupT2(ref ArchetypeUnsafe arch)
            {
                if (T2IsPool)
                    _t2.SetPool(_query.Ref.world->GetUntypedPool(ComponentType<T2>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
                else
                {
                    var li = arch.GetComponentLocalIndex(ComponentType<T2>.Index);
                    _t2.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetupTOption(ref ArchetypeUnsafe arch)
            {
                var li = arch.GetComponentLocalIndex(ComponentType<TOption>.Index);
                if (li >= 0)
                    _tOption.Set(arch.data.Ptr, arch.GetComponentOffset(li), 0, arch.GetComponentSize(li));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void AdvanceRefs()
            {
                if (T1IsPool)
                {
                    ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                    _t1.AdvancePool(arch.packedEntities.Ptr[_archRow]);
                }
                else
                    _t1.AdvanceArchetype(_archRow);
                if (T2IsPool)
                {
                    ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                    _t2.AdvancePool(arch.packedEntities.Ptr[_archRow]);
                }
                else
                    _t2.AdvanceArchetype(_archRow);
                if (QueryParamInfo<TOption>.IsComponent) _tOption.SetRow(_archRow);
            }

            public readonly void Deconstruct(out Entity e, out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2)

            {
                c1 = _t1;
                c2 = _t2;

                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                e = _query.Ref.world->entities.Ptr[arch.packedEntities.Ptr[_archRow]];
            }

            public readonly void Deconstruct(out Entity e, out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<TOption> opt)

            {
                c1 = _t1;
                c2 = _t2;
                opt = _tOption;

                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                e = _query.Ref.world->entities.Ptr[arch.packedEntities.Ptr[_archRow]];
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

            public void Update(ref World world, IntPtr data)

            {
                _range = *(Range*)(void*)data;

                _current = _range.start - 1;

                _archIdx = -1;
                _archRow = 0;
                _archEntityEnd = 0;

                if (_query.Ref.matchingArchetypes.length > 0)
                {
                    var remaining = _range.start;
                    for (int i = 0; i < _query.Ref.matchingArchetypes.length; i++)
                    {
                        ref var arch = ref world.UnsafeWorld->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[i]].Ref;
                        if (remaining < arch.count)
                        {
                            _archIdx = i;
                            SetupArchetypeRefs();
                            _archRow = remaining - 1;
                            break;
                        }
                        remaining -= arch.count;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetupArchetypeRefsFirst(ref World world)
            {
                ref var arch = ref world.UnsafeWorld->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[0]].Ref;
                _archEntityEnd = arch.count;
                SetupT1(ref arch);
                SetupT2(ref arch);
                if (QueryParamInfo<TOption>.IsComponent)
                    SetupTOption(ref arch);
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
        private ArchetypeRef<T1> _t1;
        private ArchetypeRef<T2> _t2;
        private ArchetypeRef<T3> _t3;

        private ArchetypeRef<TOption> _tOption;

        private ptr<QueryUnsafe> _query;

        private int _current;

        private Range _range;

        private int _archIdx;
        private int _archRow;
        private int _archEntityEnd;

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public readonly void Deconstruct(out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<T3> c3)
        {
            c1 = _t1;
            c2 = _t2;
            c3 = _t3;
        }

        public readonly void Deconstruct(out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<T3> c3, out ArchetypeRef<TOption> opt)
        {
            c1 = _t1;
            c2 = _t2;
            c3 = _t3;
            opt = _tOption;
        }

        public Query<T1, T2, T3, TOption> Current => this;

        public int Count

        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()

        {
            if (++_current >= _range.end) return false;

            if (++_archRow >= _archEntityEnd)
            {
                _archIdx++;
                SetupArchetypeRefs();
                return true;
            }

            AdvanceRefs();
            return true;
        }

        static readonly bool T1IsPool = ComponentType<T1>.Data.storageType == StorageType.Pool;
        static readonly bool T2IsPool = ComponentType<T2>.Data.storageType == StorageType.Pool;
        static readonly bool T3IsPool = ComponentType<T3>.Data.storageType == StorageType.Pool;
        static readonly bool TOptIsComponent = QueryParamInfo<TOption>.IsComponent;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupT1(ref ArchetypeUnsafe arch)
        {
            if (T1IsPool)
                _t1.SetPool(_query.Ref.world->GetUntypedPool(ComponentType<T1>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
            else
            {
                var li = arch.GetComponentLocalIndex(ComponentType<T1>.Index);
                _t1.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupT2(ref ArchetypeUnsafe arch)
        {
            if (T2IsPool)
                _t2.SetPool(_query.Ref.world->GetUntypedPool(ComponentType<T2>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
            else
            {
                var li = arch.GetComponentLocalIndex(ComponentType<T2>.Index);
                _t2.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupT3(ref ArchetypeUnsafe arch)
        {
            if (T3IsPool)
                _t3.SetPool(_query.Ref.world->GetUntypedPool(ComponentType<T3>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
            else
            {
                var li = arch.GetComponentLocalIndex(ComponentType<T3>.Index);
                _t3.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupTOption(ref ArchetypeUnsafe arch)
        {
            var li = arch.GetComponentLocalIndex(ComponentType<TOption>.Index);
            if (li >= 0)
                _tOption.Set(arch.data.Ptr, arch.GetComponentOffset(li), 0, arch.GetComponentSize(li));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupArchetypeRefs()
        {
            ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
            _archRow = 0;
            _archEntityEnd = arch.count;
            SetupT1(ref arch);
            SetupT2(ref arch);
            SetupT3(ref arch);
            if (QueryParamInfo<TOption>.IsComponent)
                SetupTOption(ref arch);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdvanceRefs()
        {
            ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
            var eid = arch.packedEntities.Ptr[_archRow];
            if (T1IsPool) _t1.AdvancePool(eid); else _t1.AdvanceArchetype(_archRow);
            if (T2IsPool) _t2.AdvancePool(eid); else _t2.AdvanceArchetype(_archRow);
            if (T3IsPool) _t3.AdvancePool(eid); else _t3.AdvanceArchetype(_archRow);
            if (QueryParamInfo<TOption>.IsComponent) _tOption.SetRow(_archRow);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(ref World world, IntPtr data)

        {
            _range = *(Range*)(void*)data;

            _current = _range.start - 1;

            _archIdx = -1;
            _archRow = 0;
            _archEntityEnd = 0;

            if (_query.Ref.matchingArchetypes.length > 0)
            {
                var remaining = _range.start;
                for (int i = 0; i < _query.Ref.matchingArchetypes.length; i++)
                {
                    ref var arch = ref world.UnsafeWorld->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[i]].Ref;
                    if (remaining < arch.count)
                    {
                        _archIdx = i;
                        SetupArchetypeRefs();
                        _archRow = remaining - 1;
                        break;
                    }
                    remaining -= arch.count;
                }
            }
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
            private ArchetypeRef<T1> _t1;
            private ArchetypeRef<T2> _t2;
            private ArchetypeRef<T3> _t3;

            private ArchetypeRef<TOption> _tOption;

            private ptr<QueryUnsafe> _query;

            private int _current;

            private Range _range;

            private int _archIdx;
            private int _archRow;
            private int _archEntityEnd;

            public SystemParamMetaType MetaType => SystemParamMetaType.Query;

            public WithEntity Current => this;

            public int Count

            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _query.Ref.count;
            }

            public WithEntity GetEnumerator()
            {
                return this;
            }

            public bool MoveNext()

            {
                if (++_current >= _range.end) return false;

                if (++_archRow >= _archEntityEnd)
                {
                    _archIdx++;
                    SetupArchetypeRefs();
                    return true;
                }

                AdvanceRefs();
                return true;
            }

            static readonly bool T1IsPool = ComponentType<T1>.Data.storageType == StorageType.Pool;
            static readonly bool T2IsPool = ComponentType<T2>.Data.storageType == StorageType.Pool;
            static readonly bool T3IsPool = ComponentType<T3>.Data.storageType == StorageType.Pool;
            static readonly bool TOptIsComponent = QueryParamInfo<TOption>.IsComponent;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetupT1(ref ArchetypeUnsafe arch)
            {
                if (T1IsPool)
                    _t1.SetPool(_query.Ref.world->GetUntypedPool(ComponentType<T1>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
                else
                {
                    var li = arch.GetComponentLocalIndex(ComponentType<T1>.Index);
                    _t1.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
                }
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetupT2(ref ArchetypeUnsafe arch)
            {
                if (T2IsPool)
                    _t2.SetPool(_query.Ref.world->GetUntypedPool(ComponentType<T2>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
                else
                {
                    var li = arch.GetComponentLocalIndex(ComponentType<T2>.Index);
                    _t2.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
                }
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetupT3(ref ArchetypeUnsafe arch)
            {
                if (T3IsPool)
                    _t3.SetPool(_query.Ref.world->GetUntypedPool(ComponentType<T3>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
                else
                {
                    var li = arch.GetComponentLocalIndex(ComponentType<T3>.Index);
                    _t3.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
                }
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetupTOption(ref ArchetypeUnsafe arch)
            {
                var li = arch.GetComponentLocalIndex(ComponentType<TOption>.Index);
                if (li >= 0)
                    _tOption.Set(arch.data.Ptr, arch.GetComponentOffset(li), 0, arch.GetComponentSize(li));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetupArchetypeRefs()
            {
                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                _archRow = 0;
                _archEntityEnd = arch.count;
                SetupT1(ref arch);
                SetupT2(ref arch);
                SetupT3(ref arch);
                if (QueryParamInfo<TOption>.IsComponent)
                    SetupTOption(ref arch);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void AdvanceRefs()
            {
                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                var eid = arch.packedEntities.Ptr[_archRow];
                if (T1IsPool) _t1.AdvancePool(eid); else _t1.AdvanceArchetype(_archRow);
                if (T2IsPool) _t2.AdvancePool(eid); else _t2.AdvanceArchetype(_archRow);
                if (T3IsPool) _t3.AdvancePool(eid); else _t3.AdvanceArchetype(_archRow);
                if (QueryParamInfo<TOption>.IsComponent) _tOption.SetRow(_archRow);
            }

            public readonly void Deconstruct(out Entity e, out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<T3> c3)

            {
                c1 = _t1;
                c2 = _t2;
                c3 = _t3;

                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                e = _query.Ref.world->entities.Ptr[arch.packedEntities.Ptr[_archRow]];
            }

            public readonly void Deconstruct(out Entity e, out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<T3> c3,
                out ArchetypeRef<TOption> opt)

            {
                c1 = _t1;
                c2 = _t2;
                c3 = _t3;
                opt = _tOption;

                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                e = _query.Ref.world->entities.Ptr[arch.packedEntities.Ptr[_archRow]];
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

            public void Update(ref World world, IntPtr data)

            {
                _range = *(Range*)(void*)data;

                _current = _range.start - 1;

                _archIdx = -1;
                _archRow = 0;
                _archEntityEnd = 0;

                if (_query.Ref.matchingArchetypes.length > 0)
                {
                    var remaining = _range.start;
                    for (int i = 0; i < _query.Ref.matchingArchetypes.length; i++)
                    {
                        ref var arch = ref world.UnsafeWorld->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[i]].Ref;
                        if (remaining < arch.count)
                        {
                            _archIdx = i;
                            SetupArchetypeRefs();
                            _archRow = remaining - 1;
                            break;
                        }
                        remaining -= arch.count;
                    }
                }
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
        private ArchetypeRef<T1> _t1;
        private ArchetypeRef<T2> _t2;
        private ArchetypeRef<T3> _t3;
        private ArchetypeRef<T4> _t4;

        private ArchetypeRef<TOption> _tOption;

        private ptr<QueryUnsafe> _query;

        private int _current;

        private Range _range;

        private int _archIdx;
        private int _archRow;
        private int _archEntityEnd;

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public readonly void Deconstruct(out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<T3> c3, out ArchetypeRef<T4> c4)
        {
            c1 = _t1;
            c2 = _t2;
            c3 = _t3;
            c4 = _t4;
        }

        public readonly void Deconstruct(out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<T3> c3, out ArchetypeRef<T4> c4,
            out ArchetypeRef<TOption> opt)
        {
            c1 = _t1;
            c2 = _t2;
            c3 = _t3;
            c4 = _t4;
            opt = _tOption;
        }

        public Query<T1, T2, T3, T4, TOption> Current => this;

        public int Count

        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

        static readonly bool T1IsPool = ComponentType<T1>.Data.storageType == StorageType.Pool;
        static readonly bool T2IsPool = ComponentType<T2>.Data.storageType == StorageType.Pool;
        static readonly bool T3IsPool = ComponentType<T3>.Data.storageType == StorageType.Pool;
        static readonly bool T4IsPool = ComponentType<T4>.Data.storageType == StorageType.Pool;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupT1(ref ArchetypeUnsafe arch)
        {
            if (T1IsPool)
                _t1.SetPool(_query.Ref.world->GetUntypedPool(ComponentType<T1>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
            else
            {
                var li = arch.GetComponentLocalIndex(ComponentType<T1>.Index);
                _t1.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupT2(ref ArchetypeUnsafe arch)
        {
            if (T2IsPool)
                _t2.SetPool(_query.Ref.world->GetUntypedPool(ComponentType<T2>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
            else
            {
                var li = arch.GetComponentLocalIndex(ComponentType<T2>.Index);
                _t2.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupT3(ref ArchetypeUnsafe arch)
        {
            if (T3IsPool)
                _t3.SetPool(_query.Ref.world->GetUntypedPool(ComponentType<T3>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
            else
            {
                var li = arch.GetComponentLocalIndex(ComponentType<T3>.Index);
                _t3.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupT4(ref ArchetypeUnsafe arch)
        {
            if (T4IsPool)
                _t4.SetPool(_query.Ref.world->GetUntypedPool(ComponentType<T4>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
            else
            {
                var li = arch.GetComponentLocalIndex(ComponentType<T4>.Index);
                _t4.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupArchetypeRefs()
        {
            ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
            _archRow = 0;
            _archEntityEnd = arch.count;
            SetupT1(ref arch);
            SetupT2(ref arch);
            SetupT3(ref arch);
            SetupT4(ref arch);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdvanceRefs()
        {
            if (T1IsPool)
            {
                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                _t1.AdvancePool(arch.packedEntities.Ptr[_archRow]);
            }
            else
                _t1.AdvanceArchetype(_archRow);
            if (T2IsPool)
            {
                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                _t2.AdvancePool(arch.packedEntities.Ptr[_archRow]);
            }
            else
                _t2.AdvanceArchetype(_archRow);
            if (T3IsPool)
            {
                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                _t3.AdvancePool(arch.packedEntities.Ptr[_archRow]);
            }
            else
                _t3.AdvanceArchetype(_archRow);
            if (T4IsPool)
            {
                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                _t4.AdvancePool(arch.packedEntities.Ptr[_archRow]);
            }
            else
                _t4.AdvanceArchetype(_archRow);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()

        {
            if (++_current >= _range.end) return false;

            if (++_archRow >= _archEntityEnd)
            {
                _archIdx++;
                SetupArchetypeRefs();
                return true;
            }

            AdvanceRefs();
            return true;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(ref World world, IntPtr data)

        {
            _range = *(Range*)(void*)data;

            _current = _range.start - 1;

            _archIdx = -1;
            _archRow = 0;
            _archEntityEnd = 0;

            if (_query.Ref.matchingArchetypes.length > 0)
            {
                var remaining = _range.start;
                for (int i = 0; i < _query.Ref.matchingArchetypes.length; i++)
                {
                    ref var arch = ref world.UnsafeWorld->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[i]].Ref;
                    if (remaining < arch.count)
                    {
                        _archIdx = i;
                        SetupArchetypeRefs();
                        _archRow = remaining - 1;
                        break;
                    }
                    remaining -= arch.count;
                }
            }
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
            private ArchetypeRef<T1> _t1;
            private ArchetypeRef<T2> _t2;
            private ArchetypeRef<T3> _t3;
            private ArchetypeRef<T4> _t4;

            private ArchetypeRef<TOption> _tOption;

            private ptr<QueryUnsafe> _query;

            private int _current;

            private Range _range;

            private int _archIdx;
            private int _archRow;
            private int _archEntityEnd;

            static readonly bool T1IsPool = ComponentType<T1>.Data.storageType == StorageType.Pool;
            static readonly bool T2IsPool = ComponentType<T2>.Data.storageType == StorageType.Pool;
            static readonly bool T3IsPool = ComponentType<T3>.Data.storageType == StorageType.Pool;
            static readonly bool T4IsPool = ComponentType<T4>.Data.storageType == StorageType.Pool;

            public SystemParamMetaType MetaType => SystemParamMetaType.Query;

            public WithEntity Current => this;

            public int Count

            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _query.Ref.count;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetupT1(ref ArchetypeUnsafe arch)
            {
                if (T1IsPool)
                    _t1.SetPool(_query.Ref.world->GetUntypedPool(ComponentType<T1>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
                else
                {
                    var li = arch.GetComponentLocalIndex(ComponentType<T1>.Index);
                    _t1.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetupT2(ref ArchetypeUnsafe arch)
            {
                if (T2IsPool)
                    _t2.SetPool(_query.Ref.world->GetUntypedPool(ComponentType<T2>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
                else
                {
                    var li = arch.GetComponentLocalIndex(ComponentType<T2>.Index);
                    _t2.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetupT3(ref ArchetypeUnsafe arch)
            {
                if (T3IsPool)
                    _t3.SetPool(_query.Ref.world->GetUntypedPool(ComponentType<T3>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
                else
                {
                    var li = arch.GetComponentLocalIndex(ComponentType<T3>.Index);
                    _t3.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetupT4(ref ArchetypeUnsafe arch)
            {
                if (T4IsPool)
                    _t4.SetPool(_query.Ref.world->GetUntypedPool(ComponentType<T4>.Index).UnsafeBuffer->Chunks.Ptr, arch.packedEntities.Ptr[0]);
                else
                {
                    var li = arch.GetComponentLocalIndex(ComponentType<T4>.Index);
                    _t4.SetArchetype(arch.data.Ptr, arch.GetComponentOffset(li), arch.GetComponentSize(li));
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetupArchetypeRefs()
            {
                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                _archRow = 0;
                _archEntityEnd = arch.count;
                SetupT1(ref arch);
                SetupT2(ref arch);
                SetupT3(ref arch);
                SetupT4(ref arch);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void AdvanceRefs()
            {
                if (T1IsPool)
                {
                    ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                    _t1.AdvancePool(arch.packedEntities.Ptr[_archRow]);
                }
                else
                    _t1.AdvanceArchetype(_archRow);
                if (T2IsPool)
                {
                    ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                    _t2.AdvancePool(arch.packedEntities.Ptr[_archRow]);
                }
                else
                    _t2.AdvanceArchetype(_archRow);
                if (T3IsPool)
                {
                    ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                    _t3.AdvancePool(arch.packedEntities.Ptr[_archRow]);
                }
                else
                    _t3.AdvanceArchetype(_archRow);
                if (T4IsPool)
                {
                    ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                    _t4.AdvancePool(arch.packedEntities.Ptr[_archRow]);
                }
                else
                    _t4.AdvanceArchetype(_archRow);
            }

            public bool MoveNext()

            {
                if (++_current >= _range.end) return false;

                if (++_archRow >= _archEntityEnd)
                {
                    _archIdx++;
                    SetupArchetypeRefs();
                    return true;
                }

                AdvanceRefs();
                return true;
            }

            public readonly void Deconstruct(out Entity e, out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<T3> c3,
                out ArchetypeRef<T4> c4)

            {
                c1 = _t1;
                c2 = _t2;
                c3 = _t3;
                c4 = _t4;

                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                e = _query.Ref.world->entities.Ptr[arch.packedEntities.Ptr[_archRow]];
            }

            public readonly void Deconstruct(out Entity e, out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<T3> c3,
                out ArchetypeRef<T4> c4, out ArchetypeRef<TOption> opt)

            {
                c1 = _t1;
                c2 = _t2;
                c3 = _t3;
                c4 = _t4;
                opt = _tOption;

                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
                e = _query.Ref.world->entities.Ptr[arch.packedEntities.Ptr[_archRow]];
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

            public void Update(ref World world, IntPtr data)

            {
                _range = *(Range*)(void*)data;

                _current = _range.start - 1;

                _archIdx = -1;
                _archRow = 0;
                _archEntityEnd = 0;

                if (_query.Ref.matchingArchetypes.length > 0)
                {
                    var remaining = _range.start;
                    for (int i = 0; i < _query.Ref.matchingArchetypes.length; i++)
                    {
                        ref var arch = ref world.UnsafeWorld->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[i]].Ref;
                        if (remaining < arch.count)
                        {
                            _archIdx = i;
                            SetupArchetypeRefs();
                            _archRow = remaining - 1;
                            break;
                        }
                        remaining -= arch.count;
                    }
                }
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
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged, IComponent
        where TOption : unmanaged

    {
        private ArchetypeRef<T1> _t1;
        private ArchetypeRef<T2> _t2;
        private ArchetypeRef<T3> _t3;
        private ArchetypeRef<T4> _t4;
        private ArchetypeRef<T5> _t5;

        private ArchetypeRef<TOption> _tOption;

        private ptr<QueryUnsafe> _query;

        private int _current;

        private Range _range;

        private int _archIdx;
        private int _archRow;
        private int _archEntityEnd;

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public readonly void Deconstruct(out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<T3> c3,
            out ArchetypeRef<T4> c4, out ArchetypeRef<T5> c5)
        {
            c1 = _t1;
            c2 = _t2;
            c3 = _t3;
            c4 = _t4;
            c5 = _t5;
        }

        public readonly void Deconstruct(out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<T3> c3,
            out ArchetypeRef<T4> c4, out ArchetypeRef<T5> c5,
            out ArchetypeRef<TOption> opt)
        {
            c1 = _t1;
            c2 = _t2;
            c3 = _t3;
            c4 = _t4;
            c5 = _t5;
            opt = _tOption;
        }

        public Query<T1, T2, T3, T4, T5, TOption> Current => this;

        public int Count

        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()

        {
            if (++_current >= _range.end) return false;

            if (++_archRow >= _archEntityEnd)
            {
                _archIdx++;
                SetupArchetypeRefs();
                return true;
            }

            _t1.SetRow(_archRow);
            _t2.SetRow(_archRow);
            _t3.SetRow(_archRow);
            _t4.SetRow(_archRow);
            _t5.SetRow(_archRow);
            if (QueryParamInfo<TOption>.IsComponent) _tOption.SetRow(_archRow);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetupArchetypeRefs()
        {
            ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]].Ref;
            _archRow = 0;
            _archEntityEnd = arch.count;
            _t1.Set(arch.data.Ptr, arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T1>.Index)), 0,
                arch.GetComponentSize(arch.GetComponentLocalIndex(ComponentType<T1>.Index)));
            _t2.Set(arch.data.Ptr, arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T2>.Index)), 0,
                arch.GetComponentSize(arch.GetComponentLocalIndex(ComponentType<T2>.Index)));
            _t3.Set(arch.data.Ptr, arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T3>.Index)), 0,
                arch.GetComponentSize(arch.GetComponentLocalIndex(ComponentType<T3>.Index)));
            _t4.Set(arch.data.Ptr, arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T4>.Index)), 0,
                arch.GetComponentSize(arch.GetComponentLocalIndex(ComponentType<T4>.Index)));
            _t5.Set(arch.data.Ptr, arch.GetComponentOffset(arch.GetComponentLocalIndex(ComponentType<T5>.Index)), 0,
                arch.GetComponentSize(arch.GetComponentLocalIndex(ComponentType<T5>.Index)));
            if (QueryParamInfo<TOption>.IsComponent)
            {
                var localIdx = arch.GetComponentLocalIndex(ComponentType<TOption>.Index);
                if (localIdx >= 0)
                    _tOption.Set(arch.data.Ptr, arch.GetComponentOffset(localIdx), 0, arch.GetComponentSize(localIdx));
            }
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(ref World world, IntPtr data)

        {
            _range = *(Range*)(void*)data;

            _current = _range.start - 1;

            _archIdx = -1;
            _archRow = 0;
            _archEntityEnd = 0;

            if (_query.Ref.matchingArchetypes.length > 0)
            {
                var remaining = _range.start;
                for (int i = 0; i < _query.Ref.matchingArchetypes.length; i++)
                {
                    ref var arch = ref world.UnsafeWorld->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[i]].Ref;
                    if (remaining < arch.count)
                    {
                        _archIdx = i;
                        SetupArchetypeRefs();
                        _archRow = remaining - 1;
                        break;
                    }
                    remaining -= arch.count;
                }
            }
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
            private ArchetypeRef<T1> _t1;
            private ArchetypeRef<T2> _t2;
            private ArchetypeRef<T3> _t3;
            private ArchetypeRef<T4> _t4;
            private ArchetypeRef<T5> _t5;

            private ArchetypeRef<TOption> _tOption;

            private ptr<QueryUnsafe> _query;

            private int _current;

            private Range _range;

            private int _archIdx;
            private int _archRow;
            private int _archEntityEnd;

            public SystemParamMetaType MetaType => SystemParamMetaType.Query;

            public readonly void Deconstruct(out Entity e, out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2,
                out ArchetypeRef<T3> c3,
                out ArchetypeRef<T4> c4, out ArchetypeRef<T5> c5)

            {
                c1 = _t1;
                c2 = _t2;
                c3 = _t3;
                c4 = _t4;
                c5 = _t5;

                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]]
                    .Ref;
                e = _query.Ref.world->entities.Ptr[arch.packedEntities.Ptr[_archRow]];
            }

            public readonly void Deconstruct(out Entity e, out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2,
                out ArchetypeRef<T3> c3,
                out ArchetypeRef<T4> c4, out ArchetypeRef<T5> c5, out ArchetypeRef<TOption> opt)

            {
                c1 = _t1;
                c2 = _t2;
                c3 = _t3;
                c4 = _t4;
                c5 = _t5;
                opt = _tOption;

                ref var arch = ref _query.Ref.world->archetypesList.Ptr[_query.Ref.matchingArchetypes.Ptr[_archIdx]]
                    .Ref;
                e = _query.Ref.world->entities.Ptr[arch.packedEntities.Ptr[_archRow]];
            }

            public Query<T1, T2, T3, T4, T5, TOption>.WithEntity Current => this;

            public int Count

            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _query.Ref.count;
            }

            public bool MoveNext()

            {
                if (++_current >= _range.end) return false;

                var index = _query.Ref.GetEntityID(_current);

                _t1.index = index;
                _t2.index = index;
                _t3.index = index;
                _t4.index = index;
                _t5.index = index;

                _tOption.index = index;

                return true;
            }

            public void Init(ref ptr<World.WorldUnsafe> world)

            {
                _query = world.Ref.CreateQueryPtr();

                _query.Ref.With(ComponentType<T1>.Index);

                _query.Ref.With(ComponentType<T2>.Index);

                _query.Ref.With(ComponentType<T3>.Index);

                _query.Ref.With(ComponentType<T4>.Index);

                _query.Ref.With(ComponentType<T5>.Index);

                _t1.pool = world.Ref.GetPool<T1>().UnsafeBuffer;

                _t2.pool = world.Ref.GetPool<T2>().UnsafeBuffer;

                _t3.pool = world.Ref.GetPool<T3>().UnsafeBuffer;

                _t4.pool = world.Ref.GetPool<T4>().UnsafeBuffer;

                _t5.pool = world.Ref.GetPool<T5>().UnsafeBuffer;

                TOption option = default;

                switch (option)

                {
                    case IComponent _:

                        _query.Ref.With(ComponentType<TOption>.Index);

                        _tOption.pool = world.Ref.GetUntypedPool(ComponentType<TOption>.Index).UnsafeBuffer;

                        QueryParamInfo<TOption>.IsComponent = true;

                        _tOption.ResolveChunks();

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

            public void Update(ref World world, IntPtr data)

            {
                _range = *(Range*)(void*)data;

                _current = _range.start - 1;

                _t1.ResolveChunks();

                _t2.ResolveChunks();

                _t3.ResolveChunks();

                _t4.ResolveChunks();

                _t5.ResolveChunks();

                if (QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();
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
        private ArchetypeRef<T1> _t1;
        private ArchetypeRef<T2> _t2;
        private ArchetypeRef<T3> _t3;
        private ArchetypeRef<T4> _t4;
        private ArchetypeRef<T5> _t5;
        private ArchetypeRef<T6> _t6;

        private ArchetypeRef<TOption> _tOption;

        private ptr<QueryUnsafe> _query;

        private int _current;

        private Range _range;

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public readonly void Deconstruct(out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<T3> c3, out ArchetypeRef<T4> c4, out ArchetypeRef<T5> c5,
            out ArchetypeRef<T6> c6)

        {
            c1 = _t1;
            c2 = _t2;
            c3 = _t3;
            c4 = _t4;
            c5 = _t5;
            c6 = _t6;
        }

        public readonly void Deconstruct(out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<T3> c3, out ArchetypeRef<T4> c4, out ArchetypeRef<T5> c5,
            out ArchetypeRef<T6> c6, out ArchetypeRef<TOption> opt)

        {
            c1 = _t1;
            c2 = _t2;
            c3 = _t3;
            c4 = _t4;
            c5 = _t5;
            c6 = _t6;
            opt = _tOption;
        }

        public Query<T1, T2, T3, T4, T5, T6, TOption> Current => this;

        public int Count

        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

        public bool MoveNext()

        {
            if (++_current >= _range.end) return false;

            var index = _query.Ref.GetEntityID(_current);

            _t1.index = index;
            _t2.index = index;
            _t3.index = index;
            _t4.index = index;
            _t5.index = index;
            _t6.index = index;

            _tOption.index = index;

            return true;
        }

        public void Init(ref ptr<World.WorldUnsafe> world)

        {
            _query = world.Ref.CreateQueryPtr();

            _query.Ref.With(ComponentType<T1>.Index);
            _query.Ref.With(ComponentType<T2>.Index);
            _query.Ref.With(ComponentType<T3>.Index)
                ;
            _query.Ref.With(ComponentType<T4>.Index);
            _query.Ref.With(ComponentType<T5>.Index);
            _query.Ref.With(ComponentType<T6>.Index);

            _t1.pool = world.Ref.GetPool<T1>().UnsafeBuffer;

            _t2.pool = world.Ref.GetPool<T2>().UnsafeBuffer;

            _t3.pool = world.Ref.GetPool<T3>().UnsafeBuffer;

            _t4.pool = world.Ref.GetPool<T4>().UnsafeBuffer;

            _t5.pool = world.Ref.GetPool<T5>().UnsafeBuffer;

            _t6.pool = world.Ref.GetPool<T6>().UnsafeBuffer;

            TOption option = default;

            switch (option)

            {
                case IComponent _:

                    _query.Ref.With(ComponentType<TOption>.Index);

                    _tOption.pool = world.Ref.GetUntypedPool(ComponentType<TOption>.Index).UnsafeBuffer;

                    QueryParamInfo<TOption>.IsComponent = true;

                    _tOption.ResolveChunks();

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

        public void Update(ref World world, IntPtr data)

        {
            _range = *(Range*)(void*)data;

            _current = _range.start - 1;

            _t1.ResolveChunks();

            _t2.ResolveChunks();

            _t3.ResolveChunks();

            _t4.ResolveChunks();

            _t5.ResolveChunks();

            _t6.ResolveChunks();

            if (QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();
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
            private ArchetypeRef<T1> _t1;
            private ArchetypeRef<T2> _t2;
            private ArchetypeRef<T3> _t3;
            private ArchetypeRef<T4> _t4;
            private ArchetypeRef<T5> _t5;
            private ArchetypeRef<T6> _t6;

            private ArchetypeRef<TOption> _tOption;

            private ptr<QueryUnsafe> _query;

            private int _current;

            private Range _range;

            public SystemParamMetaType MetaType => SystemParamMetaType.Query;

            public WithEntity Current => this;

            public int Count

            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _query.Ref.count;
            }

            public bool MoveNext()

            {
                if (++_current >= _range.end) return false;

                var index = _query.Ref.GetEntityID(_current);

                _t1.index = index;
                _t2.index = index;
                _t3.index = index;
                _t4.index = index;
                _t5.index = index;
                _t6.index = index;

                _tOption.index = index;

                return true;
            }

            public readonly void Deconstruct(out Entity e, out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<T3> c3,
                out ArchetypeRef<T4> c4, out ArchetypeRef<T5> c5, out ArchetypeRef<T6> c6)

            {
                c1 = _t1;
                c2 = _t2;
                c3 = _t3;
                c4 = _t4;
                c5 = _t5;
                c6 = _t6;

                e = _query.cached->GetEntity(_current);
            }

            public readonly void Deconstruct(out Entity e, out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<T3> c3,
                out ArchetypeRef<T4> c4, out ArchetypeRef<T5> c5, out ArchetypeRef<T6> c6, out ArchetypeRef<TOption> opt)

            {
                c1 = _t1;
                c2 = _t2;
                c3 = _t3;
                c4 = _t4;
                c5 = _t5;
                c6 = _t6;
                opt = _tOption;

                e = _query.cached->GetEntity(_current);
            }

            public void Init(ref ptr<World.WorldUnsafe> world)

            {
                _query = world.Ref.CreateQueryPtr();

                _query.Ref.With(ComponentType<T1>.Index);
                _query.Ref.With(ComponentType<T2>.Index);
                _query.Ref.With(ComponentType<T3>.Index)
                    ;
                _query.Ref.With(ComponentType<T4>.Index);
                _query.Ref.With(ComponentType<T5>.Index);
                _query.Ref.With(ComponentType<T6>.Index);

                _t1.pool = world.Ref.GetPool<T1>().UnsafeBuffer;

                _t2.pool = world.Ref.GetPool<T2>().UnsafeBuffer;

                _t3.pool = world.Ref.GetPool<T3>().UnsafeBuffer;

                _t4.pool = world.Ref.GetPool<T4>().UnsafeBuffer;

                _t5.pool = world.Ref.GetPool<T5>().UnsafeBuffer;

                _t6.pool = world.Ref.GetPool<T6>().UnsafeBuffer;

                TOption option = default;

                switch (option)

                {
                    case IComponent _:

                        _query.Ref.With(ComponentType<TOption>.Index);

                        _tOption.pool = world.Ref.GetUntypedPool(ComponentType<TOption>.Index).UnsafeBuffer;

                        QueryParamInfo<TOption>.IsComponent = true;

                        _tOption.ResolveChunks();

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

            public void Update(ref World world, IntPtr data)

            {
                _range = *(Range*)(void*)data;

                _current = _range.start - 1;

                _t1.ResolveChunks();

                _t2.ResolveChunks();

                _t3.ResolveChunks();

                _t4.ResolveChunks();

                _t5.ResolveChunks();

                _t6.ResolveChunks();

                if (QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();
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
        private ArchetypeRef<T1> _t1;
        private ArchetypeRef<T2> _t2;
        private ArchetypeRef<T3> _t3;
        private ArchetypeRef<T4> _t4;
        private ArchetypeRef<T5> _t5;
        private ArchetypeRef<T6> _t6;
        private ArchetypeRef<T7> _t7;

        private ArchetypeRef<TOption> _tOption;

        private ptr<QueryUnsafe> _query;

        private int _current;

        private Range _range;

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public readonly void Deconstruct(out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<T3> c3, out ArchetypeRef<T4> c4, out ArchetypeRef<T5> c5,
            out ArchetypeRef<T6> c6, out ArchetypeRef<T7> c7)

        {
            c1 = _t1;
            c2 = _t2;
            c3 = _t3;
            c4 = _t4;
            c5 = _t5;
            c6 = _t6;
            c7 = _t7;
        }

        public readonly void Deconstruct(out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<T3> c3, out ArchetypeRef<T4> c4, out ArchetypeRef<T5> c5,
            out ArchetypeRef<T6> c6, out ArchetypeRef<T7> c7, out ArchetypeRef<TOption> opt)

        {
            c1 = _t1;
            c2 = _t2;
            c3 = _t3;
            c4 = _t4;
            c5 = _t5;
            c6 = _t6;
            c7 = _t7;
            opt = _tOption;
        }

        public Query<T1, T2, T3, T4, T5, T6, T7, TOption> Current => this;

        public int Count

        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

        public bool MoveNext()

        {
            if (++_current >= _range.end) return false;

            var index = _query.Ref.GetEntityID(_current);

            _t1.index = index;
            _t2.index = index;
            _t3.index = index;
            _t4.index = index;
            _t5.index = index;
            _t6.index = index;
            _t7.index = index;

            _tOption.index = index;

            return true;
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

            _t1.pool = world.Ref.GetPool<T1>().UnsafeBuffer;

            _t2.pool = world.Ref.GetPool<T2>().UnsafeBuffer;

            _t3.pool = world.Ref.GetPool<T3>().UnsafeBuffer;

            _t4.pool = world.Ref.GetPool<T4>().UnsafeBuffer;

            _t5.pool = world.Ref.GetPool<T5>().UnsafeBuffer;

            _t6.pool = world.Ref.GetPool<T6>().UnsafeBuffer;

            _t7.pool = world.Ref.GetPool<T7>().UnsafeBuffer;

            TOption option = default;

            switch (option)

            {
                case IComponent _:

                    _query.Ref.With(ComponentType<TOption>.Index);

                    _tOption.pool = world.Ref.GetUntypedPool(ComponentType<TOption>.Index).UnsafeBuffer;

                    QueryParamInfo<TOption>.IsComponent = true;

                    _tOption.ResolveChunks();

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

        public void Update(ref World world, IntPtr data)

        {
            _range = *(Range*)(void*)data;

            _current = _range.start - 1;

            _t1.ResolveChunks();

            _t2.ResolveChunks();

            _t3.ResolveChunks();

            _t4.ResolveChunks();

            _t5.ResolveChunks();

            _t6.ResolveChunks();

            _t7.ResolveChunks();

            if (QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();
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
            private ArchetypeRef<T1> _t1;
            private ArchetypeRef<T2> _t2;
            private ArchetypeRef<T3> _t3;
            private ArchetypeRef<T4> _t4;
            private ArchetypeRef<T5> _t5;
            private ArchetypeRef<T6> _t6;
            private ArchetypeRef<T7> _t7;

            private ArchetypeRef<TOption> _tOption;

            private ptr<QueryUnsafe> _query;

            private int _current;

            private Range _range;

            public SystemParamMetaType MetaType => SystemParamMetaType.Query;

            public WithEntity Current => this;

            public int Count

            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _query.Ref.count;
            }

            public bool MoveNext()

            {
                if (++_current >= _range.end) return false;

                var index = _query.Ref.GetEntityID(_current);

                _t1.index = index;
                _t2.index = index;
                _t3.index = index;
                _t4.index = index;
                _t5.index = index;
                _t6.index = index;
                _t7.index = index;

                _tOption.index = index;

                return true;
            }

            public readonly void Deconstruct(out Entity e, out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<T3> c3,
                out ArchetypeRef<T4> c4, out ArchetypeRef<T5> c5, out ArchetypeRef<T6> c6, out ArchetypeRef<T7> c7)

            {
                c1 = _t1;
                c2 = _t2;
                c3 = _t3;
                c4 = _t4;
                c5 = _t5;
                c6 = _t6;
                c7 = _t7;

                e = _query.cached->GetEntity(_current);
            }

            public readonly void Deconstruct(out Entity e, out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<T3> c3,
                out ArchetypeRef<T4> c4, out ArchetypeRef<T5> c5, out ArchetypeRef<T6> c6, out ArchetypeRef<T7> c7, out ArchetypeRef<TOption> opt)

            {
                c1 = _t1;
                c2 = _t2;
                c3 = _t3;
                c4 = _t4;
                c5 = _t5;
                c6 = _t6;
                c7 = _t7;
                opt = _tOption;

                e = _query.cached->GetEntity(_current);
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

                _t1.pool = world.Ref.GetPool<T1>().UnsafeBuffer;

                _t2.pool = world.Ref.GetPool<T2>().UnsafeBuffer;

                _t3.pool = world.Ref.GetPool<T3>().UnsafeBuffer;

                _t4.pool = world.Ref.GetPool<T4>().UnsafeBuffer;

                _t5.pool = world.Ref.GetPool<T5>().UnsafeBuffer;

                _t6.pool = world.Ref.GetPool<T6>().UnsafeBuffer;

                _t7.pool = world.Ref.GetPool<T7>().UnsafeBuffer;

                TOption option = default;

                switch (option)

                {
                    case IComponent _:

                        _query.Ref.With(ComponentType<TOption>.Index);

                        _tOption.pool = world.Ref.GetUntypedPool(ComponentType<TOption>.Index).UnsafeBuffer;

                        QueryParamInfo<TOption>.IsComponent = true;

                        _tOption.ResolveChunks();

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

            public void Update(ref World world, IntPtr data)

            {
                _range = *(Range*)(void*)data;

                _current = _range.start - 1;

                _t1.ResolveChunks();

                _t2.ResolveChunks();

                _t3.ResolveChunks();

                _t4.ResolveChunks();

                _t5.ResolveChunks();

                _t6.ResolveChunks();

                _t7.ResolveChunks();

                if (QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();
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
        private ArchetypeRef<T1> _t1;
        private ArchetypeRef<T2> _t2;
        private ArchetypeRef<T3> _t3;
        private ArchetypeRef<T4> _t4;

        private ArchetypeRef<T5> _t5;
        private ArchetypeRef<T6> _t6;
        private ArchetypeRef<T7> _t7;
        private ArchetypeRef<T8> _t8;

        private ArchetypeRef<TOption> _tOption;

        private ptr<QueryUnsafe> _query;

        private int _current;

        private Range _range;

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public readonly void Deconstruct(out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<T3> c3, out ArchetypeRef<T4> c4,
            out ArchetypeRef<T5> c5, out ArchetypeRef<T6> c6, out ArchetypeRef<T7> c7, out ArchetypeRef<T8> c8)

        {
            c1 = _t1;
            c2 = _t2;
            c3 = _t3;
            c4 = _t4;
            c5 = _t5;
            c6 = _t6;
            c7 = _t7;
            c8 = _t8;
        }

        public readonly void Deconstruct(out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<T3> c3, out ArchetypeRef<T4> c4,
            out ArchetypeRef<T5> c5, out ArchetypeRef<T6> c6, out ArchetypeRef<T7> c7, out ArchetypeRef<T8> c8, out ArchetypeRef<TOption> opt)

        {
            c1 = _t1;
            c2 = _t2;
            c3 = _t3;
            c4 = _t4;
            c5 = _t5;
            c6 = _t6;
            c7 = _t7;
            c8 = _t8;
            opt = _tOption;
        }

        public Query<T1, T2, T3, T4, T5, T6, T7, T8, TOption> Current => this;

        public int Count

        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count;
        }

        public bool MoveNext()

        {
            if (++_current >= _range.end) return false;

            var index = _query.Ref.GetEntityID(_current);

            _t1.index = index;
            _t2.index = index;
            _t3.index = index;
            _t4.index = index;

            _t5.index = index;
            _t6.index = index;
            _t7.index = index;
            _t8.index = index;

            _tOption.index = index;

            return true;
        }

        public void Init(ref ptr<World.WorldUnsafe> world)

        {
            _query = world.Ref.CreateQueryPtr();

            _query.Ref.With(ComponentType<T1>.Index);
            _query.Ref.With(ComponentType<T2>.Index);
            _query.Ref.With(ComponentType<T3>.Index)
                ;
            _query.Ref.With(ComponentType<T4>.Index);
            _query.Ref.With(ComponentType<T5>.Index);
            _query.Ref.With(ComponentType<T6>.Index)
                ;
            _query.Ref.With(ComponentType<T7>.Index);
            _query.Ref.With(ComponentType<T8>.Index);

            _t1.pool = world.Ref.GetPool<T1>().UnsafeBuffer;

            _t2.pool = world.Ref.GetPool<T2>().UnsafeBuffer;

            _t3.pool = world.Ref.GetPool<T3>().UnsafeBuffer;

            _t4.pool = world.Ref.GetPool<T4>().UnsafeBuffer;

            _t5.pool = world.Ref.GetPool<T5>().UnsafeBuffer;

            _t6.pool = world.Ref.GetPool<T6>().UnsafeBuffer;

            _t7.pool = world.Ref.GetPool<T7>().UnsafeBuffer;

            _t8.pool = world.Ref.GetPool<T8>().UnsafeBuffer;

            TOption option = default;

            switch (option)

            {
                case IComponent _:

                    _query.Ref.With(ComponentType<TOption>.Index);

                    _tOption.pool = world.Ref.GetUntypedPool(ComponentType<TOption>.Index).UnsafeBuffer;

                    QueryParamInfo<TOption>.IsComponent = true;

                    _tOption.ResolveChunks();

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

        public void Update(ref World world, IntPtr data)

        {
            _range = *(Range*)(void*)data;

            _current = _range.start - 1;

            _t1.ResolveChunks();

            _t2.ResolveChunks();

            _t3.ResolveChunks();

            _t4.ResolveChunks();

            _t5.ResolveChunks();

            _t6.ResolveChunks();

            _t7.ResolveChunks();

            _t8.ResolveChunks();

            if (QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();
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
            private ArchetypeRef<T1> _t1;
            private ArchetypeRef<T2> _t2;
            private ArchetypeRef<T3> _t3;
            private ArchetypeRef<T4> _t4;

            private ArchetypeRef<T5> _t5;
            private ArchetypeRef<T6> _t6;
            private ArchetypeRef<T7> _t7;
            private ArchetypeRef<T8> _t8;

            private ArchetypeRef<TOption> _tOption;

            private ptr<QueryUnsafe> _query;

            private int _current;

            private Range _range;

            public SystemParamMetaType MetaType => SystemParamMetaType.Query;

            public WithEntity Current => this;

            public int Count

            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _query.Ref.count;
            }

            public bool MoveNext()

            {
                if (++_current >= _range.end) return false;

                var index = _query.Ref.GetEntityID(_current);

                _t1.index = index;
                _t2.index = index;
                _t3.index = index;
                _t4.index = index;

                _t5.index = index;
                _t6.index = index;
                _t7.index = index;
                _t8.index = index;

                _tOption.index = index;

                return true;
            }

            public readonly void Deconstruct(out Entity e, out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<T3> c3,
                out ArchetypeRef<T4> c4,
                out ArchetypeRef<T5> c5, out ArchetypeRef<T6> c6, out ArchetypeRef<T7> c7, out ArchetypeRef<T8> c8)

            {
                c1 = _t1;
                c2 = _t2;
                c3 = _t3;
                c4 = _t4;
                c5 = _t5;
                c6 = _t6;
                c7 = _t7;
                c8 = _t8;

                e = _query.cached->GetEntity(_current);
            }

            public readonly void Deconstruct(out Entity e, out ArchetypeRef<T1> c1, out ArchetypeRef<T2> c2, out ArchetypeRef<T3> c3,
                out ArchetypeRef<T4> c4,
                out ArchetypeRef<T5> c5, out ArchetypeRef<T6> c6, out ArchetypeRef<T7> c7, out ArchetypeRef<T8> c8, out ArchetypeRef<TOption> opt)

            {
                c1 = _t1;
                c2 = _t2;
                c3 = _t3;
                c4 = _t4;
                c5 = _t5;
                c6 = _t6;
                c7 = _t7;
                c8 = _t8;
                opt = _tOption;

                e = _query.cached->GetEntity(_current);
            }

            public void Init(ref ptr<World.WorldUnsafe> world)

            {
                _query = world.Ref.CreateQueryPtr();

                _query.Ref.With(ComponentType<T1>.Index);
                _query.Ref.With(ComponentType<T2>.Index);
                _query.Ref.With(ComponentType<T3>.Index)
                    ;
                _query.Ref.With(ComponentType<T4>.Index);
                _query.Ref.With(ComponentType<T5>.Index);
                _query.Ref.With(ComponentType<T6>.Index)
                    ;
                _query.Ref.With(ComponentType<T7>.Index);
                _query.Ref.With(ComponentType<T8>.Index);

                _t1.pool = world.Ref.GetPool<T1>().UnsafeBuffer;

                _t2.pool = world.Ref.GetPool<T2>().UnsafeBuffer;

                _t3.pool = world.Ref.GetPool<T3>().UnsafeBuffer;

                _t4.pool = world.Ref.GetPool<T4>().UnsafeBuffer;

                _t5.pool = world.Ref.GetPool<T5>().UnsafeBuffer;

                _t6.pool = world.Ref.GetPool<T6>().UnsafeBuffer;

                _t7.pool = world.Ref.GetPool<T7>().UnsafeBuffer;

                _t8.pool = world.Ref.GetPool<T8>().UnsafeBuffer;

                TOption option = default;

                switch (option)

                {
                    case IComponent _:

                        _query.Ref.With(ComponentType<TOption>.Index);

                        _tOption.pool = world.Ref.GetUntypedPool(ComponentType<TOption>.Index).UnsafeBuffer;

                        QueryParamInfo<TOption>.IsComponent = true;

                        _tOption.ResolveChunks();

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

            public void Update(ref World world, IntPtr data)

            {
                _range = *(Range*)(void*)data;

                _current = _range.start - 1;

                _t1.ResolveChunks();

                _t2.ResolveChunks();

                _t3.ResolveChunks();

                _t4.ResolveChunks();

                _t5.ResolveChunks();

                _t6.ResolveChunks();

                _t7.ResolveChunks();

                _t8.ResolveChunks();

                if (QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();
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
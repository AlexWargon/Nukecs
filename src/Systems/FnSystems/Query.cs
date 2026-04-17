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
        private Ref<T1> _t1;
        private ptr<QueryUnsafe> _query;
        private Range _range;
        private int _current;

        public readonly void Deconstruct(out Ref<T1> c) => c = _t1;
        public ref Entity GetEntity(int index) => ref _query.cached->GetEntity(index);
        public ref T1 Current => ref _t1.Val;
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count; 
        }
        public bool MoveNext()
        {
            _current++;
            var index = _query.Ref.GetEntityID(_current);
            _t1.index = index;
            return _current < _range.end;
        }

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();
            _query.Ref.With(ComponentType<T1>.Index);
            _t1.pool = world.Ref.GetPool<T1>().UnsafeBuffer;

        }

        public void Update(ref World world, IntPtr data)
        {
            _range = *(Range*)data;
            _current = _range.start - 1;
            _t1.pool = world.GetPool<T1>().UnsafeBuffer;
            _t1.ResolveChunks();
        }

        public IntPtr GetData() => (IntPtr)UnsafeUtility.AddressOf(ref _range);

        public bool TryGetQuery(out ptr<QueryUnsafe> query)
        {
            query = _query;
            return true;
        }

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public struct WithEntity : IQuery, ISystemParam
        {
            private Ref<T1> _t1;
            private ptr<QueryUnsafe> _query;
            private Range _range;
            private int _current;

            public SystemParamMetaType MetaType => SystemParamMetaType.Query;
            public ref Entity GetEntity(int index) => ref _query.cached->GetEntity(index);

            public WithEntity Current => this;
            public int Count
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _query.Ref.count; 
            }
            public bool MoveNext()
            {
                _current++;
                var index = _query.Ref.GetEntityID(_current);
                _t1.index = index;
                return _current < _range.end;
            }

            public readonly void Deconstruct(out Entity e, out Ref<T1> c)
            {
                c = _t1;
                e = _query.cached->GetEntity(_current);
            }

            public void Init(ref ptr<World.WorldUnsafe> world)
            {
                _query = world.Ref.CreateQueryPtr();
                _query.Ref.With(ComponentType<T1>.Index);
                _t1.pool = world.Ref.GetPool<T1>().UnsafeBuffer;

            }

            public void Update(ref World world, IntPtr data)
            {
                _range = *(Range*)(void*)data;
                _current = _range.start - 1;
                _t1.ResolveChunks();

            }

            public IntPtr GetData() => (IntPtr)UnsafeUtility.AddressOf(ref _range);

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
        private Ref<T1> _t1;
        private Ref<TOption> _tOption;
        private ptr<QueryUnsafe> _query;
        private int _current;
        private Range _range;

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public readonly void Deconstruct(out Ref<T1> c) => c = _t1;
        public readonly void Deconstruct(out Ref<T1> c, out Ref<TOption> opt) { c = _t1; opt = _tOption; }
        public Query<T1, TOption> Current => this;
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count; 
        }
        public bool MoveNext()
        {
            _current++;
            _t1.index = _query.Ref.entities.Ptr[_current];
            _tOption.index = _t1.index;
            return _current < _range.end;
        }

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();
            _query.Ref.With(ComponentType<T1>.Index);

            _t1.pool = world.Ref.GetPool<T1>().UnsafeBuffer;


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
                    for (int i = 0; i < tuple.Length; i++)
                        if (tuple[i] is IFilter f) f.Setup(_query.Ptr);
                    break;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(ref World world, IntPtr data)
        {
            _range = *(Range*)(void*)data;
            _current = _range.start - 1;
            _t1.ResolveChunks();


                    if(QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();
        }

        public IntPtr GetData() => (IntPtr)UnsafeUtility.AddressOf(ref _range);

        public bool TryGetQuery(out ptr<QueryUnsafe> query)
        {
            query = _query;
            return true;
        }
        
        [BurstCompile]
        public struct WithEntity : IQuery, ISystemParam
        {
            private Ref<T1> _t1;
            private Ref<TOption> _tOption;
            private ptr<QueryUnsafe> _query;
            private int _current;
            private Range _range;
            public void SetRange(Range range) => _range = range;
            public SystemParamMetaType MetaType => SystemParamMetaType.Query;

            public WithEntity Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)][BurstCompile]
                get => this;
            }

            public int Count
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _query.Ref.count; 
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)][BurstCompile]
            public bool MoveNext()
            {
                if (++_current >= _range.end) return false;
                _t1.index = _query.Ref.entities.Ptr[_current];
                _tOption.index = _t1.index;
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void Deconstruct(out Entity e, out Ref<T1> c)
            {
                c = _t1; 
                e = _query.cached->GetEntity(_current);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly void Deconstruct(out Entity e, out Ref<T1> c, out Ref<TOption> opt)
            {
                c = _t1; opt = _tOption;
                e = _query.cached->GetEntity(_current);
            }

            public void Init(ref ptr<World.WorldUnsafe> world)
            {
                _query = world.Ref.CreateQueryPtr();
                _query.Ref.With(ComponentType<T1>.Index);
                _t1.pool = world.Ref.GetPool<T1>().UnsafeBuffer;


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
                        for (int i = 0; i < tuple.Length; i++)
                            if (tuple[i] is IFilter f) f.Setup(_query.Ptr);
                        break;
                }
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)][BurstCompile]
            public void Update(ref World world, IntPtr data)
            {
                _range = *(Range*)(void*)data;
                _current = _range.start - 1;
                _t1.ResolveChunks();


                        if(QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();
            }

            public void UpdateInner()
            {
                _current = _range.start - 1;


            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public IntPtr GetData() => (IntPtr)UnsafeStatic.to_ptr(ref _range);

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
        private Ref<T1> _t1; private Ref<T2> _t2;
        private Ref<TOption> _tOption;
        private ptr<QueryUnsafe> _query;
        private int _current;
        private Range _range;

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public readonly void Deconstruct(out Ref<T1> c1, out Ref<T2> c2) { c1 = _t1; c2 = _t2; }
        public readonly void Deconstruct(out Ref<T1> c1, out Ref<T2> c2, out Ref<TOption> opt) { c1 = _t1; c2 = _t2; opt = _tOption; }
        public Query<T1,T2,TOption> Current => this;
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count; 
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            _current++;
            var index = _query.Ref.GetEntityID(_current);
            _t1.index = index; _t2.index = index;
            _tOption.index = index;
            return _current < _range.end;
        }

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();
            _query.Ref.With(ComponentType<T1>.Index);
            _query.Ref.With(ComponentType<T2>.Index);

            _t1.pool = world.Ref.GetPool<T1>().UnsafeBuffer;

            _t2.pool = world.Ref.GetPool<T2>().UnsafeBuffer;


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
                    for (int i = 0; i < tuple.Length; i++)
                        if (tuple[i] is IFilter f) f.Setup(_query.Ptr);
                    break;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(ref World world, IntPtr data)
        {
            _range = *(Range*)(void*)data;
            _current = _range.start - 1;
            _t1.ResolveChunks();
            _t2.ResolveChunks();


                    if(QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();
        }

        public IntPtr GetData() => (IntPtr)UnsafeUtility.AddressOf(ref _range);

        public bool TryGetQuery(out ptr<QueryUnsafe> query)
        {
            query = _query;
            return true;
        }

        public struct WithEntity : IQuery, ISystemParam
        {
            private Ref<T1> _t1; private Ref<T2> _t2;
            private Ref<TOption> _tOption;
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
                _current++;
                var index = _query.Ref.GetEntityID(_current);
                _t1.index = index; _t2.index = index;
                _tOption.index = index;
                return _current < _range.end;
            }

            public readonly void Deconstruct(out Entity e, out Ref<T1> c1, out Ref<T2> c2)
            {
                c1 = _t1; c2 = _t2;
                e = _query.cached->GetEntity(_current);
            }
            public readonly void Deconstruct(out Entity e, out Ref<T1> c1, out Ref<T2> c2, out Ref<TOption> opt)
            {
                c1 = _t1; c2 = _t2; opt = _tOption;
                e = _query.cached->GetEntity(_current);
            }

            public void Init(ref ptr<World.WorldUnsafe> world)
            {
                _query = world.Ref.CreateQueryPtr();
                _query.Ref.With(ComponentType<T1>.Index);
                _query.Ref.With(ComponentType<T2>.Index);
                _t1.pool = world.Ref.GetPool<T1>().UnsafeBuffer;

                _t2.pool = world.Ref.GetPool<T2>().UnsafeBuffer;


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
                        for (int i = 0; i < tuple.Length; i++)
                            if (tuple[i] is IFilter f) f.Setup(_query.Ptr);
                        break;
                }
            }

            public void Update(ref World world, IntPtr data)
            {
                _range = *(Range*)(void*)data;
                _current = _range.start - 1;
                _t1.ResolveChunks();
                _t2.ResolveChunks();


                        if(QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();
            }

            public IntPtr GetData() => (IntPtr)UnsafeUtility.AddressOf(ref _range);

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
        private Ref<T1> _t1; private Ref<T2> _t2; private Ref<T3> _t3;
        private Ref<TOption> _tOption;
        private ptr<QueryUnsafe> _query;
        private int _current;
        private Range _range;

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public readonly void Deconstruct(out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3) { c1 = _t1; c2 = _t2; c3 = _t3; }
        public readonly void Deconstruct(out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<TOption> opt) { c1 = _t1; c2 = _t2; c3 = _t3; opt = _tOption; }
        public Query<T1,T2,T3,TOption> Current => this;
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count; 
        }
        public bool MoveNext()
        {
            _current++;
            var index = _query.Ref.GetEntityID(_current);
            _t1.index = index; _t2.index = index; _t3.index = index;
            _tOption.index = index;
            return _current < _range.end;
        }

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();
            _query.Ref.With(ComponentType<T1>.Index);
            _query.Ref.With(ComponentType<T2>.Index);
            _query.Ref.With(ComponentType<T3>.Index);

            _t1.pool = world.Ref.GetPool<T1>().UnsafeBuffer;

            _t2.pool = world.Ref.GetPool<T2>().UnsafeBuffer;

            _t3.pool = world.Ref.GetPool<T3>().UnsafeBuffer;


            TOption option = default;
            switch (option)
            {
                case IComponent _:
                    _query.Ref.With(ComponentType<TOption>.Index);
                    _tOption.pool = world.Ref.GetUntypedPool(ComponentType<TOption>.Index).UnsafeBuffer;
                    _tOption.ResolveChunks();
                    QueryParamInfo<TOption>.IsComponent = true;
                    break;
                case IFilter filter:
                    filter.Setup(_query.Ptr);
                    break;
                case ITuple tuple:
                    for (int i = 0; i < tuple.Length; i++)
                        if (tuple[i] is IFilter f) f.Setup(_query.Ptr);
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
            if(QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();

        }

        public IntPtr GetData() => (IntPtr)UnsafeUtility.AddressOf(ref _range);

        public bool TryGetQuery(out ptr<QueryUnsafe> query) { query = _query; return true; }

        public struct WithEntity : IQuery, ISystemParam
        {
            private Ref<T1> _t1; private Ref<T2> _t2; private Ref<T3> _t3;
            private Ref<TOption> _tOption;
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
            public WithEntity GetEnumerator() => this;
            public bool MoveNext()
            {
                _current++;
                var index = _query.Ref.GetEntityID(_current);
                _t1.index = index; _t2.index = index; _t3.index = index;
                _tOption.index = index;
                return _current < _range.end;
            }

            public readonly void Deconstruct(out Entity e, out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3)
            {
                c1 = _t1; c2 = _t2; c3 = _t3;
                e = _query.cached->GetEntity(_current);
            }
            public readonly void Deconstruct(out Entity e, out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<TOption> opt)
            {
                c1 = _t1; c2 = _t2; c3 = _t3; opt = _tOption;
                e = _query.cached->GetEntity(_current);
            }

            public void Init(ref ptr<World.WorldUnsafe> world)
            {
                _query = world.Ref.CreateQueryPtr();
                _query.Ref.With(ComponentType<T1>.Index);
                _query.Ref.With(ComponentType<T2>.Index);
                _query.Ref.With(ComponentType<T3>.Index);
                _t1.pool = world.Ref.GetPool<T1>().UnsafeBuffer;

                    _t2.pool = world.Ref.GetPool<T2>().UnsafeBuffer;

                    _t3.pool = world.Ref.GetPool<T3>().UnsafeBuffer;


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
                        for (int i = 0; i < tuple.Length; i++)
                            if (tuple[i] is IFilter f) f.Setup(_query.Ptr);
                        break;
                }
            }

            public void Update(ref World world, IntPtr data)
            {
                _range = *(Range*)(void*)data;
                _current = _range.start - 1;
                
                _t1.pool = world.GetPool<T1>().UnsafeBuffer;
                _t1.ResolveChunks();

                _t2.pool = world.GetPool<T2>().UnsafeBuffer;
                _t2.ResolveChunks();

                _t3.pool = world.GetPool<T3>().UnsafeBuffer;
                _t3.ResolveChunks();
                        if(QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();
            }

            public IntPtr GetData() => (IntPtr)UnsafeUtility.AddressOf(ref _range);

            public bool TryGetQuery(out ptr<QueryUnsafe> query) { query = _query; return true; }
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
        private Ref<T1> _t1; private Ref<T2> _t2; private Ref<T3> _t3; private Ref<T4> _t4;
        private Ref<TOption> _tOption;
        private ptr<QueryUnsafe> _query;
        private int _current;
        private Range _range;

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public readonly void Deconstruct(out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4) { c1 = _t1; c2 = _t2; c3 = _t3; c4 = _t4; }
        public readonly void Deconstruct(out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4, out Ref<TOption> opt) { c1 = _t1; c2 = _t2; c3 = _t3; c4 = _t4; opt = _tOption; }

        public Query<T1,T2,T3,T4,TOption> Current => this;
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count; 
        }
        public bool MoveNext()
        {
            _current++;
            var index = _query.Ref.GetEntityID(_current);
            _t1.index = index; _t2.index = index; _t3.index = index; _t4.index = index;
            _tOption.index = index;
            return _current < _range.end;
        }

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();
            _query.Ref.With(ComponentType<T1>.Index);
            _query.Ref.With(ComponentType<T2>.Index);
            _query.Ref.With(ComponentType<T3>.Index);
            _query.Ref.With(ComponentType<T4>.Index);

            _t1.pool = world.Ref.GetPool<T1>().UnsafeBuffer;

            _t2.pool = world.Ref.GetPool<T2>().UnsafeBuffer;

            _t3.pool = world.Ref.GetPool<T3>().UnsafeBuffer;

            _t4.pool = world.Ref.GetPool<T4>().UnsafeBuffer;


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
                    for (int i = 0; i < tuple.Length; i++)
                        if (tuple[i] is IFilter f) f.Setup(_query.Ptr);
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


                    if(QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();
        }

        public IntPtr GetData() => (IntPtr)UnsafeUtility.AddressOf(ref _range);
        public bool TryGetQuery(out ptr<QueryUnsafe> query) { query = _query; return true; }

        public struct WithEntity : IQuery, ISystemParam
        {
            private Ref<T1> _t1; private Ref<T2> _t2; private Ref<T3> _t3; private Ref<T4> _t4;
            private Ref<TOption> _tOption;
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
                _current++;
                var index = _query.Ref.GetEntityID(_current);
                _t1.index = index; _t2.index = index; _t3.index = index; _t4.index = index;
                _tOption.index = index;
                return _current < _range.end;
            }

            public readonly void Deconstruct(out Entity e, out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4)
            {
                c1 = _t1; c2 = _t2; c3 = _t3; c4 = _t4;
                e = _query.cached->GetEntity(_current);
            }
            public readonly void Deconstruct(out Entity e, out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4, out Ref<TOption> opt)
            {
                c1 = _t1; c2 = _t2; c3 = _t3; c4 = _t4; opt = _tOption;
                e = _query.cached->GetEntity(_current);
            }

            public void Init(ref ptr<World.WorldUnsafe> world)
            {
                _query = world.Ref.CreateQueryPtr();
                _query.Ref.With(ComponentType<T1>.Index);
                _query.Ref.With(ComponentType<T2>.Index);
                _query.Ref.With(ComponentType<T3>.Index);
                _query.Ref.With(ComponentType<T4>.Index);
                _t1.pool = world.Ref.GetPool<T1>().UnsafeBuffer;

                _t2.pool = world.Ref.GetPool<T2>().UnsafeBuffer;

                _t3.pool = world.Ref.GetPool<T3>().UnsafeBuffer;

                _t4.pool = world.Ref.GetPool<T4>().UnsafeBuffer;


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
                        for (int i = 0; i < tuple.Length; i++)
                            if (tuple[i] is IFilter f) f.Setup(_query.Ptr);
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


                        if(QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();
            }

            public IntPtr GetData() => (IntPtr)UnsafeUtility.AddressOf(ref _range);
            public bool TryGetQuery(out ptr<QueryUnsafe> query) { query = _query; return true; }
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
        private Ref<T1> _t1; private Ref<T2> _t2; private Ref<T3> _t3; private Ref<T4> _t4; private Ref<T5> _t5;
        private Ref<TOption> _tOption;
        private ptr<QueryUnsafe> _query;
        private int _current;
        private Range _range;

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public readonly void Deconstruct(out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4, out Ref<T5> c5) { c1 = _t1; c2 = _t2; c3 = _t3; c4 = _t4; c5 = _t5; }
        public readonly void Deconstruct(out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4, out Ref<T5> c5, out Ref<TOption> opt) { c1 = _t1; c2 = _t2; c3 = _t3; c4 = _t4; c5 = _t5; opt = _tOption; }

        public Query<T1,T2,T3,T4,T5,TOption> Current => this;
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count; 
        }
        public bool MoveNext()
        {
            _current++;
            var index = _query.Ref.GetEntityID(_current);
            _t1.index = index; _t2.index = index; _t3.index = index; _t4.index = index; _t5.index = index;
            _tOption.index = index;
            return _current < _range.end;
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
                    for (int i = 0; i < tuple.Length; i++)
                        if (tuple[i] is IFilter f) f.Setup(_query.Ptr);
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


                    if(QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();
        }

        public IntPtr GetData() => (IntPtr)UnsafeUtility.AddressOf(ref _range);
        public bool TryGetQuery(out ptr<QueryUnsafe> query) { query = _query; return true; }

        public struct WithEntity : IQuery, ISystemParam
        {
            private Ref<T1> _t1; private Ref<T2> _t2; private Ref<T3> _t3; private Ref<T4> _t4; private Ref<T5> _t5;
            private Ref<TOption> _tOption;
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
                _current++;
                var index = _query.Ref.GetEntityID(_current);
                _t1.index = index; _t2.index = index; _t3.index = index; _t4.index = index; _t5.index = index;
                _tOption.index = index;
                return _current < _range.end;
            }

            public readonly void Deconstruct(out Entity e, out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4, out Ref<T5> c5)
            {
                c1 = _t1; c2 = _t2; c3 = _t3; c4 = _t4; c5 = _t5;
                e = _query.cached->GetEntity(_current);
            }
            public readonly void Deconstruct(out Entity e, out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4, out Ref<T5> c5, out Ref<TOption> opt)
            {
                c1 = _t1; c2 = _t2; c3 = _t3; c4 = _t4; c5 = _t5; opt = _tOption;
                e = _query.cached->GetEntity(_current);
            }

            public void Init(ref ptr<World.WorldUnsafe> world)
            {
                _query = world.Ref.CreateQueryPtr();
                _query.Ref.With(ComponentType<T1>.Index);_query.Ref.With(ComponentType<T2>.Index);_query.Ref.With(ComponentType<T3>.Index);_query.Ref.With(ComponentType<T4>.Index);_query.Ref.With(ComponentType<T5>.Index);
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
                        for (int i = 0; i < tuple.Length; i++)
                            if (tuple[i] is IFilter f) f.Setup(_query.Ptr);
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


                        if(QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();
            }

            public IntPtr GetData() => (IntPtr)UnsafeUtility.AddressOf(ref _range);
            public bool TryGetQuery(out ptr<QueryUnsafe> query) { query = _query; return true; }
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
        private Ref<T1> _t1; private Ref<T2> _t2; private Ref<T3> _t3; private Ref<T4> _t4; private Ref<T5> _t5; private Ref<T6> _t6;
        private Ref<TOption> _tOption;
        private ptr<QueryUnsafe> _query;
        private int _current;
        private Range _range;

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public readonly void Deconstruct(out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4, out Ref<T5> c5, out Ref<T6> c6)
        { c1 = _t1; c2 = _t2; c3 = _t3; c4 = _t4; c5 = _t5; c6 = _t6; }

        public readonly void Deconstruct(out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4, out Ref<T5> c5, out Ref<T6> c6, out Ref<TOption> opt)
        { c1 = _t1; c2 = _t2; c3 = _t3; c4 = _t4; c5 = _t5; c6 = _t6; opt = _tOption; }

        public Query<T1,T2,T3,T4,T5,T6,TOption> Current => this;
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count; 
        }
        public bool MoveNext()
        {
            _current++;
            var index = _query.Ref.GetEntityID(_current);
            _t1.index = index; _t2.index = index; _t3.index = index; _t4.index = index; _t5.index = index; _t6.index = index;
            _tOption.index = index;
            return _current < _range.end;
        }

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();
            _query.Ref.With(ComponentType<T1>.Index);_query.Ref.With(ComponentType<T2>.Index);_query.Ref.With(ComponentType<T3>.Index)
                ;_query.Ref.With(ComponentType<T4>.Index);_query.Ref.With(ComponentType<T5>.Index);_query.Ref.With(ComponentType<T6>.Index);

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
                    for (int i = 0; i < tuple.Length; i++)
                        if (tuple[i] is IFilter f) f.Setup(_query.Ptr);
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


                    if(QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();
        }

        public IntPtr GetData() => (IntPtr)UnsafeUtility.AddressOf(ref _range);
        public bool TryGetQuery(out ptr<QueryUnsafe> query) { query = _query; return true; }

        public struct WithEntity : IQuery, ISystemParam
        {
            private Ref<T1> _t1; private Ref<T2> _t2; private Ref<T3> _t3; private Ref<T4> _t4; private Ref<T5> _t5; private Ref<T6> _t6;
            private Ref<TOption> _tOption;
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
                _current++;
                var index = _query.Ref.GetEntityID(_current);
                _t1.index = index; _t2.index = index; _t3.index = index; _t4.index = index; _t5.index = index; _t6.index = index;
                _tOption.index = index;
                return _current < _range.end;
            }

            public readonly void Deconstruct(out Entity e, out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4, out Ref<T5> c5, out Ref<T6> c6)
            {
                c1 = _t1; c2 = _t2; c3 = _t3; c4 = _t4; c5 = _t5; c6 = _t6;
                e = _query.cached->GetEntity(_current);
            }
            public readonly void Deconstruct(out Entity e, out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4, out Ref<T5> c5, out Ref<T6> c6, out Ref<TOption> opt)
            {
                c1 = _t1; c2 = _t2; c3 = _t3; c4 = _t4; c5 = _t5; c6 = _t6; opt = _tOption;
                e = _query.cached->GetEntity(_current);
            }

            public void Init(ref ptr<World.WorldUnsafe> world)
            {
                _query = world.Ref.CreateQueryPtr();
                _query.Ref.With(ComponentType<T1>.Index);_query.Ref.With(ComponentType<T2>.Index);_query.Ref.With(ComponentType<T3>.Index)
                    ;_query.Ref.With(ComponentType<T4>.Index);_query.Ref.With(ComponentType<T5>.Index);_query.Ref.With(ComponentType<T6>.Index);

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
                        for (int i = 0; i < tuple.Length; i++)
                            if (tuple[i] is IFilter f) f.Setup(_query.Ptr);
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


                        if(QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();
            }

            public IntPtr GetData() => (IntPtr)UnsafeUtility.AddressOf(ref _range);
            public bool TryGetQuery(out ptr<QueryUnsafe> query) { query = _query; return true; }
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
        private Ref<T1> _t1; private Ref<T2> _t2; private Ref<T3> _t3; private Ref<T4> _t4; private Ref<T5> _t5; private Ref<T6> _t6; private Ref<T7> _t7;
        private Ref<TOption> _tOption;
        private ptr<QueryUnsafe> _query;
        private int _current;
        private Range _range;

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public readonly void Deconstruct(out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4, out Ref<T5> c5, out Ref<T6> c6, out Ref<T7> c7)
        { c1 = _t1; c2 = _t2; c3 = _t3; c4 = _t4; c5 = _t5; c6 = _t6; c7 = _t7; }

        public readonly void Deconstruct(out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4, out Ref<T5> c5, out Ref<T6> c6, out Ref<T7> c7, out Ref<TOption> opt)
        { c1 = _t1; c2 = _t2; c3 = _t3; c4 = _t4; c5 = _t5; c6 = _t6; c7 = _t7; opt = _tOption; }

        public Query<T1,T2,T3,T4,T5,T6,T7,TOption> Current => this;
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count; 
        }
        public bool MoveNext()
        {
            _current++;
            var index = _query.Ref.GetEntityID(_current);
            _t1.index = index; _t2.index = index; _t3.index = index; _t4.index = index; _t5.index = index; _t6.index = index; _t7.index = index;
            _tOption.index = index;
            return _current < _range.end;
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
                    for (int i = 0; i < tuple.Length; i++)
                        if (tuple[i] is IFilter f) f.Setup(_query.Ptr);
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


                    if(QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();
        }

        public IntPtr GetData() => (IntPtr)UnsafeUtility.AddressOf(ref _range);
        public bool TryGetQuery(out ptr<QueryUnsafe> query) { query = _query; return true; }

        public struct WithEntity : IQuery, ISystemParam
        {
            private Ref<T1> _t1; private Ref<T2> _t2; private Ref<T3> _t3; private Ref<T4> _t4; private Ref<T5> _t5; private Ref<T6> _t6; private Ref<T7> _t7;
            private Ref<TOption> _tOption;
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
                _current++;
                var index = _query.Ref.GetEntityID(_current);
                _t1.index = index; _t2.index = index; _t3.index = index; _t4.index = index; _t5.index = index; _t6.index = index; _t7.index = index;
                _tOption.index = index;
                return _current < _range.end;
            }

            public readonly void Deconstruct(out Entity e, out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4, out Ref<T5> c5, out Ref<T6> c6, out Ref<T7> c7)
            {
                c1 = _t1; c2 = _t2; c3 = _t3; c4 = _t4; c5 = _t5; c6 = _t6; c7 = _t7;
                e = _query.cached->GetEntity(_current);
            }
            public readonly void Deconstruct(out Entity e, out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4, out Ref<T5> c5, out Ref<T6> c6, out Ref<T7> c7, out Ref<TOption> opt)
            {
                c1 = _t1; c2 = _t2; c3 = _t3; c4 = _t4; c5 = _t5; c6 = _t6; c7 = _t7; opt = _tOption;
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
                        for (int i = 0; i < tuple.Length; i++)
                            if (tuple[i] is IFilter f) f.Setup(_query.Ptr);
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


                        if(QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();
            }

            public IntPtr GetData() => (IntPtr)UnsafeUtility.AddressOf(ref _range);
            public bool TryGetQuery(out ptr<QueryUnsafe> query) { query = _query; return true; }
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
        private Ref<T1> _t1; private Ref<T2> _t2; private Ref<T3> _t3; private Ref<T4> _t4;
        private Ref<T5> _t5; private Ref<T6> _t6; private Ref<T7> _t7; private Ref<T8> _t8;
        private Ref<TOption> _tOption;
        private ptr<QueryUnsafe> _query;
        private int _current;
        private Range _range;

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;

        public readonly void Deconstruct(out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4,
                                         out Ref<T5> c5, out Ref<T6> c6, out Ref<T7> c7, out Ref<T8> c8)
        { c1 = _t1; c2 = _t2; c3 = _t3; c4 = _t4; c5 = _t5; c6 = _t6; c7 = _t7; c8 = _t8; }

        public readonly void Deconstruct(out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4,
                                         out Ref<T5> c5, out Ref<T6> c6, out Ref<T7> c7, out Ref<T8> c8, out Ref<TOption> opt)
        { c1 = _t1; c2 = _t2; c3 = _t3; c4 = _t4; c5 = _t5; c6 = _t6; c7 = _t7; c8 = _t8; opt = _tOption; }

        public Query<T1,T2,T3,T4,T5,T6,T7,T8,TOption> Current => this;
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _query.Ref.count; 
        }
        public bool MoveNext()
        {
            _current++;
            var index = _query.Ref.GetEntityID(_current);
            _t1.index = index; _t2.index = index; _t3.index = index; _t4.index = index;
            _t5.index = index; _t6.index = index; _t7.index = index; _t8.index = index;
            _tOption.index = index;
            return _current < _range.end;
        }

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();
            _query.Ref.With(ComponentType<T1>.Index);_query.Ref.With(ComponentType<T2>.Index);_query.Ref.With(ComponentType<T3>.Index)
                ;_query.Ref.With(ComponentType<T4>.Index);_query.Ref.With(ComponentType<T5>.Index);_query.Ref.With(ComponentType<T6>.Index)
                ;_query.Ref.With(ComponentType<T7>.Index);_query.Ref.With(ComponentType<T8>.Index);

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
                    for (int i = 0; i < tuple.Length; i++)
                        if (tuple[i] is IFilter f) f.Setup(_query.Ptr);
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



                    if(QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();
        }

        public IntPtr GetData() => (IntPtr)UnsafeUtility.AddressOf(ref _range);
        public bool TryGetQuery(out ptr<QueryUnsafe> query) { query = _query; return true; }

        public struct WithEntity : IQuery, ISystemParam
        {
            private Ref<T1> _t1; private Ref<T2> _t2; private Ref<T3> _t3; private Ref<T4> _t4;
            private Ref<T5> _t5; private Ref<T6> _t6; private Ref<T7> _t7; private Ref<T8> _t8;
            private Ref<TOption> _tOption;
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
                _current++;
                var index = _query.Ref.GetEntityID(_current);
                _t1.index = index; _t2.index = index; _t3.index = index; _t4.index = index;
                _t5.index = index; _t6.index = index; _t7.index = index; _t8.index = index;
                _tOption.index = index;
                return _current < _range.end;
            }

            public readonly void Deconstruct(out Entity e, out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4,
                                             out Ref<T5> c5, out Ref<T6> c6, out Ref<T7> c7, out Ref<T8> c8)
            {
                c1 = _t1; c2 = _t2; c3 = _t3; c4 = _t4; c5 = _t5; c6 = _t6; c7 = _t7; c8 = _t8;
                e = _query.cached->GetEntity(_current);
            }

            public readonly void Deconstruct(out Entity e, out Ref<T1> c1, out Ref<T2> c2, out Ref<T3> c3, out Ref<T4> c4,
                                             out Ref<T5> c5, out Ref<T6> c6, out Ref<T7> c7, out Ref<T8> c8, out Ref<TOption> opt)
            {
                c1 = _t1; c2 = _t2; c3 = _t3; c4 = _t4; c5 = _t5; c6 = _t6; c7 = _t7; c8 = _t8; opt = _tOption;
                e = _query.cached->GetEntity(_current);
            }

            public void Init(ref ptr<World.WorldUnsafe> world)
            {
                _query = world.Ref.CreateQueryPtr();
                _query.Ref.With(ComponentType<T1>.Index);_query.Ref.With(ComponentType<T2>.Index);_query.Ref.With(ComponentType<T3>.Index)
                    ;_query.Ref.With(ComponentType<T4>.Index);_query.Ref.With(ComponentType<T5>.Index);_query.Ref.With(ComponentType<T6>.Index)
                    ;_query.Ref.With(ComponentType<T7>.Index);_query.Ref.With(ComponentType<T8>.Index);

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
                        for (int i = 0; i < tuple.Length; i++)
                            if (tuple[i] is IFilter f) f.Setup(_query.Ptr);
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



                        if(QueryParamInfo<TOption>.IsComponent) _tOption.ResolveChunks();
            }

            public IntPtr GetData() => (IntPtr)UnsafeUtility.AddressOf(ref _range);
            public bool TryGetQuery(out ptr<QueryUnsafe> query) { query = _query; return true; }
        }
    }

    public struct QueryParamInfo<T>
    {
        private static readonly SharedStatic<byte> isComponent = SharedStatic<byte>.GetOrCreate<QueryParamInfo<T>>();

        public static bool IsComponent
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => isComponent.Data == 1;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => isComponent.Data = value ? (byte)1 : (byte)0;
        }
    }
    
}

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using static Wargon.Nukecs.UnsafeStatic;
namespace Wargon.Nukecs {
    public interface IQuery {
        ref Entity GetEntity(int index);
    }

    public static class QueryGenericExtensions {
        // internal static void FetchRange<TQuery>(this TQuery query, int start, int end)
        //     where TQuery : unmanaged, IQuery {
        //     query.Range = new Range(start, end);
        // }

        public static TQuery GetEnumerator<TQuery>(this TQuery query) where TQuery : unmanaged, IQuery {
            return query;
        }

        // public static bool MoveNext<TQuery>(this TQuery query)  where TQuery : unmanaged, IQuery {
        //     var range = query.Range;
        //     return false;
        // }
    }

    public enum SystemParamMetaType
    {
        None = 0,
        Query = 1,
        World = 2,
        Single = 3,
        Service = 4
    }
    public interface ISystemParam {
        SystemParamMetaType MetaType { get; }
        void Init(ref ptr<World.WorldUnsafe> world);
        void Update(ref World world, IntPtr data);
        IntPtr GetData();
    }
    public readonly struct Range {
        public readonly int start;
        public readonly int end;

        public Range(int start, int end) {
            this.start = start;
            this.end = end;
        }
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Query<T1> : IQuery, ISystemParam where T1 : unmanaged, IComponent {
        private Ref<T1> _t1;
        private ptr<QueryUnsafe> _query;
        private Range _range;
        private int _current;
        public readonly void Deconstruct(out Ref<T1> c) {
            c = _t1;
        }
        public ref Entity GetEntity(int index) => ref _query.cached->GetEntity(index);

        public ref T1 Current => ref _t1.Val;

        public bool MoveNext() {
            _current++;
            var index = _query.Ref.GetEntityID(_current);
            _t1.index = index;
            return _current < _range.end;
        }
        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();
            _query.Ref.With(ComponentType<T1>.Index);
            _t1.pool = world.Ref.GetPool<T1>().UnsafeBufferPtr.Ref.Chunks.Ptr;
        }

        public void Update(ref World world, IntPtr data) {
            _range = *(Range*)(void*)data;
            _current = _range.start - 1;
        }

        public IntPtr GetData()
        {
            var r = malloc<Range>(Allocator.Temp);
            *r = new Range(0, _query.Ref.count);
            return (IntPtr)r;
        }

        public SystemParamMetaType MetaType => SystemParamMetaType.Query;
        public struct WithEntity : IQuery, ISystemParam {
            private Ref<T1> _t1;
            private ptr<QueryUnsafe> _query;
            private Range _range;
            private int _current;
            public SystemParamMetaType MetaType => SystemParamMetaType.Query;
            public ref Entity GetEntity(int index) => ref _query.cached->GetEntity(index);
            
            public WithEntity Current => this;
            public bool MoveNext() {
                _current++;
                var index = _query.Ref.GetEntityID(_current);
                _t1.index = index;
                return _current < _range.end;
            }
            public readonly void Deconstruct(out Entity e, out Ref<T1> c) {
                c = _t1;
                e = _query.cached->GetEntity(_current);
            }
            public void Init(ref ptr<World.WorldUnsafe> world) {
                _query = world.Ref.CreateQueryPtr();
                _query.Ref.With(ComponentType<T1>.Index);
                _t1.pool = world.Ref.GetPool<T1>().UnsafeBufferPtr.Ref.Chunks.Ptr;
            }

            public void Update(ref World world, IntPtr data) {
                _range = *(Range*)(void*)data;
                _current = _range.start - 1;
            }
            public IntPtr GetData()
            {
                var r = malloc<Range>(Allocator.Temp);
                *r = new Range(0, _query.Ref.count);
                return (IntPtr)r;
            }
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Query<T1, TOption> : ISystemParam 
        where T1 : unmanaged, IComponent 
        where TOption : unmanaged{
        private Ref<T1> _t1;
        private Ref<TOption> _tOption;
        private ptr<QueryUnsafe> _query;
        private int _current;
        private Range _range;
        public SystemParamMetaType MetaType => SystemParamMetaType.Query;
        public readonly void Deconstruct(out Ref<T1> c) {
            c = _t1;
        }
        public readonly void Deconstruct(out Ref<T1> c, out Ref<TOption> opt) {
            c = _t1;
            opt = _tOption;
        }

        public Query<T1, TOption> GetEnumerator() => this;
        public Query<T1, TOption> Current => this;

        public bool MoveNext() {
            _current++;
            var index = _query.Ref.GetEntityID(_current);
            _t1.index = index;
            _tOption.index = index;
            return _current < _range.end;
        }
        public void Init(ref ptr<World.WorldUnsafe> world) {
            _query = world.Ref.CreateQueryPtr();
            _query.Ref.With(ComponentType<T1>.Index);
            TOption option = default;
            switch (option) {
                case IComponent c:
                    _query.Ref.With(ComponentType<TOption>.Index);
                    break;
                case IFilter filter:
                    filter.Setup(_query.Ptr);
                    break;
                case ITuple tuple: {
                    for (int i = 0; i < tuple.Length; i++) {
                        var type = tuple[i];
                        if (type is IFilter f) {
                            f.Setup(_query.Ptr);
                        }
                    }
                    break;
                }
            }
            _t1.pool = world.Ref.GetPool<T1>().UnsafeBufferPtr.Ref.Chunks.Ptr;
        }

        public void Update(ref World world, IntPtr data) {
            _range = *(Range*)(void*)data;
            _current = _range.start - 1;
        }
        public IntPtr GetData()
        {
            var r = malloc<Range>(Allocator.Temp);
            *r = new Range(0, _query.Ref.count);
            return (IntPtr)r;
        }
        public struct WithEntity : ISystemParam {
            private Ref<T1> _t1;
            private Ref<TOption> _tOption;
            private ptr<QueryUnsafe> _query;
            private int _current;
            private Range _range;
            public SystemParamMetaType MetaType => SystemParamMetaType.Query;
            public WithEntity GetEnumerator() => this;
            public WithEntity Current => this;
            public bool MoveNext() {
                _current++;
                var index = _query.Ref.GetEntityID(_current);
                _t1.index = index;
                _tOption.index = index;
                return _current < _range.end;
            }
            public readonly void Deconstruct(out Entity e, out Ref<T1> c) {
                c = _t1;
                e = default;
            }
            public readonly void Deconstruct(out Entity e, out Ref<T1> c, out Ref<TOption> opt) {
                c = _t1;
                opt = _tOption;
                e = _query.cached->GetEntity(_current);
            }

            public void Init(ref ptr<World.WorldUnsafe> world) {
                var q = world.Ref.CreateQueryPtr();
                q.Ref.With(ComponentType<T1>.Index);
                TOption option = default;
                switch (option) {
                    case IComponent c:
                        q.Ref.With(ComponentType<TOption>.Index);
                        break;
                    case IFilter filter:
                        filter.Setup(q.Ptr);
                        break;
                    case ITuple tuple: {
                        for (var i = 0; i < tuple.Length; i++) {
                            var type = tuple[i];
                            if (type is IFilter f) {
                                f.Setup(q.Ptr);
                            }
                        }
                        break;
                    }
                }
            }

            public void Update(ref World world, IntPtr data)
            {
                _range = *(Range*)(void*)data;
                _current = _range.start - 1;
            }
            public IntPtr GetData()
            {
                var r = malloc<Range>(Allocator.Temp);
                *r = new Range(0, _query.Ref.count);
                return (IntPtr)r;
            }
        }

    }

    
}
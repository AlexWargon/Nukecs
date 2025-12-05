using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wargon.Nukecs {
    public interface IQuery {
        ref Entity GetEntity(int index);
        Range Range { get; set; }
        void FetchData();
        int GetCurrent();
    }

    public static class QueryGenericExtensions {
        internal static void FetchRange<TQuery>(this TQuery query, int start, int end)
            where TQuery : unmanaged, IQuery {
            query.Range = new Range(start, end);
        }

        public static TQuery GetEnumerator<TQuery>(this TQuery query) where TQuery : unmanaged, IQuery {
            return query;
        }

        public static bool MoveNext<TQuery>(this TQuery query)  where TQuery : unmanaged, IQuery {
            var range = query.Range;
            return false;
        }
    }
    public interface ISystemParam {
        void Init(ref World world);
        void Update(ref World world);
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
        private Rf<T1> _t1;
        private ptr<QueryUnsafe> _query;
        private Range _range;
        public Range Range {
            get => _range;
            set => _range = value;
        }
        public int GetCurrent() {
            return _current;
        }
        private int _current;
        public readonly void Deconstruct(out Rf<T1> c) {
            c = _t1;
        }

        public ref Entity GetEntity(int index) => ref _query.cached->GetEntity(index);
        
        public void UpdateRange(Range range) {
            _range = range;
            _current = _range.start - 1;
        }
        public Query<T1> Current => this;

        public void FetchData() {
            
        }
        public bool MoveNext() {
            _current++;
            var index = _query.Ref.GetEntityID(_current);
            _t1.index = index;
            return _current < _query.Ref.count;
        }
        public struct WithEntity : IQuery, ISystemParam {
            private Rf<T1> _t1;
            private ptr<QueryUnsafe> _query;
            private Range _range;
            private int _current;

            public Range Range {
                get => _range;
                set => _range = value;
            }
            
            public ref Entity GetEntity(int index) => ref _query.cached->GetEntity(index);
            public void FetchData() {
            
            }

            public int GetCurrent() {
                return _current;
            }

            public void UpdateRange(Range range) {
                _range = range;
                _current = range.start - 1;
            }
            
            public WithEntity Current => this;
            // public bool MoveNext() {
            //     _current++;
            //     var index = _query.Ref.GetEntityID(_current);
            //     _t1.index = index;
            //     return _current < _range.end;
            // }
            public readonly void Deconstruct(out Entity e, out Rf<T1> c) {
                c = _t1;
                e = _query.cached->GetEntity(_current);
            }
            public void Init(ref World world) {
                var q = world.unsafeWorldPtr.Ref.CreateQueryPtr();
                q.Ref.With(ComponentType<T1>.Index);
                _t1.pool = world.GetPool<T1>().UnsafeBufferPtr.Ref.Chunks.Ptr;
            }

            public void Update(ref World world) {
                
            }
        }
        public void Init(ref World world) {
            var q = world.unsafeWorldPtr.Ref.CreateQueryPtr();
            q.Ref.With(ComponentType<T1>.Index);
        }

        public void Update(ref World world) {
            
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct Query<T1, TOption> : ISystemParam where T1 : unmanaged where TOption : unmanaged{
        private Rf<T1> _t1;
        private Rf<TOption> _tOption;
        private ptr<QueryUnsafe> _query;
        private int _current;
        public readonly void Deconstruct(out Rf<T1> c) {
            c = _t1;
        }
        public readonly void Deconstruct(out Rf<T1> c, out Rf<TOption> opt) {
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
            return _current < _query.Ref.count;
        }
        
        public unsafe struct WithEntity : ISystemParam {
            private Rf<T1> _t1;
            private Rf<TOption> _tOption;
            private ptr<QueryUnsafe> _query;
            private int _current;
            public WithEntity GetEnumerator() => this;
            public WithEntity Current => this;
            public bool MoveNext() {
                _current++;
                var index = _query.Ref.GetEntityID(_current);
                _t1.index = index;
                _tOption.index = index;
                return _current < _query.Ref.count;
            }
            public readonly void Deconstruct(out Entity e, out Rf<T1> c) {
                c = _t1;
                e = default;
            }
            public readonly void Deconstruct(out Entity e, out Rf<T1> c, out Rf<TOption> opt) {
                c = _t1;
                opt = _tOption;
                e = _query.cached->GetEntity(_current);
            }

            public void Init(ref World world) {
                var q = world.unsafeWorldPtr.Ref.CreateQueryPtr();
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
                        for (int i = 0; i < tuple.Length; i++) {
                            var type = tuple[i];
                            if (type is IFilter f) {
                                f.Setup(q.Ptr);
                            }
                        }
                        break;
                    }
                }
            }

            public void Update(ref World world) {
                
            }
        }

        public unsafe void Init(ref World world) {
            var q = world.unsafeWorldPtr.Ref.CreateQueryPtr();
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
                    for (int i = 0; i < tuple.Length; i++) {
                        var type = tuple[i];
                        if (type is IFilter f) {
                            f.Setup(q.Ptr);
                        }
                    }
                    break;
                }
            }
        }

        public void Update(ref World world) {
            
        }
    }
}
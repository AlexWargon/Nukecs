using System.Runtime.CompilerServices;

namespace Wargon.Nukecs
{
    public unsafe interface IFilter
    {
        void Setup(QueryUnsafe* query);
    }

    public interface IFilterWith<T1> : IFilter 
        where T1 : unmanaged, IComponent
    {
        ref T1 Get(int e);
    }
    public interface IFilterWith<T1, T2> : IFilter
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
    {
        (Ref<T1>, Ref<T2>) Get(int e);
    }
    public interface IFilterWith<T1, T2, T3>  : IFilter
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
    {
        (Ref<T1>, Ref<T2>, Ref<T3>) Get(int e);
    }
    public interface IFilterWith<T1, T2, T3, T4>  : IFilter
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
    {
        (Ref<T1>, Ref<T2>, Ref<T3>, Ref<T4>) Get(int e);
    }
    
    public interface IFilterWith<T1, T2, T3, T4, T5>  : IFilter
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged, IComponent
    {
        (Ref<T1>, Ref<T2>, Ref<T3>, Ref<T4>, Ref<T5>) Get(int e);
    }
    
    public struct With<T1> : IFilter where T1 : unmanaged, IComponent
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            query->With(ComponentType<T1>.Index);
        }
    }
    
    
    public struct With<T1, T2> : IFilter 
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            query->With(ComponentType<T1>.Index);
            query->With(ComponentType<T2>.Index);
        }
    }
    public struct With<T1, T2, T3> : IFilter
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            query->With(ComponentType<T1>.Index);
            query->With(ComponentType<T2>.Index);
            query->With(ComponentType<T3>.Index);
        }
    }
    
    public struct With<T1, T2, T3, T4> : IFilter
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            query->With(ComponentType<T1>.Index);
            query->With(ComponentType<T2>.Index);
            query->With(ComponentType<T3>.Index);
            query->With(ComponentType<T4>.Index);
        }
    }
    
    public struct With<T1, T2, T3, T4, T5> : IFilter
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged, IComponent
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            query->With(ComponentType<T1>.Index);
            query->With(ComponentType<T2>.Index);
            query->With(ComponentType<T3>.Index);
            query->With(ComponentType<T4>.Index);
            query->With(ComponentType<T5>.Index);
        }
    }
    
    public struct With<T1, T2, T3, T4, T5, T6> : IFilter 
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged, IComponent
        where T6 : unmanaged, IComponent
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            query->With(ComponentType<T1>.Index);
            query->With(ComponentType<T2>.Index);
            query->With(ComponentType<T3>.Index);
            query->With(ComponentType<T4>.Index);
            query->With(ComponentType<T5>.Index);
            query->With(ComponentType<T6>.Index);
        }
    }
    
    public struct None<T1> : IFilter where T1 : unmanaged, IComponent
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            query->None(ComponentType<T1>.Index);
        }
    }
    
    public struct None<T1, T2> : IFilter where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            query->None(ComponentType<T1>.Index);
            query->None(ComponentType<T2>.Index);
        }
    }
    public struct None<T1, T2, T3> : IFilter 
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            query->None(ComponentType<T1>.Index);
            query->None(ComponentType<T2>.Index);
            query->None(ComponentType<T3>.Index);
        }
    }
    
    public struct None<T1, T2, T3, T4> : IFilter 
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            query->None(ComponentType<T1>.Index);
            query->None(ComponentType<T2>.Index);
            query->None(ComponentType<T3>.Index);
            query->None(ComponentType<T4>.Index);
        }
    }
    
    public struct None<T1, T2, T3, T4, T5> : IFilter 
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged, IComponent
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            query->None(ComponentType<T1>.Index);
            query->None(ComponentType<T2>.Index);
            query->None(ComponentType<T3>.Index);
            query->None(ComponentType<T4>.Index);
            query->None(ComponentType<T5>.Index);
        }
    }

    public unsafe struct Query<TWith, TNone> 
        where TWith : unmanaged, IFilter
        where TNone : unmanaged, IFilter
    {
        internal QueryUnsafe* internalPointer;
        public static Query<TWith, TNone>  New(QueryUnsafe* q)
        {
            var query = new Query<TWith, TNone> 
            {
                internalPointer = q
            };
            TWith with = default;
            with.Setup(query.internalPointer);
            TNone none = default;
            none.Setup(query.internalPointer);
            return query;
        }
        
        public QueryEnumerator GetEnumerator() {
            return new QueryEnumerator(internalPointer);
        }
        
    }
    // public ref struct Enumerator {
    //     private int _lastIndex;
    //     private readonly QueryUnsafe* _query;
    //     private readonly GenericPool.GenericPoolUnsafe* _pool;
    //     [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //     internal Enumerator(QueryUnsafe* queryUnsafe) {
    //         _query = queryUnsafe;
    //         _lastIndex = -1;
    //         _pool = queryUnsafe->world->GetUntypedPool(ComponentType<T1>.Index).UnsafeBuffer;
    //     }
    //     [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //     public bool MoveNext() {
    //         _lastIndex++;
    //         return _query->count > _lastIndex;
    //     }
    //     
    //     public void Reset() {
    //         _lastIndex = -1;
    //     }
    //     
    //     public ref T1 Current {
    //         [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //         get => ref _pool->GetRef<T1>(_query->GetEntityID(_lastIndex));
    //     }
    // }
}
using System;

namespace Wargon.Nukecs.Tests.RuntimeDispatch
{
    public static unsafe class RuntimeOverloadQueryExtensions
    {
        public static RuntimeDispatch.RuntimeDispatchIter<RuntimeDispatch.RuntimeDispatchRefs<T1,T2,T3,T4>> GetOverloadEnumerator<T1,T2,T3,T4>(this in Query<T1,T2,T3,T4> query)
            where T1 : unmanaged,IComponent where T2 : unmanaged,IComponent where T3 : unmanaged,IComponent where T4 : unmanaged,IComponent
        {
            query.TryGetQuery(out var raw);
            return new RuntimeDispatch.RuntimeDispatchIter<RuntimeDispatch.RuntimeDispatchRefs<T1,T2,T3,T4>>(raw.Ptr);
        }
        public static RuntimePageIter<RuntimeAllPoolRefs<T1,T2,T3,T4>> GetOverloadEnumerator<T1,T2,T3,T4>(this Query<T1,T2,T3,T4> query)
            where T1 : unmanaged,IPoolComponent where T2 : unmanaged,IPoolComponent where T3 : unmanaged,IPoolComponent where T4 : unmanaged,IPoolComponent
        {
            query.TryGetQuery(out var raw);
            return new RuntimePageIter<RuntimeAllPoolRefs<T1,T2,T3,T4>>(raw.Ptr);
        }
        public static RuntimeDispatch.RuntimeDispatchIter<RuntimeDispatch.RuntimeDispatchRefs<T1,T2,T3,T4>> GetOverloadEnumerator<T1,T2,T3,T4,TFilter>(this in Query<T1,T2,T3,T4,TFilter> query)
            where T1 : unmanaged,IComponent where T2 : unmanaged,IComponent where T3 : unmanaged,IComponent where T4 : unmanaged,IComponent where TFilter : unmanaged
        {
            if(QueryParamInfo<TFilter>.IsComponent && ComponentType<TFilter>.Data.category!=ComponentCategory.Tag)
                throw new InvalidOperationException("The fifth parameter must be a filter or tag.");
            query.TryGetQuery(out var raw);
            return new RuntimeDispatch.RuntimeDispatchIter<RuntimeDispatch.RuntimeDispatchRefs<T1,T2,T3,T4>>(raw.Ptr);
        }
        public static RuntimePageIter<RuntimeAllPoolRefs<T1,T2,T3,T4>> GetOverloadEnumerator<T1,T2,T3,T4,TFilter>(this Query<T1,T2,T3,T4,TFilter> query)
            where T1 : unmanaged,IPoolComponent where T2 : unmanaged,IPoolComponent where T3 : unmanaged,IPoolComponent where T4 : unmanaged,IPoolComponent where TFilter : unmanaged
        {
            if(QueryParamInfo<TFilter>.IsComponent && ComponentType<TFilter>.Data.category!=ComponentCategory.Tag)
                throw new InvalidOperationException("The fifth parameter must be a filter or tag.");
            query.TryGetQuery(out var raw);
            return new RuntimePageIter<RuntimeAllPoolRefs<T1,T2,T3,T4>>(raw.Ptr);
        }
    }
    public static class RuntimeOverloadLoops
    {
        public static void Inline(ref Query<RuntimeQi4A,RuntimeQi4B,RuntimeQi4C,RuntimeQi4D> query)
        {
            foreach(var (a,b,c,d) in query)
            { a.Get.Value+=b.Read.Value; c.Get.Value+=d.Read.Value; }
        }
        public static void One(ref Query<RuntimeQi4A,RuntimeQi4B,RuntimeQi4C,CacheQi4P4> query)
        {
            foreach(var (a,b,c,d) in query)
            { a.Get.Value+=b.Read.Value; c.Get.Value+=d.Read.Value; }
        }
        public static void Mixed(ref Query<RuntimeQi4A,CacheQi4P2,RuntimeQi4C,CacheQi4P4> query)
        {
            foreach(var (a,b,c,d) in query)
            { a.Get.Value+=b.Read.Value; c.Get.Value+=d.Read.Value; }
        }

    }
}
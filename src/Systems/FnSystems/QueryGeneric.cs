using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs {
    public interface IQuery {

    }
    [BurstCompile(CompileSynchronously = true)]
    public static class QueryGenericExtensions {
        // internal static void FetchRange<TQuery>(this TQuery query, int start, int end)
        //     where TQuery : unmanaged, IQuery {
        //     query.Range = new Range(start, end);
        // }
        

        // public static bool MoveNext<TQuery>(this TQuery query)  where TQuery : unmanaged, IQuery {
        //     var range = query.Range;
        //     return false;
        // }
        [BurstCompile(CompileSynchronously = true)][MethodImpl(inline.YES)]
        public static TQueryEnumerator GetEnumerator<TQueryEnumerator>(this TQueryEnumerator query)
            where TQueryEnumerator :  struct, IQuery
        {
            return query;
        }
        
    }

    public enum SystemParamMetaType
    {
        None = 0,
        Query = 1,
        World = 2,
        Single = 3,
        Service = 4,
        State = 5
    }
    public interface ISystemParam {
        SystemParamMetaType MetaType { get; }
        void Init(ref ptr<World.WorldUnsafe> world);
        void Update(ref World world, IntPtr data);
        IntPtr GetData();
        bool TryGetQuery(out ptr<QueryUnsafe> query);
        public Type ParamType => GetType();
    }
    public readonly struct Range {
        public readonly int start;
        public readonly int end;

        public Range(int start, int end) {
            this.start = start;
            this.end = end;
        }
    }

    public struct Serv<TService> where TService : unmanaged, ISystemParam, IService
    {
        private ptr<ServiceStorage> _storage;
        public ref TService Ref => ref _storage.Ref.Get<TService>();
    }

    internal struct ServicesCount
    {
        internal static readonly SharedStatic<int> Count = SharedStatic<int>.GetOrCreate<ServicesCount>();
    }
    internal struct ServiceID<TService> where TService : unmanaged, ISystemParam, IService
    {
        internal static readonly SharedStatic<int> StaticInstance = SharedStatic<int>.GetOrCreate<ServiceID<TService>>();
        
        static ServiceID()
        {
            StaticInstance.Data = ServicesCount.Count.Data++;
        }
    }
    internal struct ServiceStorage
    {
        private UnsafeList<ptr> serviceList;

        internal void Register<TService>(ref ptr<World.WorldUnsafe> world) where TService : unmanaged, ISystemParam, IService
        {
            serviceList[ServiceID<TService>.StaticInstance.Data] =
                world.Ref.AllocatorRef.AllocatePtr<TService>().UntypedPointer;
        }
        internal ref TService Get<TService>() where TService : unmanaged, ISystemParam, IService
        {
            return ref serviceList[ServiceID<TService>.StaticInstance.Data].AsRef<TService>();
        }
    }
     
    public partial struct World : ISystemParam
    {
        public SystemParamMetaType MetaType => SystemParamMetaType.World;
        void ISystemParam.Init(ref ptr<WorldUnsafe> world)
        {
            throw new NotImplementedException();
        }

        void ISystemParam.Update(ref World world, IntPtr data)
        {
            throw new NotImplementedException();
        }

        IntPtr ISystemParam.GetData()
        {
            throw new NotImplementedException();
        }

        bool ISystemParam.TryGetQuery(out ptr<QueryUnsafe> query)
        {
            throw new NotImplementedException();
        }
    }
}
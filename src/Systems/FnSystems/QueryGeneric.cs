using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Wargon.Nukecs.Collections;
// ReSharper disable InconsistentNaming

namespace Wargon.Nukecs {
    public interface IQuery {
        int Count { get; }
    }

    public enum SystemParamMetaType : byte
    {
        Events = 0,
        Query = 1,
        World = 2,
        Single = 3,
        Service = 4,
        State = 5,
        Resource = 6,
        Local = 7
    }

    public struct SetQueryPtrProxy
    {
        private ptr<QueryUnsafe> _query;
        internal int id;
        public void SetQueryPtr(ptr<QueryUnsafe> q)
        {
            _query = q;
            id = q.Ref.Id;
        }
    }
    public interface ISystemParam {
        SystemParamMetaType MetaType { get; }
        void Init(ref ptr<World.WorldUnsafe> world);
        void Update(ref World world, IntPtr data);
        public Type ParamType => GetType();
    }

    public interface IRanged
    {
        void SetRange(Range range);
        Range GetRange();
    }
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Range {
        public readonly int start;
        public readonly int end;
        public int Count => end - start;
        public Range(int start, int end)
        {
            this.start = start;
            this.end = end;
        }

        public override string ToString()
        {
            return $"Range:[{start}, {end}]";
        }
    }

    public struct SystemParams 
    {
        private MemoryList<ptr> _pointers;
        private ptr<World.WorldUnsafe> _world;
        private int _count;
        
        public ref TParam Get<TParam>() where TParam : unmanaged, ISystemParam 
        {
            return ref _pointers[SystemParamData<TParam>.Index].AsRef<TParam>();
        }

        public void Add<TParam>(in TParam param) where TParam : unmanaged, ISystemParam 
        {
            var ptr = _world.Ref._allocate_ptr<TParam>();
            SystemParamData<TParam>.Set(new SystemParamData
            {
                metaType = param.MetaType,
                index = _count
            });
            _pointers[_count] = ptr.UntypedPointer;
        }
    }

    public struct SystemParamData<TParam> where TParam : unmanaged, ISystemParam
    {
        private static readonly SharedStatic<SystemParamData> Data = 
            SharedStatic<SystemParamData>.GetOrCreate<SystemParamData<TParam>>();
        public static ref readonly int Index => ref Data.Data.index;
        public static SystemParamMetaType MetaType => Data.Data.metaType;

        public static void Set(SystemParamData data)
        {
            Data.Data = data;
        }
    }

    public struct SystemParamData
    {
        public SystemParamMetaType metaType;
        public int index;
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
        public void SetQueryPtr(ptr<QueryUnsafe> q) { }
    }

    public enum _
    {
        None = 0,
        Component = 1,
        Resource = 2,
        System = 3,
    }
    public static class DefType
    {
        public const int None = 0;
        public const int Component = 1;
        public const int Resource = 2;
        public const int System = 3;
    }
    public class def : Attribute
    {
        public int defType;
        public @def(int defType)
        {
            this.defType = defType;
        }
        public @def(string defType)
        {
            switch (defType)
            {
                case "None":
                    this.defType = DefType.None;
                    break;
                case "Component":
                    this.defType = DefType.Component;
                    break;
                case "Resource":
                    this.defType = DefType.Resource;
                    break;
                case "System":
                    this.defType = DefType.System;
                    break;
            }
        }
    }
}
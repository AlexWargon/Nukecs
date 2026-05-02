using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Wargon.Nukecs.Allocators;

namespace Wargon.Nukecs
{
    public unsafe struct Single<T1> : ISystemParam
        where T1 : unmanaged, IComponent
    {

        private ptr<QueryUnsafe> _query;
        public SystemParamMetaType MetaType => SystemParamMetaType.Single;
        
        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _query = world.Ref.CreateQueryPtr();
            _query.Ref.With(ComponentType<T1>.Index);

        }

        public ref Entity Entity => ref _query.Ref.GetEntity(0);
        public void Update(ref World world, IntPtr data)
        {
            throw new NotImplementedException();
        }

        public IntPtr GetData()
        {
            throw new NotImplementedException();
        }

        public bool TryGetQuery(out ptr<QueryUnsafe> query)
        {
            throw new NotImplementedException();

        }
    }

    public unsafe struct MutRes<TRes> : ISystemParam where TRes : unmanaged
    {
        private readonly TRes* _reference;

        public MutRes(in TRes resource)
        {
            _reference = (TRes*)UnsafeUtility.MallocTracked(
                UnsafeUtility.SizeOf<TRes>(), 
                UnsafeUtility.AlignOf<TRes>(), 
                Unity.Collections.Allocator.Persistent, 0);
            
        }
        public static implicit operator TRes(in MutRes<TRes> res)
        {
            return *res._reference;
        }
        public static implicit operator MutRes<TRes>(in TRes res)
        {
            return new MutRes<TRes>(in res);
        }
        public SystemParamMetaType MetaType => SystemParamMetaType.Resource;
        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            
        }

        public void Update(ref World world, IntPtr data)
        {
            throw new NotImplementedException();
        }

        public IntPtr GetData()
        {
            throw new NotImplementedException();
        }

        public bool TryGetQuery(out ptr<QueryUnsafe> query)
        {
            throw new NotImplementedException();
        }
    }

    public interface IResource
    {
        void Init(ref World world);
        void Update(ref World world);
    }
    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct Res<TRes> : ISystemParam where TRes : struct, IResource
    {
        private TRes _reference;
        public readonly TRes val => _reference;
        public Res(in TRes resource)
        {
            _reference = resource;
        }
        
        public static implicit operator TRes(in Res<TRes> res)
        {
            return res._reference;
        }
        public static implicit operator Res<TRes>(in TRes res)
        {
            return new Res<TRes>(in res);
        }

        public SystemParamMetaType MetaType => SystemParamMetaType.Resource;
        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _reference.Init(ref World.Get(world.Ref.Id));
            dbug.log("INIT");
        }

        public void Update(ref World world, IntPtr data)
        {
            _reference.Update(ref world);
        }

        public IntPtr GetData()
        {
            return IntPtr.Zero;
        }

        public bool TryGetQuery(out ptr<QueryUnsafe> query)
        {
            query = default;
            return false;
        }
    }
}
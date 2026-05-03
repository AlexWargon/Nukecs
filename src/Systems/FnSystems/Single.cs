using System;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Wargon.Nukecs.Allocators;
using Wargon.Nukecs.Collections;

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
    /// <summary>
    /// Individual context for system. New instance for every system
    /// </summary>
    public interface IResource
    {
        /// <summary>
        /// Call on res creation.
        /// Can use managed types.
        /// </summary>
        /// <param name="world">ECS world</param>
        void Init(ref World world);
        /// <summary>
        /// Call before every system update.
        /// Can't use managed types.
        /// </summary>
        /// <param name="world">ECS world</param>
        void Update(ref World world);
    }

    public struct Local<TData> : ISystemParam where TData : unmanaged, IResource
    {
        private ptr<TData> _data;
        public ref TData Value => ref _data.Ref;
        public SystemParamMetaType MetaType => SystemParamMetaType.Local;
        public unsafe void Init(ref ptr<World.WorldUnsafe> world)
        {
            _data = world.Ref._allocate_ptr<TData>();
            _data.cached->Init(ref world.Ref.ManagedWorld.Ref);
        }

        public unsafe void Update(ref World world, IntPtr data)
        {
            _data.cached->Update(ref world);
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


    public struct param_type<T>
    {
        public static int index = 0;
    }
    public unsafe struct ResStorage
    {
        private MemoryList<ptr> _resources;

        public ResStorage(ref MemAllocator allocator)
        {
            _resources = new MemoryList<ptr>(32, ref allocator);
        }
        public ptr<T> Get<T>() where T : unmanaged
        {
            var index = param_type<T>.index;
            return _resources.Ptr[index].AsTyped<T>();
        }

        public bool Has<T>() where T : unmanaged
        {
            var index = param_type<T>.index;
            if (index >= _resources.length) return false;
            return  !_resources[param_type<T>.index].IsNull;
        }

        public bool Add<T>(in T resource, World.WorldUnsafe* world) where T : unmanaged
        {
            if (Has<T>()) return false;
            var ptr = world->_allocate_ptr<T>();
            param_type<T>.index = _resources.length;
            ptr.Ref = resource;
            _resources.Add(ptr.UntypedPointer, ref world->AllocatorRef);
            return true;
        }
    }
    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct Res<TRes> : ISystemParam where TRes : struct, IResource
    {
        public SystemParamMetaType MetaType => SystemParamMetaType.Resource;
        private TRes _reference;
        public readonly TRes Val => _reference;
        public Res(in TRes resource)
        {
            _reference = resource;
        }
        
        public static implicit operator TRes(in Res<TRes> res)
        {
            return res._reference;
        }
        public static explicit operator Res<TRes>(in TRes res)
        {
            return new Res<TRes>(in res);
        }

        
        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _reference.Init(ref world.Ref.ManagedWorld.Ref);
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
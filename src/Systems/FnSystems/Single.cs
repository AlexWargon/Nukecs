using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Wargon.Nukecs.Allocators;
using Allocator = Unity.Collections.Allocator;

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

        public void SetQueryPtr(ptr<QueryUnsafe> q) { }
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

        public void SetQueryPtr(ptr<QueryUnsafe> q) { }
    }

    internal interface IResourceCreate
    {
        ptr Create(ref ResStorage storage);
    }
    internal interface IResourceGetSet
    {
        internal IRes GetResource();
        internal void SetResource(IRes res);
    }
    /// <summary>
    /// Individual context for system. New instance for every system
    /// </summary>
    public interface IRes
    {
        /// <summary>
        /// Call ones on res creation.
        /// Can use managed types.
        /// </summary>
        /// <param name="world">ECS World : Wargon.Nukecs.World</param>
        void OnCreate(ref World world);
        /// <summary>
        /// Call before every system update.
        /// Can't use managed types.
        /// </summary>
        /// <param name="world">ECS World : Wargon.Nukecs.World</param>
        void OnUpdate(ref World world);
    }
    
    public unsafe struct Data<T> where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        private T* _data;

        public static Data<T> New()
        {
            Data<T> data = default;
            data._data = (T*)UnsafeUtility.MallocTracked(
                sizeof(T),
                UnsafeUtility.AlignOf<T>(),
                Allocator.Persistent, 0);
            return data;
        }

        public static void Dispose(Data<T> data)
        {
            UnsafeUtility.FreeTracked(data._data, Allocator.Persistent);
        }
    }
    public struct Local<TData> : ISystemParam where TData : unmanaged, IRes
    {
        public TData Ref;
        public SystemParamMetaType MetaType => SystemParamMetaType.Local;
        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            Ref = default;
            Ref.OnCreate(ref world.Ref.ManagedWorld.Ref);
        }

        public unsafe void Update(ref World world, IntPtr data)
        {
            Ref.OnUpdate(ref world);
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

        public void SetQueryPtr(ptr<QueryUnsafe> q) { }
    }
}
using System;
using Unity.Collections.LowLevel.Unsafe;
using Allocator = Unity.Collections.Allocator;

namespace Wargon.Nukecs
{
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
    /// Resource lifecycle used by shared Res/ResManaged values and per-registration Local values.
    /// </summary>
    public interface IRes
    {
        /// <summary>
        /// Called once when a resource is first registered in a world, or a Local value is created.
        /// Can use managed types.
        /// </summary>
        /// <param name="world">ECS World : Wargon.Nukecs.World</param>
        void OnCreate(ref World world);
        /// <summary>
        /// Called when a consuming system parameter updates. Unmanaged job paths must be Burst-compatible.
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
    /// <summary>Resource owned by one system registration. Parallel ranges share it.</summary>
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

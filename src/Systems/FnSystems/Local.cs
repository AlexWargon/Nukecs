using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
    /// <remarks>
    /// Holds only a ptr to a value block in the world arena, so the param stays blittable for
    /// Burst direct calls even when TData has bool/char fields, and a TData layout change
    /// needs no editor restart.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Local<TData> : ISystemParam where TData : unmanaged, IRes
    {
        // Must stay the first field: ResStorage rebases it as an untyped ptr after load.
        internal ptr<TData> value;

        public ref TData Ref
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *value.cached;
        }

        public SystemParamMetaType MetaType => SystemParamMetaType.Local;
        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            value = world.Ref._allocate_ptr<TData>(1, AllocatorTags.WorldMisc);
            *value.cached = default;
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

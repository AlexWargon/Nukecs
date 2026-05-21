using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wargon.Nukecs.Tests;

// ReSharper disable InconsistentNaming
// ReSharper disable StaticMemberInGenericType

namespace Wargon.Nukecs
{
    using static UnsafeStatic;
    /// <summary>
    /// Provides read/write access to a singleton resource
    /// from a system parameter.
    /// Example: <code>ExampleSystem(ref Res&lt;TRes&gt; res){ }</code>
    /// </summary>
    /// <typeparam name="TRes">The resource type.</typeparam>
    [StructLayout(LayoutKind.Sequential, Size = 1)]
    public struct Res<TRes> : ISystemParam, IResourceGetSet where TRes : struct, IRes
    {
        public SystemParamMetaType MetaType => SystemParamMetaType.Resource;
        public ref TRes Ref
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref StructSingleton<TRes>.Instance;
        }

        void IResourceGetSet.SetResource(IRes res)
        {
            
            Ref = (TRes)res;
        }

        IRes IResourceGetSet.GetResource()
        {
            return Ref;
        }

        internal void Set(IRes res)
        {
            Ref = (TRes)res;
        }

        public Res(in TRes resource)
        {
            //_field = ALLOCATOR.PER_WORLD[worldId].AllocatePtr(size_of<TRes>()).as_ptr_str<TRes>();
            //_field.Ref = resource;
            //world = worldId;

            if (!StructSingleton<TRes>.IsCreated)
            {
                StructSingleton<TRes>.Create(resource);
            }
            Ref = resource;
        }

        public unsafe void Init(ref ptr<World.WorldUnsafe> worldPtr)
        {
            if (!StructSingleton<TRes>.IsCreated)
            {
                StructSingleton<TRes>.Create();
            }
            Ref.OnCreate(ref worldPtr.Ref.ManagedWorld.Ref);
        }

        public void Update(ref World worldRef, IntPtr data)
        {
            Ref.OnUpdate(ref worldRef);
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
        
        public static implicit operator TRes(in Res<TRes> res)
        {
            return res.Ref;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct SaveRes<TRes> : ISystemParam where TRes : struct, IRes
    {
        private ptr_str<TRes> data;
        public ref TRes Ref => ref data.Ref;
        public SystemParamMetaType MetaType => SystemParamMetaType.Resource;
        public void Init(ref ptr<World.WorldUnsafe> worldPtr)
        {
            data = worldPtr.Ref.AllocatorRef.AllocatePtr(size_of<TRes>()).as_ptr_str<TRes>();
        }

        public void Update(ref World world, IntPtr data)
        {
            this.data.Ref.OnUpdate(ref world);
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
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
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
        // private ptr_str<TRes> _field;
        // private byte world;
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

        public Res(in TRes resource, byte worldId)
        {
            //_field = ALLOCATOR.PER_WORLD[worldId].AllocatePtr(size_of<TRes>()).as_ptr_str<TRes>();
            //_field.Ref = resource;
            //world = worldId;

            if (!StructSingleton<TRes>.IsCreated)
            {
                StructSingleton<TRes>.Create(resource);
            }
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

        // public static explicit operator Res<TRes>(in TRes res)
        // {
        //     return new Res<TRes>(in res);
        // }
    }

}
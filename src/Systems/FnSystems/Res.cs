using System;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming
// ReSharper disable StaticMemberInGenericType

namespace Wargon.Nukecs
{
    using static UnsafeStatic;

    /// <summary>
    /// Provides read/write access to a per-world singleton resource
    /// from a system parameter.
    /// Example: <code>ExampleSystem(ref Res&lt;TRes&gt; res){ }</code>
    /// The value lives in the world arena (ResStorage slot), so it is isolated per world,
    /// saved with the world, and changing the TRes layout does not require an editor restart.
    /// Outside systems use <see cref="World.GetRes{TRes}"/>.
    /// </summary>
    /// <typeparam name="TRes">The resource type.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    public struct Res<TRes> : ISystemParam, IResourceGetSet where TRes : unmanaged, IRes
    {
        /// <summary>Resource value. Systems receive the param by ref into the world arena slot.</summary>
        public TRes Ref;
        public SystemParamMetaType MetaType => SystemParamMetaType.Resource;

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
            Ref = resource;
        }

        public void Init(ref ptr<World.WorldUnsafe> worldPtr)
        {
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

        public void SetQueryPtr(ptr<QueryUnsafe> q) { }
        
        public static implicit operator TRes(in Res<TRes> res)
        {
            return res.Ref;
        }
    }
    // SaveRes<TRes> was removed before 1.0: it registered nothing, persisted nothing and
    // was referenced nowhere — its name implied save-related behavior that never existed.

    public struct TimeRes
    {
        public float DeltaTime;
        public float DeltaTimeFixed;
        public float Time;
        public double ElapsedTime;
        public uint TickCount;
    }
}
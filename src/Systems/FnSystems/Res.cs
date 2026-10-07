using System;
using System.Runtime.CompilerServices;
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
    /// The value lives in its own block in the world arena, so it is isolated per world and
    /// changing the TRes layout does not require an editor restart. Resources are runtime
    /// state: a load keeps the live world's values and ignores the saved ones.
    /// Res itself holds only an arena pointer: it stays blittable for Burst direct calls
    /// even when TRes has non-blittable fields (bool, char).
    /// Outside systems use <see cref="World.GetRes{TRes}"/>; a detached
    /// <c>new Res&lt;T&gt;()</c> has no storage.
    /// </summary>
    /// <typeparam name="TRes">The resource type.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Res<TRes> : ISystemParam, IResourceGetSet where TRes : unmanaged, IRes
    {
        // Must stay the first field: ResStorage rebases it as an untyped ptr after load.
        internal ptr<TRes> value;

        public SystemParamMetaType MetaType => SystemParamMetaType.Resource;

        public ref TRes Ref
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref *value.cached;
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

        public void Init(ref ptr<World.WorldUnsafe> worldPtr)
        {
            Create(ref worldPtr, default);
        }

        /// <summary>Allocates the value in the world arena, stores <paramref name="initial"/> and calls OnCreate.</summary>
        internal void Create(ref ptr<World.WorldUnsafe> worldPtr, in TRes initial)
        {
            ref var world = ref worldPtr.Ref;
            value = world._allocate_ptr<TRes>(1, AllocatorTags.WorldMisc);
            *value.cached = initial;
            world.resStorage.MarkValuePtr(StableTypeHash<Res<TRes>>.Value, sizeof(TRes));
            Ref.OnCreate(ref world.ManagedWorld.Ref);
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
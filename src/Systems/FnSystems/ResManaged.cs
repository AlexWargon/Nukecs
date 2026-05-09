using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wargon.Nukecs
{
    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct ResManaged<TRes> : ISystemParam, IResourceGetSet where TRes : class, IResource, new()
    {
        private ManagedResRef<TRes> _reference;
        public TRes Val => _reference.Value;
        public SystemParamMetaType MetaType => SystemParamMetaType.Resource;
        void IResourceGetSet.SetResource(IResource resource)
        {
            _reference = (TRes)resource;
        }
        IResource IResourceGetSet.GetResource()
        {
            return _reference.Value;
        }
        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _reference = new ManagedResRef<TRes>(new TRes());
            _reference.Value.Init(ref world.Ref.ManagedWorld.Ref);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(ref World world, IntPtr data)
        {
            _reference.Value.Update(ref world);
        }

        public IntPtr GetData() => IntPtr.Zero;

        public bool TryGetQuery(out ptr<QueryUnsafe> query)
        {
            query = default;
            return false;
        }
    }
}
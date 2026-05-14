using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wargon.Nukecs
{
    [Serializable, StructLayout(LayoutKind.Sequential)]
    public struct ResManaged<TRes> : ISystemParam, IResourceGetSet where TRes : class, IRes
    {
        internal ManagedResRef<TRes> _reference;
        public TRes Val => _reference.Value;
        public SystemParamMetaType MetaType => SystemParamMetaType.Resource;
        
        public ResManaged(TRes val)
        {
            _reference = val;
        }
        
        void IResourceGetSet.SetResource(IRes res)
        {
            _reference = (TRes)res;
        }
        
        IRes IResourceGetSet.GetResource()
        {
            return _reference.Value;
        }

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            if (_reference == null)
            {
                dbug.error($"managed res type of {typeof(TRes).Name} reference is null");
            }
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
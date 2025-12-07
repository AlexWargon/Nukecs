using System;
using Unity.Jobs;

namespace Wargon.Nukecs
{
    /// <summary>
    /// <code>
    /// Dependencies
    /// World
    /// Time
    /// </code>
    /// </summary>
    public struct State : ISystemParam
    {
        public JobHandle Dependencies;
        public World World;
        public TimeData Time;
        public SystemParamMetaType MetaType => SystemParamMetaType.State;
        void ISystemParam.Init(ref ptr<World.WorldUnsafe> world)
        {
            
        }

        void ISystemParam.Update(ref World world, IntPtr data)
        {
            
        }

        IntPtr ISystemParam.GetData()
        {
            return  IntPtr.Zero;
            
        }
        bool ISystemParam.TryGetQuery(out ptr<QueryUnsafe> query)
        {
            query = default;
            return false;
        }
    }
}
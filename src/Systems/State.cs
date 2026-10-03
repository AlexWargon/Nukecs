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
        public byte SkipECBSchedule;
        public SystemParamMetaType MetaType => SystemParamMetaType.State;
        void ISystemParam.Init(ref ptr<World.WorldUnsafe> world)
        {
            
        }

        void ISystemParam.Update(ref World world, IntPtr data)
        {
            
        }

        public void SetQueryPtr(ptr<QueryUnsafe> q) { }
    }
}
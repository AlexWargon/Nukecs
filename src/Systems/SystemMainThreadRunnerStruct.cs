using Unity.Jobs;

namespace Wargon.Nukecs
{
    internal class SystemMainThreadRunnerStruct<TSystem> : ISystemRunner where TSystem : struct, ISystem {
        internal TSystem System;
        internal ECBJob EcbJob;

        public JobHandle Schedule(UpdateContext updateContext, ref State state)
        {
            ref var world = ref state.World;
            world.CurrentContext = updateContext;
            System.OnUpdate(ref state);
            EcbJob.ECB = world.GetEcbVieContext(updateContext);
            EcbJob.world = world;
            return EcbJob.Schedule(state.Dependencies);
        }

        public void Run(ref State state) {
            System.OnUpdate(ref state);
            state.World.ECB.Playback(ref state.World);
        }
    }
}
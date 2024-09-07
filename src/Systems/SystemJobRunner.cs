using Unity.Jobs;

namespace Wargon.Nukecs
{
    internal class SystemJobRunner<TSystem> : ISystemRunner where TSystem : struct, IJobSystem {
        public TSystem System;
        public ECBJob EcbJob;

        public JobHandle Schedule(UpdateContext updateContext, ref State state) {
            System.Schedule(ref state.World, state.DeltaTime, state.Dependencies);
            EcbJob.ECB = state.World.GetEcbVieContext(updateContext);
            EcbJob.world = state.World;
            return EcbJob.Schedule(state.Dependencies);
        }

        public void Run(ref State state) {
            System.OnUpdate(ref state.World, state.DeltaTime);
            state.World.ECB.Playback(ref state.World);
        }
    }
}
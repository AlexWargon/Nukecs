using Unity.Jobs;

namespace Wargon.Nukecs
{
    internal class SystemMainThreadRunnerClass<TSystem> : ISystemRunner where TSystem : class, ISystem, new() {
        internal TSystem System;
        internal ECBJob EcbJob;

        public JobHandle Schedule(UpdateContext updateContext, ref State state)
        {
            ref var world = ref state.World;
            System.OnUpdate(ref state);
            EcbJob.ECB = world.GetEcbVieContext(updateContext);
            EcbJob.ECB.PlaybackMainThread(ref world);
            return state.Dependencies;
        }

        public void Run(ref State state) {
            System.OnUpdate(ref state);
            state.World.ECB.Playback(ref state.World);
        }
    }
}
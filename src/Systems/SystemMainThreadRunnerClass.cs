using Unity.Jobs;

namespace Wargon.Nukecs
{
    internal class SystemMainThreadRunnerClass<TSystem> : ISystemRunner where TSystem : class, ISystem, new() {
        internal TSystem System;
        internal ECBJob EcbJob;

        public JobHandle Schedule(ref World world, float dt, ref JobHandle jobHandle, UpdateContext updateContext) {
            world.CurrentContext = updateContext;
            System.OnUpdate(ref world, dt);
            EcbJob.ECB = world.GetEcbVieContext(updateContext);
            EcbJob.world = world;
            return EcbJob.Schedule(jobHandle);
        }

        public void Run(ref World world, float dt) {
            System.OnUpdate(ref world, dt);
            world.ECB.Playback(ref world);
        }
    }
}
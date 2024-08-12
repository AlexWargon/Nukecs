using Unity.Jobs;

namespace Wargon.Nukecs
{
    internal class SystemJobRunner<TSystem> : ISystemRunner where TSystem : struct, IJobSystem {
        public TSystem System;
        public ECBJob EcbJob;

        public JobHandle Schedule(ref World world, float dt, ref JobHandle jobHandle) {
            System.Schedule(ref world, dt, jobHandle);
            EcbJob.ECB = world.ECB;
            EcbJob.world = world;
            return EcbJob.Schedule(jobHandle);
        }

        public void Run(ref World world, float dt) {
            System.OnUpdate(ref world, dt);
            world.ECB.Playback(ref world);
        }
    }
}
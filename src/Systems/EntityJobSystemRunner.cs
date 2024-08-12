using Unity.Jobs;

namespace Wargon.Nukecs
{
    internal class EntityJobSystemRunner<TSystem> : ISystemRunner where TSystem : struct, IEntityJobSystem {
        public TSystem System;
        public Query Query;
        public SystemMode Mode;
        public ECBJob EcbJob;

        public JobHandle Schedule(ref World world, float dt, ref JobHandle jobHandle) {
            if (Mode == SystemMode.Main) {
                for (var i = 0; i < Query.Count; i++) {
                    System.OnUpdate(ref Query.GetEntity(i), dt);    
                }
                EcbJob.ECB = world.ECB;
                EcbJob.world = world;
                EcbJob.Run();
            }
            else {
                jobHandle = System.Schedule(ref Query, dt, Mode, jobHandle);
                EcbJob.ECB = world.ECB;
                EcbJob.world = world;
                jobHandle = EcbJob.Schedule(jobHandle);
            }
            return EcbJob.Schedule(jobHandle);
        }

        public void Run(ref World world, float dt) {
            for (int i = 0; i < Query.Count; i++) {
                System.OnUpdate(ref this.Query.GetEntity(i), dt);
            }
            world.ECB.Playback(ref world);
        }
    }
}
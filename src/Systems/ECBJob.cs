using Unity.Burst;
using Unity.Jobs;

namespace Wargon.Nukecs
{
    [BurstCompile]
    public struct ECBJob : IJob {
        public EntityCommandBuffer ECB;
        public World world;
        public UpdateContext updateContext;
        public void Execute() {
            if (!ECB.IsCreated || !ECB.HasCommands) return;
            ECB.Playback(ref world);
        }
    }
}
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
            ECB.Playback(ref world);
        }
    }
}
using Unity.Burst;

namespace Wargon.Nukecs
{
    [BurstCompile]
    public struct ECBJob : INJob {
        public EntityCommandBuffer ECB;
        public World world;
        public UpdateContext updateContext;
        public void Execute() {
            if (!ECB.IsCreated || !ECB.HasCommands) return;
            ECB.Playback(ref world);
        }
    }
}
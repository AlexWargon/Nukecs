using Unity.Burst;

namespace Wargon.Nukecs {
    [BurstCompile]
    public struct EntityDestroySystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Single;
        public Query GetQuery(ref World world)
        {
            world.GetPool<DestroyEntity>();
            return world.Query(false).With<DestroyEntity>();
        }
        public void OnUpdate(ref Entity entity, ref State state) {
            entity.DestroyNow();
        }
    }
}
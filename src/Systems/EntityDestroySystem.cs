using Unity.Burst;

namespace Wargon.Nukecs {
    [BurstCompile]
    public struct EntityDestroySystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Single;
        public Query GetQuery(ref World world)
        {
            return world.Query(false).With<DestroyEntity>();
        }
        public void OnUpdate(ref Entity entity, ref State state) {
            entity.DestroyNow();
        }
    }

    public struct EntityDestroyMTSystem : ISystem, IOnCreate
    {
        private Query query;
        public void OnCreate(ref World world)
        {
            query = world.Query(false).With<DestroyEntity>();
        }
        public void OnUpdate(ref State state)
        {
            foreach (ref var entity in query)
            {
                ref var arch = ref entity.ArchetypeRef;
                arch.Destroy(entity.id);
            }
        }
    }
}
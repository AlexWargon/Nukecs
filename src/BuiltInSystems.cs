namespace Wargon.Nukecs{

    using Unity.Burst;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;

    [BurstCompile]
    public struct EntityDestroySystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.Query().With<DestroyEntity>();
        }
        public void OnUpdate(ref Entity entity, ref State state) {
            entity.DestroyNow();
        }
    }

    public unsafe struct OnPrefabSpawnSystem : ISystem
    {
        public void OnUpdate(ref State state)
        {
            state.Dependencies = new OnPrefabSpawnJob{world = state.World.UnsafeWorld}.Schedule(state.Dependencies);
        }
        [BurstCompile]
        private struct OnPrefabSpawnJob : IJob {
            [NativeDisableUnsafePtrRestriction]
            public World.WorldUnsafe* world;
            public void Execute()
            {
                for (var index = 0; index < world->prefabesToSpawn.Length; index++)
                {
                    ref var e = ref world->prefabesToSpawn.ElementAtNoCheck(index);
                    e.Remove<IsPrefab>();
                }

                if(world->prefabesToSpawn.m_length > 0){
                    world->prefabesToSpawn.Clear();
                }
            }
        }
    }
}
namespace Wargon.Nukecs{

    using Unity.Burst;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;

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
                if(world->prefabsToSpawn.list.m_length < 1) return;
                for (var index = 0; index < world->prefabsToSpawn.list.m_length; index++)
                {
                    ref var e = ref world->prefabsToSpawn.ElementAt(index);
                    e.Remove<IsPrefab>();
                    if (e.Has<ComponentArray<Child>>())
                    {
                        ref var children = ref e.GetArray<Child>();
                        foreach (ref var child in children)
                        {
                            child.Value.Remove<IsPrefab>();
                        }
                    }
                }
                world->prefabsToSpawn.Clear();
            }
        }
    }
}
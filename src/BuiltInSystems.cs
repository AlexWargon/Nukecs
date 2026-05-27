namespace Wargon.Nukecs{

    using Unity.Burst;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    // public unsafe struct OnPrefabSpawnSystem : ISystem
    // {
    //     public void OnUpdate(ref State state)
    //     {
    //         ref var world = ref state.World.UnsafeWorldRef;
    //         if (world.prefabsToSpawn.Length < 1) return;
    //         for (var index = 0; index < world.prefabsToSpawn.Length; index++)
    //         {
    //             ref var e = ref world.prefabsToSpawn.ElementAt(index);
    //             e.Remove<IsPrefab>();
    //             if (e.Has<ComponentArray<Child>>())
    //             {
    //                 ref var children = ref e.GetArray<Child>();
    //                 foreach (ref var child in children)
    //                 {
    //                     child.Value.Remove<IsPrefab>();
    //                 }
    //             }
    //         }
    //         world.prefabsToSpawn.Clear();
    //     }
    // }

    public unsafe class OnPrefabSpawnSystem : ISystem
    {
        public void OnUpdate(ref State state)
        {
            if(state.World.IsAlive)
                state.Dependencies = new OnPrefabSpawnJob{world = state.World.UnsafeWorld}.Schedule(state.Dependencies);
        }
        [BurstCompile]
        private struct OnPrefabSpawnJob : IJob {
            [NativeDisableUnsafePtrRestriction]
            public World.WorldUnsafe* world;
            public void Execute()
            {
                ref var w = ref *world;
                if(w.prefabsToSpawn.Length < 1) return;
                for (var index = 0; index < w.prefabsToSpawn.Length; index++)
                {
                    ref var e = ref w.prefabsToSpawn.ElementAt(index);
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
                w.prefabsToSpawn.Clear();
            }
        }
    }
    public static class DefaultSystems
    {
        [BurstCompile, System]
        public static void OnPrefabSpawn(ref World world)
        {
            ref var w = ref world.UnsafeWorldRef;
            if(w.prefabsToSpawn.Length < 1) return;
            for (var index = 0; index < w.prefabsToSpawn.Length; index++)
            {
                ref var e = ref w.prefabsToSpawn.ElementAt(index);
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
            w.prefabsToSpawn.Clear();
        }

        [System, BurstCompile]
        public static void ClearEvents(ref World world)
        {
            world.UnsafeWorldRef.eventsStorage.ClearAll();
        }
    }
}
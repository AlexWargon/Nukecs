using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Wargon.Nukecs.Collision2D
{
    public class WriteCollisionsEventsSystem : ISystem
    {
        public void OnUpdate(ref State state)
        {
            var grind2D = Grid2D.Instance;
            ref var hits = ref grind2D.Hits;
            var job = new WriteCollisionsEventsJob
            {
                World = state.World,
                CollisionsDataPool = state.World.GetPool<ComponentArray<Collision2DData>>()
                    .AsComponentPool<ComponentArray<Collision2DData>>(),
                Hits = hits
            };
            state.Dependencies = job.Schedule(state.Dependencies);
        }

        [BurstCompile]
        public struct WriteCollisionsEventsJob : IJob
        {
            public World World;
            public ComponentPool<ComponentArray<Collision2DData>> CollisionsDataPool;
            public NativeQueue<HitInfo> Hits;
            public void Execute()
            {
                while (Hits.Count > 0)
                {
                    var hitInfo = Hits.Dequeue();
                    ref var buffer1 = ref CollisionsDataPool.Get(hitInfo.From);
                    ref var buffer2 = ref CollisionsDataPool.Get(hitInfo.To);
                    ref var ent1 = ref World.GetEntity(hitInfo.From);
                    ref var ent2 = ref World.GetEntity(hitInfo.To);
                    if(!ent1.IsValid() || !ent2.IsValid()) continue;
                    buffer1.AddParallel(new Collision2DData
                    {
                        Other = ent2,
                        Type = hitInfo.Type,
                        Position = hitInfo.Pos,
                        Normal = hitInfo.Normal
                    });
                                    
                    buffer2.AddParallel(new Collision2DData
                    {
                        Other = ent1,
                        Type = hitInfo.Type,
                        Position = hitInfo.Pos,
                        Normal = hitInfo.Normal
                    });
                    ent1.Add<CollidedFlag>();
                    ent2.Add<CollidedFlag>();
                }
            }
        }
    }
}

using Unity.Burst;

namespace Wargon.Nukecs.Collision2D
{
    using Unity.Jobs;
    using Transforms;
    using Unity.Collections;
    public struct Collision2DSystem : ISystem, IOnCreate {
        private GenericPool transforms;
        private GenericPool colliders;
        private GenericPool rectangles;
        private GenericPool bodies;
        private GenericPool collisionsDataArrays;
        private int collisionStatesSize;
        
        public void OnCreate(ref World world)
        {
            transforms = world.GetPool<Transform>();
            colliders = world.GetPool<Circle2D>();
            rectangles = world.GetPool<Rectangle2D>();
            bodies = world.GetPool<Body2D>();
            collisionsDataArrays = world.GetPool<ComponentArray<Collision2DData>>();
        }
        
        public void OnUpdate(ref State state) {
            var grind2D = Grid2D.Instance;
            grind2D.Hits.Clear();
            var estimatedSize = colliders.Count * 24 + 1000;
            ref var processedCollisions = ref grind2D.ProcessedCollisions;
            if (processedCollisions.IsCreated)
            {
                processedCollisions.Dispose();
            }
            processedCollisions = new NativeParallelHashSet<ulong>(estimatedSize, Allocator.TempJob);

            ref var cells = ref grind2D.cells;
            var notEmptyCellsIndexes = new NativeList<int>(cells.Length, Allocator.TempJob);
            for (int i = 0; i < cells.m_length; i++)
            {
                ref var cell = ref cells.ElementAtNoCheck(i);
                if (cell.CollidersBuffer.Count == 0 && cell.RectanglesBuffer.Count == 0) continue;
                notEmptyCellsIndexes.Add(i);
            }

            var collisionJob = new Collision2DHitsParallelJobBatched {
                Colliders = colliders.AsComponentPool<Circle2D>(),
                Transforms = transforms.AsComponentPool<Transform>(),
                Bodies = bodies.AsComponentPool<Body2D>(),
                Rectangles = rectangles.AsComponentPool<Rectangle2D>(),
                CollisionData = collisionsDataArrays.AsComponentPool<ComponentArray<Collision2DData>>(),
                CollisionEnterHits = grind2D.Hits.AsParallelWriter(),
                Cells = grind2D.cells,
                W = grind2D.W,
                H = grind2D.H,
                Offset = grind2D.Offset,
                GridPosition = grind2D.Position,
                CellSize = grind2D.CellSize,
                World = state.World,
                ProcessedCollisions = processedCollisions.AsParallelWriter(),
                CellIndexes = notEmptyCellsIndexes
            };

            state.Dependencies = collisionJob.ScheduleBatch(notEmptyCellsIndexes.Length, 16, state.Dependencies);

            state.Dependencies = notEmptyCellsIndexes.Dispose(state.Dependencies);
        }
    }

    public struct WriteCollisionsEventsSystem : ISystem
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
                                    
                    ent1.Add(new CollidedFlag());
                    ent2.Add(new CollidedFlag());
                }
            }
        }
    }
}  
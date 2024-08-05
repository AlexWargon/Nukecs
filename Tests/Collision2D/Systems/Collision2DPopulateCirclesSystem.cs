namespace Wargon.Nukecs.Collision2D {
    using Unity.Burst;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;
    using Transform = Transforms.Transform;

    public struct Collision2DPopulateCirclesSystem : ISystem, IOnCreate {
        private GenericPool colliders;
        public Query query;
        private GenericPool transforms;

        public void OnCreate(ref World world) {
            query = world.Query().With<Circle2D>().With<Transform>();
            colliders = world.GetPool<Circle2D>();
            transforms = world.GetPool<Transform>();
        }

        public void OnUpdate(ref World world, float deltaTime) {
            var grid2D = Grid2D.Instance;
            var populateJob = new PopulateCellsJob {
                query = query,
                colliders = colliders.AsComponentPool<Circle2D>(),
                transforms = transforms.AsComponentPool<Transform>(),
                cells = grid2D.cells,
                cellSizeX = grid2D.CellSize,
                cellSizeY = grid2D.CellSize,
                Offset = grid2D.Offset,
                GridPosition = grid2D.Position,
                W = grid2D.W,
                H = grid2D.H
            };
            world.Dependencies = populateJob.Schedule(query.Count, 1, world.Dependencies);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
        public struct PopulateCellsJob : IJobParallelFor {
            public Query query;
            public UnsafeList<Grid2DCell> cells;
            public ComponentPool<Circle2D> colliders;
            public ComponentPool<Transform> transforms;
            public int cellSizeX, cellSizeY, W, H;
            public Vector2 Offset;
            public Vector2 GridPosition;

            public void Execute(int index) {
                var e = query.GetEntityIndex(index);
                ref var circle = ref colliders.Get(e);
                ref var transform = ref transforms.Get(e);
                circle.collided = false;
                circle.position = new float2(transform.Position.x, transform.Position.y);
                var px = floor((transform.Position.x - Offset.x - GridPosition.x) / cellSizeX);
                var py = floor((transform.Position.y - Offset.y - GridPosition.y) / cellSizeY);

                if (px >= 0 && px < W && py >= 0 && py < H) {
                    var cellIndex = py * W + px;
                    if (cellIndex > -1 && cellIndex < cells.Length) {
                        var cell = cells[cellIndex];
                        cell.CollidersBuffer.Add(circle.index);
                        circle.cellIndex = cellIndex;
                        cells[cellIndex] = cell;
                    }
                }
            }

            private int floor(float x) {
                var xi = (int) x;
                return x < xi ? xi - 1 : xi;
            }
        }
    }
}
namespace Wargon.Nukecs.Collision2D {
    using Unity.Burst;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using UnityEngine;
    using Transform = Transforms.Transform;

    public struct Collision2DPopulateRectsSystem : ISystem, IOnCreate {
        private GenericPool colliders;
        private GenericPool transforms;
        private Query query;

        public void OnCreate(ref World world) {
            query = world.Query().With<Rectangle2D>().With<Transform>()
                //.WithArray<Collision2DData>()
                ;
            colliders = world.GetPool<Rectangle2D>();
            transforms = world.GetPool<Transform>();
        }
        public void OnUpdate(ref State state) {
            var grid2D = Grid2D.Instance;
            var populateJob = new PopulateCellsJob {
                query = query,
                rectangles = colliders.AsComponentPool<Rectangle2D>(),
                transforms = transforms.AsComponentPool<Transform>(),
                cells = grid2D.cells,
                cellSizeX = grid2D.CellSize,
                cellSizeY = grid2D.CellSize,
                Offset = grid2D.Offset,
                GridPosition = grid2D.Position,
                W = grid2D.W,
                H = grid2D.H
            };
            state.Dependencies = populateJob.Schedule(query.Count, state.Dependencies);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
        public struct PopulateCellsJob : IJobFor {
            public Query query;
            public UnsafeList<Grid2DCell> cells;
            public ComponentPool<Transform> transforms;
            public ComponentPool<Rectangle2D> rectangles;
            public int cellSizeX, cellSizeY, W, H;
            public Vector2 Offset;
            public Vector2 GridPosition;

            public void Execute(int index) {
                var e = query.GetEntityIndex(index);
                ref var rect = ref rectangles.Get(e);

                ref var transform = ref transforms.Get(e);

                var px = floor((transform.Position.x - Offset.x - GridPosition.x) / cellSizeX);
                var py = floor((transform.Position.y - Offset.y - GridPosition.y) / cellSizeY);
                var cellIndex = py * W + px;
                if (cellIndex > -1 && cellIndex < cells.Length) {
                    var cell = cells[cellIndex];
                    cell.RectanglesBuffer.Add(e);
                    cells[cellIndex] = cell;
                }
            }

            private static int floor(float x) {
                var xi = (int) x;
                return x < xi ? xi - 1 : xi;
            }
        }
    }
}
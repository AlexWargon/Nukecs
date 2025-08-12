using Unity.Mathematics;

namespace Wargon.Nukecs.Collision2D {
    using Unity.Burst;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using UnityEngine;
    using Transform = Transforms.Transform;

    public struct Collision2DPopulateRectsSystem : ISystem, IOnCreate, IJobRunner {
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
            var populateJob = new PopulateCellsJob2 {
                query = query,
                rectangles = colliders.AsComponentPool<Rectangle2D>(),
                transforms = transforms.AsComponentPool<Transform>(),
                cells = grid2D.cells,
                cellSizeX = grid2D.CellSize,
                cellSizeY = grid2D.CellSize,
                Offset = grid2D.Offset,
                GridPosition = grid2D.Position,
                W = grid2D.width,
                H = grid2D.height
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
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
    public struct PopulateCellsJob2 : IJobFor
    {
        public Query query;
        public UnsafeList<Grid2DCell> cells;
        public ComponentPool<Transform> transforms;
        public ComponentPool<Rectangle2D> rectangles;
        public int cellSizeX, cellSizeY, W, H;
        public Vector2 Offset;
        public Vector2 GridPosition;

        public void Execute(int index)
        {
            var e = query.GetEntityIndex(index);
            ref var rect = ref rectangles.Get(e);
            ref var transform = ref transforms.Get(e);

            // Получаем вершины прямоугольника с учетом вращения
            rect.GetVertices(transform, out float2 v0, out float2 v1, out float2 v2, out float2 v3);

            // Находим AABB прямоугольника
            float2 min = math.min(math.min(v0, v1), math.min(v2, v3));
            float2 max = math.max(math.max(v0, v1), math.max(v2, v3));

            // Определяем диапазон ячеек
            int minX = (int)math.floor((min.x - Offset.x - GridPosition.x) / cellSizeX);
            int minY = (int)math.floor((min.y - Offset.y - GridPosition.y) / cellSizeY);
            int maxX = (int)math.floor((max.x - Offset.x - GridPosition.x) / cellSizeX);
            int maxY = (int)math.floor((max.y - Offset.y - GridPosition.y) / cellSizeY);

            minX = math.clamp(minX, 0, W - 1);
            minY = math.clamp(minY, 0, H - 1);
            maxX = math.clamp(maxX, 0, W - 1);
            maxY = math.clamp(maxY, 0, H - 1);

            // Добавляем прямоугольник во все пересекаемые ячейки
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int cellIndex = y * W + x;
                    if (cellIndex >= 0 && cellIndex < cells.Length)
                    {
                        cells.ElementAt(cellIndex).RectanglesBuffer.Add(e);
                    }
                }
            }
            
        }
    }
}
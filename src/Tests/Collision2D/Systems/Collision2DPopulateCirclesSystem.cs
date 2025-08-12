using System.Runtime.CompilerServices;

namespace Wargon.Nukecs.Collision2D {
    using Unity.Burst;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;
    using UnityEngine;
    using Transform = Transforms.Transform;

    public struct Collision2DPopulateCirclesSystem : ISystem, IOnCreate, IJobRunner {
        private GenericPool colliders;
        public Query query;
        private GenericPool transforms;

        public void OnCreate(ref World world) {
            query = world.Query().With<Circle2D>().With<Transform>().WithArray<Collision2DData>()
                //.WithArray<Collision2DData>()
                ;
            colliders = world.GetPool<Circle2D>();
            transforms = world.GetPool<Transform>();
        }

        public void OnUpdate(ref State state) {
            var grid2D = Grid2D.Instance;
            var populateJob = new PopulateCellsJobSingle {
                query = query,
                colliders = colliders.AsComponentPool<Circle2D>(),
                transforms = transforms.AsComponentPool<Transform>(),
                cells = grid2D.cells,
                cellSizeX = grid2D.CellSize,
                cellSizeY = grid2D.CellSize,
                Offset = grid2D.Offset,
                GridPosition = grid2D.Position,
                gridWidth = grid2D.width,
                gridHight = grid2D.height
            };
            state.Dependencies = populateJob.Schedule(query.Count, 64, state.Dependencies);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        public struct PopulateCellsJob : IJobParallelFor {
            public Query query;
            public UnsafeList<Grid2DCell> cells;
            public ComponentPool<Circle2D> colliders;
            public ComponentPool<Transform> transforms;
            public int cellSizeX, cellSizeY, gridWidth, gridHight;
            public Vector2 Offset, GridPosition;

            public void Execute(int index) {
                var e = query.GetEntityIndex(index);
                ref var circle = ref colliders.Get(e);
                ref var transform = ref transforms.Get(e);
                circle.collided = false;
                circle.position = new float2(transform.Position.x, transform.Position.y);
                var px = floor((transform.Position.x - Offset.x - GridPosition.x) / cellSizeX);
                var py = floor((transform.Position.y - Offset.y - GridPosition.y) / cellSizeY);
                
                if (px >= 0 && px < gridWidth && py >= 0 && py < gridHight) {
                    var cellIndex = py * gridWidth + px;
                    if (cellIndex > -1 && cellIndex < cells.m_length) {
                        ref var cell = ref cells.ElementAt(cellIndex);
                        cell.CollidersBuffer.Add(e);
                        circle.cellIndex = cellIndex;
                    }
                }
            }
            [BurstCompile]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private int floor(float x) {
                var xi = (int) x;
                return x < xi ? xi - 1 : xi;
            }
        }
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        public struct PopulateCellsJobSingle : IJobParallelFor {
            public Query query;
            public UnsafeList<Grid2DCell> cells;
            public ComponentPool<Circle2D> colliders;
            public ComponentPool<Transform> transforms;
            public int cellSizeX, cellSizeY, gridWidth, gridHight;
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
                
                if (px >= 0 && px < gridWidth && py >= 0 && py < gridHight) {
                    var cellIndex = py * gridWidth + px;
                    if (cellIndex > -1 && cellIndex < cells.m_length) {
                        ref var cell = ref cells.ElementAt(cellIndex);
                        cell.CollidersBuffer.Add(e);
                        circle.cellIndex = cellIndex;
                    }
                }
            }
            [BurstCompile]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private int floor(float x) {
                var xi = (int) x;
                return x < xi ? xi - 1 : xi;
            }
        }
    }
}
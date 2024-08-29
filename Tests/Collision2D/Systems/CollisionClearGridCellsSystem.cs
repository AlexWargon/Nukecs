namespace Wargon.Nukecs.Collision2D
{
    using Unity.Burst;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;

    public struct CollisionClearGridCellsSystem : ISystem {
        public void OnUpdate(ref World world, float dt){
            var grind2d = Grid2D.Instance;
            world.DependenciesUpdate = new ClearJob {
                cells = grind2d.cells
            }.Schedule(grind2d.cells.Length, 1, world.DependenciesUpdate);
        }

        [BurstCompile]
        private struct ClearJob : IJobParallelFor {
            public UnsafeList<Grid2DCell> cells;
            public void Execute(int i) {
                var cell = cells.ElementAt(i);
                cell.CollidersBuffer.Clear();
                cell.RectanglesBuffer.Clear();
                cells[i] = cell;
            }
        }
    }
}  
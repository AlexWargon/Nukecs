namespace Wargon.Nukecs.Collision2D
{
    using Unity.Burst;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;

    public struct CollisionClearGridCellsSystem : ISystem {
        public void OnUpdate(ref State state){
            var grind2d = Grid2D.Instance;
            state.Dependencies = new ClearJob {
                cells = grind2d.cells
            }.Schedule(grind2d.cells.Length, 1, state.Dependencies);
        }

        [BurstCompile]
        private struct ClearJob : IJobParallelFor {
            public UnsafeList<Grid2DCell> cells;
            public void Execute(int i) {
                ref var cell = ref cells.ElementAt(i);
                cell.CollidersBuffer.Clear();
                cell.RectanglesBuffer.Clear();
            }
        }
    }
}  
namespace Wargon.Nukecs.Collision2D
{
    using Unity.Jobs;
    using Transforms;
    using Unity.Collections;
    public class Collision2DSystem : ISystem, IOnCreate, IOnDestroy, IJobRunner {
        private GenericPool _transforms;
        private GenericPool _colliders;
        private GenericPool _rectangles;
        private GenericPool _bodies;
        private GenericPool _collisionsDataArrays;
        private int _collisionStatesSize;
        private NativeList<int> _notEmptyCellsIndexes;
        public void OnDestroy(ref World world)
        {
            _notEmptyCellsIndexes.Dispose();
        }
        public void OnCreate(ref World world)
        {
            _transforms = world.GetPool<Transform>();
            _colliders = world.GetPool<Circle2D>();
            _rectangles = world.GetPool<Rectangle2D>();
            _bodies = world.GetPool<Body2D>();
            _collisionsDataArrays = world.GetPool<ComponentArray<Collision2DData>>();
        }
        
        public void OnUpdate(ref State state) {
            var grind2D = Grid2D.Instance;
            grind2D.Hits.Clear();
            var estimatedSize = _colliders.Count * 24 + 1000;
            ref var processedCollisions = ref grind2D.ProcessedCollisions;
            if (!processedCollisions.IsCreated || processedCollisions.Capacity < estimatedSize)
            {
                if (processedCollisions.IsCreated) processedCollisions.Dispose();
                processedCollisions = new NativeParallelHashSet<ulong>(estimatedSize, Allocator.Persistent);
            }
            
            processedCollisions.Clear();
            
            ref var cells = ref grind2D.cells;
            if (!_notEmptyCellsIndexes.IsCreated)
            {
                _notEmptyCellsIndexes = new NativeList<int>(cells.Length, Allocator.Persistent);
            }
            
            _notEmptyCellsIndexes.Clear();
            for (int i = 0; i < cells.m_length; i++)
            {
                ref var cell = ref cells.ElementAtNoCheck(i);
                if (cell.CollidersBuffer.Count == 0 && cell.RectanglesBuffer.Count == 0) continue;
                _notEmptyCellsIndexes.Add(i);
            }

            var collisionJob = new Collision2DHitsParallelJobBatched {
                colliders = _colliders.AsComponentPool<Circle2D>(),
                transforms = _transforms.AsComponentPool<Transform>(),
                bodies = _bodies.AsComponentPool<Body2D>(),
                rectangles = _rectangles.AsComponentPool<Rectangle2D>(),
                collisionData = _collisionsDataArrays.AsComponentPool<ComponentArray<Collision2DData>>(),
                collisionEnterHits = grind2D.Hits.AsParallelWriter(),
                cells = grind2D.cells,
                width = grind2D.width,
                height = grind2D.height,
                offset = grind2D.Offset,
                gridPosition = grind2D.Position,
                CellSize = grind2D.CellSize,
                world = state.World,
                processedCollisions = processedCollisions.AsParallelWriter(),
                cellIndexes = _notEmptyCellsIndexes
            };

            state.Dependencies = collisionJob.ScheduleBatch(_notEmptyCellsIndexes.Length, 16, state.Dependencies);
        }
    }
}  
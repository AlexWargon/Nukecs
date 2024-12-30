namespace Wargon.Nukecs.Collision2D
{
    using Unity.Jobs;
    using Transforms;

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
            // ref var collisionState = ref grind2D.collisionStates;
            // collisionStatesSize = collisionState.Capacity;
            //
            // if (collisionStatesSize < state.World.EntitiesAmount * 4)
            // {
            //     var newCapacity = collisionStatesSize * 2;
            //     collisionState.Capacity = newCapacity;
            //     collisionStatesSize = newCapacity;
            // }
            var collisionJob1 = new Collision2DHitsParallelJob {
                colliders = colliders.AsComponentPool<Circle2D>(),
                transforms = transforms.AsComponentPool<Transform>(),
                bodies = bodies.AsComponentPool<Body2D>(),
                rectangles = rectangles.AsComponentPool<Rectangle2D>(),
                collisionData = collisionsDataArrays.AsComponentPool<ComponentArray<Collision2DData>>(),
                collisionEnterHits = grind2D.Hits.AsParallelWriter(),
                cells = grind2D.cells,
                W = grind2D.W,
                H = grind2D.H,
                Offset = grind2D.Offset,
                GridPosition = grind2D.Position,
                cellSize = grind2D.CellSize,
                world = state.World
            };
            state.Dependencies = collisionJob1.Schedule(Grid2D.Instance.cells.Length, 64, state.Dependencies);
        }
    }
}  
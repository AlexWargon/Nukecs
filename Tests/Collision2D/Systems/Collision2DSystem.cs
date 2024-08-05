namespace Wargon.Nukecs.Collision2D
{
    using Unity.Jobs;
    using Wargon.Nukecs.Transforms;

    public struct Collision2DSystem : ISystem, IOnCreate {
        private GenericPool transforms;
        private GenericPool colliders;
        private GenericPool rectangles;
        private GenericPool bodies;
        public void OnCreate(ref World world) {
            transforms = world.GetPool<Transform>();
            colliders = world.GetPool<Circle2D>();
            rectangles = world.GetPool<Rectangle2D>();
            bodies = world.GetPool<Body2D>();
        }
        public void OnUpdate(ref World world, float deltaTime) {
            var grind2D = Grid2D.Instance;
            var collisionJob1 = new Collision2DMark2ParallelHitsJob {
                colliders = colliders.AsComponentPool<Circle2D>(),
                transforms = transforms.AsComponentPool<Transform>(),
                bodies = bodies.AsComponentPool<Body2D>(),
                rectangles = rectangles.AsComponentPool<Rectangle2D>(),
                collisionEnterHits = grind2D.Hits.AsParallelWriter(),
                cells = grind2D.cells,
                W = grind2D.W,
                H = grind2D.H,
                Offset = grind2D.Offset,
                GridPosition = grind2D.Position,
                cellSize = grind2D.CellSize,
                iterations = 1
            };

            world.Dependencies = collisionJob1.Schedule(Grid2D.Instance.cells.Length, 1, world.Dependencies);
        }
    }
}  
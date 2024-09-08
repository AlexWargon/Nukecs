namespace Wargon.Nukecs.Collision2D
{
    using Unity.Burst;
    using Unity.Jobs;
    using Transform = Transforms.Transform;

    public struct CollidersSizeUpdateSystem : ISystem, IOnCreate {
        private GenericPool circles;
        private Query query;
        private GenericPool transforms;
        public void OnCreate(ref World world)
        {
            query = world.Query().With<Circle2D>().With<Transform>();
            circles = world.GetPool<Circle2D>();
            transforms = world.GetPool<Transform>();
        }
        public void OnUpdate(ref State state)
        {
            state.Dependencies = new Job {
                    transforms = transforms.AsComponentPool<Transform>(),
                    circles = circles.AsComponentPool<Circle2D>(),
                    query = query
            }.Schedule(query.Count, 1, state.Dependencies);
        }



        [BurstCompile]
        private struct Job : IJobParallelFor {
            public ComponentPool<Transform> transforms;
            public ComponentPool<Circle2D> circles;
            public Query query;

            public void Execute(int index) {
                var e = query.GetEntity(index);
                ref var circle = ref circles.Get(e.id);
                var transform = transforms.Get(e.id);
                if (circle.oneFrame == false)
                    circle.radius = transform.Scale.x * circle.radiusDefault;
            }
        }
    }
}
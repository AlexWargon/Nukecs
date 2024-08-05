namespace Wargon.Nukecs.Collision2D
{
    using Unity.Burst;
    using Unity.Mathematics;
    using Transform = Transforms.Transform;

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
    public struct UpdateCirclePositionsSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world)
        {
            return world.Query().With<Transform>().With<Body2D>().With<Circle2D>();
        }

        public void OnUpdate(ref Entity entity, float deltaTime)
        {
            ref readonly var body = ref entity.Read<Body2D>();
            ref var collider = ref entity.Get<Circle2D>();
            var pos = entity.Get<Transform>().Position;
            collider.position = new float2(pos.x, pos.y);
            collider.position += body.velocity;
        }
    }
}  
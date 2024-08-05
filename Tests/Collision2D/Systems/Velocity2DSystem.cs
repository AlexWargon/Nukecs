namespace Wargon.Nukecs.Collision2D
{
    using Unity.Burst;
    using Unity.Mathematics;
    using Transform = Transforms.Transform;

    [BurstCompile]
    public struct Velocity2DSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world)
        {
            return world.Query().With<Body2D>().With<Transform>();
        }
        public void OnUpdate(ref Entity entity, float deltaTime)
        {
            ref var body = ref entity.Get<Body2D>();
            ref var transform = ref entity.Get<Transform>();
            transform.Position = new float3(transform.Position.x + body.velocity.x, transform.Position.y + body.velocity.y, transform.Position.z);

            body.velocity = float2.zero;
        }
    }
}  
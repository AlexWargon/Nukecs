
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
            return world.Query().With<Body2D>().With<Transform>().None<DestroyEntity>();
        }
        public void OnUpdate(ref Entity entity, ref State state)
        {
            ref var body = ref entity.Get<Body2D>();
            var deltaTime = state.DeltaTime;
            ref var transform = ref entity.Get<Transform>();
            transform.Position = new float3(transform.Position.x + body.velocity.x * deltaTime, transform.Position.y + body.velocity.y * deltaTime, transform.Position.z);
            //body.velocity = float2.zero;
        }
    }
    [BurstCompile]
    public struct OnColliderSpawnSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Single;
        public Query GetQuery(ref World world) {
            return world.Query().With<Circle2D>().With<EntityCreated>();
        }

        public void OnUpdate(ref Entity entity, ref State state) {
            entity.Get<Circle2D>().index = entity.id;
        }
    }
}  
using Unity.Burst;

namespace Wargon.Nukecs.Collision2D
{
    [BurstCompile]
    public struct CollisionsClear : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.Query().WithArray<Collision2DData>().With<CollidedFlag>();
        }
        [BurstCompile]
        public void OnUpdate(ref Entity entity, ref State state) {
            ref var buffer = ref entity.GetArray<Collision2DData>();
            buffer.Clear();
            entity.Remove<CollidedFlag>();
        }
    }
}
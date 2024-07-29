using Unity.Burst;

namespace Wargon.Nukecs.Tests {
    [BurstCompile]
    public struct CullSpritesSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery()
                .With<SpriteRenderData>()
                .With<SpriteChunkReference>()
                .None<Culled>();
        }

        public void OnUpdate(ref Entity entity, float deltaTime) {
            var data = CullingData.instance;
            var xMax = data.xMax;
            var yMax = data.yMax;
            var xMin = data.xMin;
            var yMin = data.yMin;
            ref readonly var transform = ref entity.Read<Transform>();
            if (!(transform.Position.x < xMax && 
                  transform.Position.x > xMin &&
                  transform.Position.y < yMax && 
                  transform.Position.y > yMin)) {
                entity.Cull();
            }
        }
    }
}
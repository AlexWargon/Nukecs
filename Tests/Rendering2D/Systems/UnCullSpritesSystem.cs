using Unity.Burst;
using Wargon.Nukecs.Transforms;

namespace Wargon.Nukecs.Tests {
    [BurstCompile]
    public struct UnCullSpritesSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.Query()
                .With<Transform>()
                .With<SpriteRenderData>()
                .With<Culled>()
                .With<SpriteChunkReference>()
                .None<DestroyEntity>();
        }

        public void OnUpdate(ref Entity entity, ref State state) {
            var data = CullingData.instance;
            var xMax = data.xMax;
            var yMax = data.yMax;
            var xMin = data.xMin;
            var yMin = data.yMin;
            ref readonly var transform = ref entity.Read<Transform>();
            if (transform.Position.x < xMax && transform.Position.x > xMin && transform.Position.y < yMax &&
                transform.Position.y > yMin) {
                entity.Remove<Culled>();
            }
        }
    }
}
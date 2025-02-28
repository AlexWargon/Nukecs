using Unity.Burst;
using Wargon.Nukecs.Transforms;
namespace Wargon.Nukecs.Tests {
    [BurstCompile]
    public unsafe struct UpdateChunkDataSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.Query()
                .With<SpriteRenderData>()
                .With<SpriteChunkReference>()
                .With<Transform>()
                .None<Culled>();
        }

        public void OnUpdate(ref Entity entity, ref State state) {
            ref readonly var data = ref entity.Read<SpriteRenderData>();
            ref var chunkIndex = ref entity.Get<SpriteChunkReference>();
            ref readonly var transform = ref entity.Read<Transform>();
            ref var chunk = ref chunkIndex.ChunkRef;
            chunk.UpdateData(entity.id, in data, in transform);
        }
    }
}
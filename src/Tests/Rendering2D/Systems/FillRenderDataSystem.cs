using Unity.Burst;
using Wargon.Nukecs.Transforms;

namespace Wargon.Nukecs.Tests {
    [BurstCompile]
    public struct FillRenderDataSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Single;
        public Query GetQuery(ref World world) {
            return world.Query().With<SpriteRenderData>().With<Transform>().None<Culled>();
        }
        public unsafe void OnUpdate(ref Entity entity, ref State state) {
            var (chunk, transform, data) =
                entity.Read<SpriteChunkReference, Transform, SpriteRenderData>();
            if(chunk.chunk != null)
                chunk.ChunkRef.AddToFill(in entity, in transform, in data);
        }
    }
}
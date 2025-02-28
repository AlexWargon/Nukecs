namespace Wargon.Nukecs.Tests {
    
    using Unity.Burst;
    using Transform = Transforms.Transform;
    
    [BurstCompile]
    public struct FillRenderDataSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Single;
        public Query GetQuery(ref World world) {
            return world.Query().With<SpriteRenderData>().With<Transform>().None<Culled>();
        }
        public void OnUpdate(ref Entity entity, ref State state) {
            var (chunk, transform, data) = entity.Read<SpriteChunkReference, Transform, SpriteRenderData>();
            chunk.ChunkRef.AddToFill(in entity, in transform, in data);
        }
    }
}
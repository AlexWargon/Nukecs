using Unity.Burst;
using Wargon.Nukecs.Transforms;

namespace Wargon.Nukecs.Tests {
    [BurstCompile]
    public struct FillRenderDataSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Single;
        public Query GetQuery(ref World world) {
            return world.Query().With<SpriteRenderData>().With<Transform>().None<DestroyEntity>().None<Culled>();
        }
        public void OnUpdate(ref Entity entity, ref State state) {
            var (chunk, transform, data) =
                entity.Read<SpriteChunkReference, Transform, SpriteRenderData>();
            chunk.ChunkRef.AddToFill(in entity, in transform, in data);
        }
    }

    public class SpriteRender2D : SystemsGroup {
        public SpriteRender2D(ref World world) : base(ref world) {
            this.name = nameof(SpriteRender2D);
            this.Add<SpriteRenderSystem>()
                .Add<UpdateCameraCullingSystem>()
                .Add<CullSpritesSystem>()
                .Add<UnCullSpritesSystem>()
                
                .Add<SpriteAnimationSystem>()
                .Add<FillRenderDataSystem>()
                ;
        }
    }
}
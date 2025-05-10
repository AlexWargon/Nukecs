namespace Wargon.Nukecs.Tests
{
    public class SpriteRender2D : SystemsGroup {
        public SpriteRender2D(ref World world) : base(ref world) {
            this.name = nameof(SpriteRender2D);
            this
                .Add<FillRenderDataSystem>()
                .Add<SpriteRenderSystem>()
                .Add<UpdateCameraCullingSystem>()
                //.Add<CullSpritesSystem>()
                //.Add<UnCullSpritesSystem>()
                .Add<SpriteAnimationSystem>()
                
                
                ;
        }
    }
}
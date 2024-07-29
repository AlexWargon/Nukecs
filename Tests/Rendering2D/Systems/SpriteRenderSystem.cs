namespace Wargon.Nukecs.Tests {
    public struct SpriteRenderSystem : ISystem
    {
        public void OnUpdate(ref World world, float deltaTime) {
            SpriteArchetypesStorage.Singleton.OnUpdate();
        }
    }
}
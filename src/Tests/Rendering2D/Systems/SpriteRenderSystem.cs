namespace Wargon.Nukecs.Tests {
    public struct SpriteRenderSystem : ISystem
    {
        public void OnUpdate(ref State state) {
            SpriteArchetypesStorage.Singleton.OnUpdate();
        }
    }
}
namespace Wargon.Nukecs {
    public unsafe interface IComponentJobSystemUnsafe2 {
        public void OnUpdate(ref Entity entity, void* c1, void* c2, ref State state);
    }
}
namespace Wargon.Nukecs.Reactive
{
    public struct ReactAndClearSystem<T> : ISystem, IOnCreate where T : unmanaged, IComponent, IReactive
    {
        private Query query;
        public void OnCreate(ref World world)
        {
            query = world.Query().With<Changed<T>>().With<T>();
        }

        public void OnUpdate(ref State state)
        {
            foreach (ref var entity in query)
            {
                entity.Remove<Changed<T>>();
                ComponentChangeEvent<T>.Invoke(ref entity.Get<T>(), ref entity);
            }
        }
    }
}
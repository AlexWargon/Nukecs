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
                ComponentChangeEvent<T>.Invoke(in entity.Get<T>(), in entity);
            }
        }
    }

    public static partial class DefaultSystems
    {
        public static void ReactAndClearSystem<T>(in Query<T, Changed<T>>.WithEntity query) where T : unmanaged, IComponent
        {
            foreach (var (e, c1, c2) 
                     in query)
            {
                e.Remove<Changed<T>>();
                ComponentChangeEvent<T>.Invoke(in c1.Get, in e);
            }
        }
    }
    public struct ReactComponent : IRes
    {
        private int componentId;
        public void OnCreate(ref World world)
        {
            
        }

        public void OnUpdate(ref World world)
        {
            
        }
    }
}
using Unity.Burst;

namespace Wargon.Nukecs.Reactive
{
    public unsafe struct ReactiveCheckSystem<T> : IEntityJobSystem where T : unmanaged, IComponent, IReactive
    {
        public SystemMode Mode => SystemMode.Single;
        public Query GetQuery(ref World world)
        {
            return world.Query().With<T>().With<Reactive<T>>();
        }
        public void OnUpdate(ref Entity entity, ref State state)
        {
            ref var reactive = ref entity.Get<Reactive<T>>();
            var currentVersion = entity.worldPointer->entityDirtyVersion.ElementAt(entity.id);
            if (currentVersion != reactive.lastSeenVersion)
            {
                entity.Add<Changed<T>>();
                reactive.lastSeenVersion = currentVersion;
            }
        }
    }
    public static class SystemsExtensions
    {
        public static Systems AddReactive<T>(this Systems systems) where T : unmanaged, IComponent, IReactive
        {
            systems.Add<ReactAndClearSystem<T>>().Add<ReactiveCheckSystem<T>>();
            return systems;
        }
    }
}
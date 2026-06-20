using System.Collections.Generic;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Система диспетчеризации для каждого типа T. Запускается после
    /// <see cref="ReactiveCheckSystem"/> (порядок в списке onUpdate системы).
    /// Очищает очередь ChangedQueue по типам и вызывает зарегистрированные управляемые колбэки.
    /// </summary>
    public struct ReactDispatchSystem<T> : ISystem, IOnCreate
        where T : unmanaged, IComponent
    {
        private World world;
        private ReactiveStorage<T> storage;

        public void OnCreate(ref World world)
        {
            this.world = world;
            storage = ReactiveStorageRegistry<T>.GetOrCreate(world);
        }

        public void OnUpdate(ref State state)
        {
            // Wait on the check job ONLY (not all dependencies).
            ReactiveJobSync.CompleteCheck(world.Id);

            ref var ts = ref storage.TypeStateRef;
            var changed = ts.Changed;

            var len = changed.Length;
            for (int i = 0; i < len; i++)
            {
                var entityId = changed[i];
                var entity = world.GetEntity(entityId);
                if (!entity.IsValid()) continue;

                if (storage.ManagedPerEntity.TryGetValue(entityId, out var list))
                    DispatchList(list, in entity);
            }

            changed.Clear();

            // Периодическая очистка: удаляем подписки на уничтоженных сущностях.
            CleanupDeadSubscriptions();
        }

        private static void DispatchList(List<Subscription<T>> list, in Entity entity)
        {
            for (int j = list.Count - 1; j >= 0; j--)
            {
                var sub = list[j];
                ref var current = ref entity.Get<T>();
                if (sub.ManagedFilter != null && !sub.ManagedFilter(in current))
                    continue;
                sub.Managed?.Invoke(in current, in entity);
            }

            // Удаляем одноразовые (one-shots).
            for (int j = list.Count - 1; j >= 0; j--)
            {
                if (list[j].IsOnce)
                {
                    var sub = list[j];
                    list.RemoveAt(j);
                    sub.Dispose();
                }
            }
        }

        private void CleanupDeadSubscriptions()
        {
            ref var ts = ref storage.TypeStateRef;
            var alive = ts.Alive;
            for (int i = alive.Length - 1; i >= 0; i--)
            {
                var entityId = alive[i];
                var entity = world.GetEntity(entityId);
                if (entity.IsValid() && entity.Has<T>()) continue;
                storage.RemoveAllForEntity(entityId);
            }
        }
    }
}

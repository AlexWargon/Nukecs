using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Пользовательский API для реактивных подписок на сущность.
    /// </summary>
    public static class EntityReactiveExtensions
    {
        /// <summary>
        /// Подписаться на изменения компонента <typeparamref name="T"/> у этой сущности.
        /// Колбэк срабатывает в следующем кадре после обнаружения изменения.
        /// Возвращает токен, который можно передать в <see cref="OffChange{T}(Wargon.Nukecs.Entity,long)"/>.
        /// </summary>
        public static long OnChange<T>(this Entity entity, ReactDelegate<T> callback, ReactOptions options = ReactOptions.None)
            where T : unmanaged, IComponent
        {
            return Subscribe(entity, callback, options, null);
        }

        /// <summary>Подписаться с предикатом фильтрации (пропускает диспетчеризацию, если фильтр возвращает false).</summary>
        public static long OnChange<T>(this Entity entity, ReactDelegate<T> callback, ReactFilter<T> filter, ReactOptions options = ReactOptions.None)
            where T : unmanaged, IComponent
        {
            return Subscribe(entity, callback, options, filter);
        }

        /// <summary>Отписаться по токену, возвращенному из <c>OnChange</c>.</summary>
        public static void OffChange<T>(this Entity entity, long token) where T : unmanaged, IComponent
        {
            if (ReactiveStorageRegistry<T>.TryGet(entity.worldIndex, out var storage))
                storage.Remove(token);
        }

        /// <summary>Удалить все подписки типа <typeparamref name="T"/> у этой сущности.</summary>
        public static void OffChange<T>(this Entity entity) where T : unmanaged, IComponent
        {
            if (ReactiveStorageRegistry<T>.TryGet(entity.worldIndex, out var storage))
                storage.RemoveAllForEntity(entity.id);
        }

        private static long Subscribe<T>(
            Entity entity,
            ReactDelegate<T> callback,
            ReactOptions options,
            ReactFilter<T> filter)
            where T : unmanaged, IComponent
        {
            ref var world = ref entity.world;
            SystemsReactiveExtensions.EnsureRegistered<T>(world);
            var storage = ReactiveStorageRegistry<T>.GetOrCreate(world);

            var sub = new Subscription<T> { Options = options, Managed = callback };
            if (filter != null) sub.SetManagedFilter(filter);

            var token = storage.AddEntitySubscription(entity.id, sub);

            // Начальная загрузка (Bootstrap) снимка старого значения, чтобы первое изменение не вызывало ложного срабатывания.
            ref var ts = ref storage.TypeStateRef;
            if (entity.Has<T>())
            {
                if (!ts.Offsets.ContainsKey(entity.id))
                {
                    ref var current = ref entity.Get<T>();
                    unsafe
                    {
                        var newOffset = ts.AppendBytes((byte*)UnsafeUtility.AddressOf(ref current));
                        ts.Offsets.TryAdd(entity.id, newOffset);
                    }
                }
            }

            // TriggerImmediately: запускается синхронно с текущим значением, если это возможно.
            // Если T еще нет у сущности (отложенное добавление через ECB), откладываем запуск —
            // система проверки поставит сущность в очередь при первом обнаружении, и диспетчеризация
            // запустит колбэк в следующем OnUpdate (после воспроизведения ECB).
            if ((options & ReactOptions.TriggerImmediately) != 0)
            {
                if (entity.Has<T>())
                {
                    ref var v = ref entity.Get<T>();
                    sub.Managed?.Invoke(in v, in entity);
                }
                else
                {
                    sub.TriggerPending = true;
                    if (ts.PendingTriggers.IsCreated) ts.PendingTriggers[entity.id] = 1;
                }
            }

            return token;
        }
    }
}

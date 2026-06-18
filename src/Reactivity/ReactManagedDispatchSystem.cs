using System;
using System.Collections.Generic;
using Unity.Collections;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Main-thread dispatcher for managed delegates. Also performs:
    /// <list type="bullet">
    /// <item>One-shot subscription cleanup (auto-unsubscribe after fire).</item>
    /// <item>Auto-cleanup of subscriptions on dead/destroyed entities or when
    /// component <typeparamref name="T"/> was removed.</item>
    /// <item><see cref="ReactiveStorage{T}.Changed"/>.Clear() at end of frame.</item>
    /// </list>
    /// </summary>
    public struct ReactManagedDispatchSystem<T> : ISystem, IOnCreate
        where T : unmanaged, IComponent
    {
        private ReactiveStorage<T> storage;
        private World world;

        public void OnCreate(ref World world)
        {
            storage = ReactiveWorldRegistry.GetOrCreate<T>(world);
            this.world = world;
        }

        public void OnUpdate(ref State state)
        {
            var changed = storage.Changed;
            var oldValues = storage.OldValues;
            var managedPerEntity = storage.ManagedPerEntity;
            var managedWorld = storage.ManagedWorldLevel;

            var length = changed.Length;
            for (int i = 0; i < length; i++)
            {
                var entityId = changed[i];
                if (!oldValues.TryGetValue(entityId, out var value)) continue;
                var entity = world.GetEntity(entityId);
                if (!entity.IsValid()) continue;

                // Per-entity managed subs.
                if (managedPerEntity.TryGetValue(entityId, out var list))
                    DispatchList(list, in value, in entity);

                // World-level managed subs.
                if (managedWorld.Count > 0)
                    DispatchList(managedWorld, in value, in entity);
            }

            changed.Clear();

            // Periodic cleanup: scan all subscribed entity ids, drop dead ones.
            CleanupDeadSubscriptions();
        }

        private static void DispatchList(List<Subscription<T>> list, in T value, in Entity entity)
        {
            for (int j = list.Count - 1; j >= 0; j--)
            {
                var sub = list[j];
                // Managed filter only (burst filters run in ReactBurstDispatchSystem).
                if (sub.ManagedFilter != null)
                {
                    if (!sub.ManagedFilter(in value)) continue;
                }
                sub.Managed?.Invoke(in value, in entity);
            }

            // Remove one-shots (after dispatch, last-fired-first).
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
            // Walk the Alive list and drop any entries whose entity is dead or
            // no longer has T. ManagedPerEntity subs are removed along with
            // their OldValues snapshot; burst subs are also cleared.
            var alive = storage.Alive;
            for (int i = alive.Length - 1; i >= 0; i--)
            {
                var entityId = alive[i];
                var entity = world.GetEntity(entityId);
                if (entity.IsValid() && entity.Has<T>()) continue;

                storage.RemoveAllForEntity(entityId);
            }

            // Also clean world-level OldValues entries for dead entities
            // (they may have been bootstrapped without per-entity subscription).
            var oldValues = storage.OldValues;
            var keys = oldValues.GetKeyArray(Allocator.TempJob);
            for (int i = 0; i < keys.Length; i++)
            {
                var entityId = keys[i];
                var entity = world.GetEntity(entityId);
                if (entity.IsValid() && entity.Has<T>()) continue;
                oldValues.Remove(entityId);
            }
            keys.Dispose();
        }
    }
}

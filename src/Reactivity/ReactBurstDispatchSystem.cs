using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Burst-capable dispatcher for Burst subscriptions. Iterates
    /// <see cref="ReactiveStorage{T}.Changed"/> and invokes registered Burst
    /// function pointers (per-entity and world-level) via
    /// <c>FunctionPointer&lt;ReactDelegateBurst&gt;.Invoke</c>.
    ///
    /// Why non-generic delegate: <c>FunctionPointer&lt;TDelegate&gt;.Invoke</c> routes
    /// through <c>Marshal.GetDelegateForFunctionPointer</c>, which fails on generic
    /// delegate types in Mono. <c>ReactDelegateBurst</c> is non-generic
    /// (signature: <c>void(in Entity)</c>), so it works in both managed and Burst.
    /// </summary>
    public unsafe struct ReactBurstDispatchSystem<T> : ISystem, IOnCreate
        where T : unmanaged, IComponent
    {
        private NativeHashMap<int, T> oldValues;
        private ChangedQueue<int> changed;
        private NativeList<BurstSubEntry> burstSubs;
        private NativeParallelMultiHashMap <int, int> entityToBurstSubs;
        private World.WorldUnsafe* worldPtr;

        public void OnCreate(ref World world)
        {
            var storage = ReactiveWorldRegistry.GetOrCreate<T>(world);
            oldValues = storage.OldValues;
            changed = storage.Changed;
            burstSubs = storage.BurstSubs;
            entityToBurstSubs = storage.EntityToBurstSubs;
            worldPtr = world.UnsafeWorld;
        }

        public void OnUpdate(ref State state)
        {
            // Fast path: no burst subscriptions at all.
            if (burstSubs.Length == 0) return;

            var length = changed.Length;
            for (int i = 0; i < length; i++)
            {
                var entityId = changed[i];
                if (!oldValues.TryGetValue(entityId, out var value)) continue;

                var entity = worldPtr->GetEntity(entityId);

                // Per-entity burst subs.
                Dispatch(entityId, in entity);
                // World-level burst subs (registered under id == -1).
                if (entityId != -1)
                    Dispatch(-1, in entity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Dispatch(int key, in Entity entity)
        {
            if (!entityToBurstSubs.TryGetFirstValue(key, out var subIdx, out var it)) return;
            do
            {
                ref var entry = ref burstSubs.ElementAt(subIdx);
                var fnPtr = entry.FnPtr;
                if (fnPtr == IntPtr.Zero) continue;

                // Burst filter (non-generic ReactFilterBurst — works under Mono).
                if (entry.FilterPtr != IntPtr.Zero)
                {
                    var filter = new FunctionPointer<ReactFilterBurst>(entry.FilterPtr).Invoke;
                    if (!filter(in entity)) continue;
                }

                // Burst callback (non-generic ReactDelegateBurst — works under Mono).
                var cb = new FunctionPointer<ReactDelegateBurst>(fnPtr).Invoke;
                cb(in entity);
            } while (entityToBurstSubs.TryGetNextValue(out subIdx, ref it));
        }
    }
}




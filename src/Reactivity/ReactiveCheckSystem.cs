using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Detects value changes of component <typeparamref name="T"/> on entities that
    /// have at least one active subscription.
    ///
    /// The per-entity loop is <see cref="BurstCompileAttribute">Burst-compiled</see>
    /// — it accesses component data through the non-generic archetype API
    /// (byte* + MemCmp), bypassing <c>entity.Get&lt;T&gt;()</c> generic extensions
    /// which are unstable in Burst. The world-level loop (only active when
    /// <c>world.OnChange&lt;T&gt;</c> subscribers exist) runs managed because it
    /// touches <see cref="ReactiveStorage{T}"/> (a managed class).
    /// </summary>
    public unsafe struct ReactiveCheckSystem<T> : ISystem, IOnCreate
        where T : unmanaged, IComponent
    {
        // Blittable fields — usable from Burst-compiled methods.
        private NativeHashMap<int, T> oldValues;
        private ChangedQueue<int> changed;
        private NativeArray<int> flags;
        private NativeList<int> alive;
        private NativeHashMap<int, byte> pendingTriggers;
        private World.WorldUnsafe* worldPtr;
        private int typeIndex;
        private int componentSize;

        // Managed reference — used only from non-Burst methods (world-level path).
        private ReactiveStorage<T> storage;

        public void OnCreate(ref World world)
        {
            storage = ReactiveWorldRegistry.GetOrCreate<T>(world);
            oldValues = storage.OldValues;
            changed = storage.Changed;
            flags = storage.Flags;
            alive = storage.Alive;
            pendingTriggers = storage.PendingTriggers;
            worldPtr = world.UnsafeWorld;
            typeIndex = storage.TypeIndex;
            componentSize = UnsafeUtility.SizeOf<T>();
        }

        public void OnUpdate(ref State state)
        {
            // Per-entity subscribers: Burst-compiled scan of subscribed entities.
            CheckPerEntityBurst();

            // World-level subscribers: managed scan (touches storage for query lookup).
            if (flags[0] != 0)
                CheckWorldLevelManaged(ref state);
        }

        [BurstCompile]
        private void CheckPerEntityBurst()
        {
            var aliveLen = alive.Length;
            var entityLocationsPtr = worldPtr->entityLocations.Ptr;
            var archetypesListPtr = worldPtr->archetypesList.Ptr;

            for (int i = 0; i < aliveLen; i++)
            {
                var id = alive[i];

                // Resolve archetype via non-generic API.
                var loc = entityLocationsPtr[id];
                var arch = archetypesListPtr[loc.archetypeIndex].Ptr;

                // IsValid check: entity id == 0 means slot is empty.
                if (worldPtr->entities.Ptr[id].id == 0) continue;

                // Check T is on the archetype via bitmask (non-generic).
                if (!arch->Has(typeIndex)) continue;

                // Locate component data: archetype.data.Ptr + offset + row * size.
                var localIdx = arch->GetComponentLocalIndex(typeIndex);
                var offset = arch->GetComponentOffset(localIdx);
                byte* componentPtr = arch->data.Ptr + offset + loc.row * componentSize;
                T current = *(T*)componentPtr;

                // Lazy bootstrap: first observation of this entity.
                if (!oldValues.TryGetValue(id, out var old))
                {
                    oldValues.TryAdd(id, current);
                    // Consume pending trigger (deferred TriggerImmediately) from the
                    // burst-readable mirror.
                    if (pendingTriggers.IsCreated
                        && pendingTriggers.TryGetValue(id, out var p) && p != 0)
                    {
                        pendingTriggers.Remove(id);
                        changed.Enqueue(id);
                    }
                    continue;
                }

                // MemCmp current vs old (raw byte* — type-agnostic, Burst-friendly).
                if (UnsafeUtility.MemCmp(componentPtr, UnsafeUtility.AddressOf(ref old), componentSize) != 0)
                {
                    oldValues[id] = current;
                    changed.Enqueue(id);
                }
            }
        }

        private void CheckWorldLevelManaged(ref State state)
        {
            // Pull the shared world-level query from storage. Created lazily on
            // first world.OnChange<T>() which may run AFTER this system's OnCreate.
            var queryPtr = storage.WorldQueryPtr;
            if (queryPtr == null) return;

            var queryId = storage.WorldQueryId;
            var queryVersion = storage.WorldQueryVersion;
            Nukecs.Query.RestoreIfNeed(ref queryPtr, ref queryVersion, queryId, ref state.World);
            // Write back in case the pointer moved (world.queries resized).
            storage.WorldQueryPtr = queryPtr;
            storage.WorldQueryVersion = queryVersion;

            var arches = queryPtr->matchingArchetypes;
            var archesPtr = arches.Ptr;
            var archesLen = arches.Length;
            var archList = worldPtr->archetypesList.Ptr;

            for (int archI = 0; archI < archesLen; archI++)
            {
                var arch = archList[archesPtr[archI]].Ptr;
                var count = arch->count;
                var packed = arch->packedEntities.Ptr;
                for (int row = 0; row < count; row++)
                {
                    var id = packed[row];

                    // Skip entities with per-entity subscriptions — they are
                    // handled by the per-entity loop above and would double-dispatch.
                    if (storage.HasPerEntitySubscription(id)) continue;

                    var entity = worldPtr->GetEntity(id);
                    ref var current = ref entity.Get<T>();

                    if (oldValues.TryGetValue(id, out var old))
                    {
                        var changedNow = UnsafeUtility.MemCmp(
                            UnsafeUtility.AddressOf(ref current),
                            UnsafeUtility.AddressOf(ref old), componentSize) != 0;

                        if (changedNow)
                        {
                            oldValues[id] = current;
                            changed.Enqueue(id);
                        }
                    }
                    else
                    {
                        oldValues.TryAdd(id, current);
                    }
                }
            }
        }
    }
}

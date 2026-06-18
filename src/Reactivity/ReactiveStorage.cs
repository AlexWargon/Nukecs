using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Unmanaged entry describing one Burst subscription. Stored in a flat
    /// <see cref="NativeList{T}"/> so Burst systems can iterate without managed help.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct BurstSubEntry
    {
        public IntPtr FnPtr;       // ReactDelegate<T> function pointer
        public IntPtr FilterPtr;   // ReactFilter<T> function pointer (0 = no filter)
        public byte Flags;         // ReactOptions (subset relevant to burst: Once, IsBurst)
    }

    /// <summary>
    /// Polymorphic handle so the registry can dispose all per-type storages on world free.
    /// </summary>
    internal interface IReactiveStorage
    {
        void Dispose();
        int TypeIndex { get; }
    }

    /// <summary>
    /// Per-(world, type) reactive state. Lives on the managed heap (so it can hold
    /// managed delegates) but mirrors Burst-callable subscription data into
    /// <see cref="BurstSubs"/> / <see cref="EntityToBurstSubs"/> for dispatch from jobs.
    /// </summary>
    internal sealed unsafe class ReactiveStorage<T> : IReactiveStorage where T : unmanaged, IComponent
    {
        public readonly int WorldId;
        public readonly World World;

        // Snapshot of last seen value per subscribed entity.
        public NativeHashMap<int, T> OldValues;

        // EntityIds snapshot for the check system to iterate.
        public NativeList<int> Alive;

        // EntityIds pending removal (entity destroyed). Drained by the check system.
        public NativeList<int> Removals;

        // Spinlock queue of changed entity ids. Filled by the check system,
        // drained by the dispatch systems.
        public ChangedQueue<int> Changed;

        // Burst-visible flags. Index 0: HasWorldLevelSubs (0/1). Updated from managed side.
        public NativeArray<int> Flags;

        // Burst-visible pending-triggers map. Key=entityId, Value=1 (non-zero means pending).
        // Populated by Subscribe (when TriggerImmediately requested but T not on entity yet),
        // consumed by the Burst-compiled check system on first observation of T.
        public NativeHashMap<int, byte> PendingTriggers;

        // Burst-side subscription bookkeeping (parallel arrays).
        public NativeList<BurstSubEntry> BurstSubs;
        public NativeParallelMultiHashMap <int, int> EntityToBurstSubs; // entityId -> sub index
        public NativeList<int> BurstFreeSlots;                 // recyclable indices

        // Managed-side subscription bookkeeping.
        public Dictionary<int, List<Subscription<T>>> ManagedPerEntity; // entityId -> subs
        public List<Subscription<T>> ManagedWorldLevel;                 // world-level subs
        private readonly Dictionary<long, Subscription<T>> _byToken = new();
        private long _nextToken = 1;

        // Set once when systems are registered for this (world, type).
        public bool SystemsRegistered;

        // Query for world-level subscriptions (created lazily on first world.OnChange<T>()).
        // Lives in storage (not the CheckSystem) so both the subscribe path and the
        // Check system share the same QueryUnsafe — no leak per tick.
        public QueryUnsafe* WorldQueryPtr;
        public int WorldQueryId;
        public int WorldQueryVersion;
        public bool WorldQueryCreated;

        /// <summary>
        /// 0/1 flag read by Burst check system. When non-zero, the check system
        /// also walks the query (to catch changes on entities without per-entity subs).
        /// Mirrored in <see cref="Flags"/>[0] for Burst visibility.
        /// </summary>
        public int HasWorldLevelSubs
        {
            get => Flags.IsCreated && Flags.Length > 0 ? Flags[0] : 0;
            set
            {
                if (Flags.IsCreated && Flags.Length > 0) Flags[0] = value;
            }
        }

        public int TypeIndex => ComponentType<T>.Index;

        public ReactiveStorage(World world)
        {
            World = world;
            WorldId = world.Id;
            const int cap = 32;
            OldValues = new NativeHashMap<int, T>(cap, Allocator.Persistent);
            Alive = new NativeList<int>(cap, Allocator.Persistent);
            Removals = new NativeList<int>(cap, Allocator.Persistent);
            Changed = new ChangedQueue<int>(cap, Allocator.Persistent);
            BurstSubs = new NativeList<BurstSubEntry>(8, Allocator.Persistent);
            EntityToBurstSubs = new NativeParallelMultiHashMap <int, int>(8, Allocator.Persistent);
            BurstFreeSlots = new NativeList<int>(4, Allocator.Persistent);
            Flags = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            PendingTriggers = new NativeHashMap<int, byte>(4, Allocator.Persistent);
            ManagedPerEntity = new Dictionary<int, List<Subscription<T>>>();
            ManagedWorldLevel = new List<Subscription<T>>();
        }

        public void Dispose()
        {
            if (OldValues.IsCreated) OldValues.Dispose();
            if (Alive.IsCreated) Alive.Dispose();
            if (Removals.IsCreated) Removals.Dispose();
            Changed.Dispose();
            if (BurstSubs.IsCreated) BurstSubs.Dispose();
            if (EntityToBurstSubs.IsCreated) EntityToBurstSubs.Dispose();
            if (BurstFreeSlots.IsCreated) BurstFreeSlots.Dispose();
            if (Flags.IsCreated) Flags.Dispose();
            if (PendingTriggers.IsCreated) PendingTriggers.Dispose();

            foreach (var kv in ManagedPerEntity)
                foreach (var s in kv.Value) s.Dispose();
            foreach (var s in ManagedWorldLevel) s.Dispose();
            ManagedPerEntity.Clear();
            ManagedWorldLevel.Clear();
            _byToken.Clear();
        }

        // ---- subscription mutators (managed side, main thread only) ----

        public long AddEntitySubscription(int entityId, Subscription<T> sub)
        {
            sub.Token = _nextToken++;
            sub.EntityId = entityId;
            _byToken[sub.Token] = sub;

            // Burst subs go into BurstSubs/EntityToBurstSubs (invoked by
            // ReactBurstDispatchSystem via FunctionPointer<ReactDelegateBurst> —
            // non-generic delegate works under Mono). Managed subs go into
            // ManagedPerEntity (invoked by ReactManagedDispatchSystem).
            if (sub.IsBurst)
                AddBurstEntry(entityId, sub);
            else
            {
                if (!ManagedPerEntity.TryGetValue(entityId, out var list))
                {
                    list = new List<Subscription<T>>(2);
                    ManagedPerEntity[entityId] = list;
                }
                list.Add(sub);
            }

            // Track alive for check system (only for entity-level subs; world-level handled separately).
            if (entityId >= 0 && !AliveContains(entityId))
                Alive.Add(entityId);

            return sub.Token;
        }

        public long AddWorldSubscription(Subscription<T> sub)
        {
            sub.Token = _nextToken++;
            sub.EntityId = -1;
            _byToken[sub.Token] = sub;

            if (sub.IsBurst)
                AddBurstEntry(-1, sub);
            else
                ManagedWorldLevel.Add(sub);

            HasWorldLevelSubs = ManagedWorldLevel.Count > 0 || WorldLevelBurstCount() > 0 ? 1 : 0;

            // Bootstrap: when the FIRST world-level subscription arrives, snapshot
            // every existing entity with T into OldValues. Subsequent value changes
            // will then be detected as diffs on the next check tick. Without this,
            // the first check tick would bootstrap with the (already-mutated) current
            // value and miss the change.
            if (!WorldQueryCreated)
            {
                var w = World.UnsafeWorld;
                var q = World.Query().With<T>();
                WorldQueryPtr = q.queryUnsafe;
                WorldQueryId = q.id;
                WorldQueryVersion = 0;
                WorldQueryCreated = true;
                // Populate matchingArchetypes (Query created after archetype exists).
                w->RefreshArchetypes();
                BootstrapExistingEntities();
            }

            return sub.Token;
        }

        private unsafe void BootstrapExistingEntities()
        {
            var w = World.UnsafeWorld;
            var arches = WorldQueryPtr->matchingArchetypes;
            var archesPtr = arches.Ptr;
            var archesLen = arches.Length;
            var archList = w->archetypesList.Ptr;
            for (int archI = 0; archI < archesLen; archI++)
            {
                var arch = archList[archesPtr[archI]].Ptr;
                var count = arch->count;
                var packed = arch->packedEntities.Ptr;
                for (int row = 0; row < count; row++)
                {
                    var id = packed[row];
                    if (OldValues.ContainsKey(id)) continue;
                    var entity = w->GetEntity(id);
                    OldValues.TryAdd(id, entity.Get<T>());
                }
            }
        }

        private int WorldLevelBurstCount()
        {
            int n = 0;
            if (EntityToBurstSubs.TryGetFirstValue(-1, out var idx, out var it))
            {
                do { if (BurstSubs[idx].FnPtr != IntPtr.Zero) n++; }
                while (EntityToBurstSubs.TryGetNextValue(out idx, ref it));
            }
            return n;
        }

        private void AddBurstEntry(int entityId, Subscription<T> sub)
        {
            int idx;
            if (BurstFreeSlots.Length > 0)
            {
                idx = BurstFreeSlots[BurstFreeSlots.Length - 1];
                BurstFreeSlots.RemoveAtSwapBack(BurstFreeSlots.Length - 1);
                BurstSubs[idx] = new BurstSubEntry
                {
                    FnPtr = sub.BurstFnPtr,
                    FilterPtr = sub.FilterFnPtr,
                    Flags = (byte)sub.Options
                };
            }
            else
            {
                idx = BurstSubs.Length;
                BurstSubs.Add(new BurstSubEntry
                {
                    FnPtr = sub.BurstFnPtr,
                    FilterPtr = sub.FilterFnPtr,
                    Flags = (byte)sub.Options
                });
            }
            EntityToBurstSubs.Add(entityId, idx);
        }

        public bool Remove(long token)
        {
            if (!_byToken.TryGetValue(token, out var sub)) return false;
            _byToken.Remove(token);

            if (sub.IsBurst)
                RemoveBurstEntry(sub.EntityId, sub.BurstFnPtr);
            else if (sub.EntityId >= 0)
            {
                if (ManagedPerEntity.TryGetValue(sub.EntityId, out var list))
                {
                    list.Remove(sub);
                    if (list.Count == 0) ManagedPerEntity.Remove(sub.EntityId);
                }
            }
            else
                ManagedWorldLevel.Remove(sub);

            if (sub.EntityId < 0)
                HasWorldLevelSubs = ManagedWorldLevel.Count > 0 || WorldLevelBurstCount() > 0 ? 1 : 0;

            sub.Dispose();
            return true;
        }

        public void RemoveAllForEntity(int entityId)
        {
            // Remove managed
            if (ManagedPerEntity.TryGetValue(entityId, out var list))
            {
                foreach (var s in list) { _byToken.Remove(s.Token); s.Dispose(); }
                ManagedPerEntity.Remove(entityId);
            }
            // Remove burst (mark slots free)
            if (EntityToBurstSubs.TryGetFirstValue(entityId, out var idx, out var it))
            {
                do
                {
                    BurstSubs[idx] = default;
                    BurstFreeSlots.Add(idx);
                } while (EntityToBurstSubs.TryGetNextValue(out idx, ref it));
                EntityToBurstSubs.Remove(entityId);
            }

            // Remove from Alive list
            for (int i = 0; i < Alive.Length; i++)
            {
                if (Alive[i] == entityId)
                {
                    Alive.RemoveAtSwapBack(i);
                    break;
                }
            }
            OldValues.Remove(entityId);
        }

        private void RemoveBurstEntry(int entityId, IntPtr fnPtr)
        {
            if (!EntityToBurstSubs.TryGetFirstValue(entityId, out var idx, out var it)) return;
            do
            {
                if (BurstSubs[idx].FnPtr == fnPtr)
                {
                    BurstSubs[idx] = default;
                    BurstFreeSlots.Add(idx);
                    // NativeMultiHashMap does not support per-value removal cleanly;
                    // rebuild is expensive. We rebuild the entity bucket below.
                    break;
                }
            } while (EntityToBurstSubs.TryGetNextValue(out idx, ref it));
            RebuildBurstBucket(entityId);
        }

        private void RebuildBurstBucket(int entityId)
        {
            // Collect remaining indices and re-add them.
            using var keep = new NativeList<int>(4, Allocator.Temp);
            if (EntityToBurstSubs.TryGetFirstValue(entityId, out var idx, out var it))
            {
                do
                {
                    if (BurstSubs[idx].FnPtr != IntPtr.Zero) keep.Add(idx);
                } while (EntityToBurstSubs.TryGetNextValue(out idx, ref it));
            }
            EntityToBurstSubs.Remove(entityId);
            for (int i = 0; i < keep.Length; i++)
                EntityToBurstSubs.Add(entityId, keep[i]);
        }

        private bool AliveContains(int entityId)
        {
            for (int i = 0; i < Alive.Length; i++)
                if (Alive[i] == entityId) return true;
            return false;
        }

        /// <summary>
        /// True if the given entity has at least one per-entity subscription
        /// (managed or burst). Used by the world-level check loop to skip entities
        /// that are already handled by the per-entity loop (avoids double dispatch).
        /// </summary>
        public bool HasPerEntitySubscription(int entityId)
        {
            // Check `alive` instead of the dictionaries — alive is the canonical list
            // of entities with per-entity subs, kept in sync by Add/Remove.
            return AliveContains(entityId);
        }

        /// <summary>
        /// Set the burst-visible pending-trigger flag for an entity. Called from Subscribe
        /// when TriggerImmediately is requested but T is not yet on the entity.
        /// </summary>
        public void SetPendingTrigger(int entityId)
        {
            if (PendingTriggers.IsCreated) PendingTriggers[entityId] = 1;
        }

        /// <summary>
        /// Check whether any managed per-entity subscription for the given entity has
        /// <see cref="Subscription{T}.TriggerPending"/> set, and clear those flags.
        /// Returns true if at least one pending trigger was consumed (caller should
        /// enqueue the entity for dispatch).
        /// </summary>
        public bool ConsumePendingTrigger(int entityId)
        {
            // Read the burst-side mirror (kept in sync with Subscription.TriggerPending).
            if (!PendingTriggers.IsCreated) return false;
            if (!PendingTriggers.TryGetValue(entityId, out var v) || v == 0) return false;
            PendingTriggers.Remove(entityId);
            // Also clear the managed-side flag on the subscriptions.
            if (ManagedPerEntity.TryGetValue(entityId, out var list))
            {
                for (int i = 0; i < list.Count; i++)
                    list[i].TriggerPending = false;
            }
            return true;
        }
    }
}

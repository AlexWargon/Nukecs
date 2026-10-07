using System;
using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs
{
    // Session-local slot ids for generated runners; worlds store locals by the stable Key.
    internal static class LocalParamSlots
    {
        // Stable across sessions: Owner is the generator's FNV-1a of the method/parameter
        // identity, Scope the Systems container index, Instance the registration ordinal.
        internal struct Key : IEquatable<Key>
        {
            internal ulong Owner;
            internal int Scope;
            internal int Instance;
            public bool Equals(Key other) => Owner == other.Owner && Scope == other.Scope && Instance == other.Instance;
            public override int GetHashCode() => unchecked(((int)(Owner ^ (Owner >> 32)) * 397 ^ Scope) * 397 ^ Instance);
        }
        private struct RegistryKey { }
        private static readonly SharedStatic<HashMap<Key, int>> Slots = SharedStatic<HashMap<Key, int>>.GetOrCreate<RegistryKey>();
        private struct ReverseKey { }
        private static readonly SharedStatic<HashMap<int, Key>> SlotKeys = SharedStatic<HashMap<int, Key>>.GetOrCreate<ReverseKey>();
        private struct GateKey { }
        private static readonly SharedStatic<Spinner> Gate = SharedStatic<Spinner>.GetOrCreate<GateKey>();

        internal static int NextInstance(ulong owner, ref HashMap<ulong, int> registrations)
        {
            if (!registrations.IsCreated) registrations = new HashMap<ulong, int>(16, Allocator.Persistent);
            registrations.TryGetValue(owner, out var instance);
            if (!registrations.ContainsKey(owner) && registrations.Count == registrations.Capacity)
                registrations.Capacity *= 2;
            registrations[owner] = instance + 1;
            return instance;
        }

        internal static int Acquire(ulong owner, int scope, int instance)
        {
            var key = new Key { Owner = owner, Scope = scope, Instance = instance };
            Gate.Data.Acquire();
            ref var slots = ref Slots.Data;
            if (!slots.IsCreated) slots = new HashMap<Key, int>(64, Allocator.Persistent);
            if (!slots.TryGetValue(key, out var slot)) {
                slot = ResourceSlotIds.Acquire();
                if (slots.Count == slots.Capacity) slots.Capacity *= 2;
                slots.TryAdd(key, slot);
                ref var keys = ref SlotKeys.Data;
                if (!keys.IsCreated) keys = new HashMap<int, Key>(64, Allocator.Persistent);
                if (keys.Count == keys.Capacity) keys.Capacity *= 2;
                keys.TryAdd(slot, key);
            }
            Gate.Data.Release();
            return slot;
        }

        internal static bool TryGetKey(int slot, out Key key)
        {
            Gate.Data.Acquire();
            key = default;
            var found = SlotKeys.Data.IsCreated && SlotKeys.Data.TryGetValue(slot, out key);
            Gate.Data.Release();
            return found;
        }

        internal static void Dispose()
        {
            if (Slots.Data.IsCreated) Slots.Data.Dispose();
            Slots.Data = default;
            if (SlotKeys.Data.IsCreated) SlotKeys.Data.Dispose();
            SlotKeys.Data = default;
            Gate.Data = default;
            ResourceSlotIds.Reset();
        }
    }

    internal static class ResourceSlotIds
    {
        private struct CounterKey { }
        private static readonly SharedStatic<int> Next = SharedStatic<int>.GetOrCreate<CounterKey>();
        internal static int Acquire() => Interlocked.Increment(ref Next.Data) - 1;
        internal static void Reset() => Next.Data = 0;
    }
}

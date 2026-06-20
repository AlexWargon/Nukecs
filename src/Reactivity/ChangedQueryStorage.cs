using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Per-(world, type) storage for the Changed&lt;T&gt; query filter pipeline.
    /// Holds the flat oldValues byte buffer and the ChangedList of entity IDs
    /// that changed this frame. Populated by auto-generated _Fetch systems,
    /// consumed by user [System] methods with Changed&lt;T&gt; query filters.
    ///
    /// Allocator.Persistent — NOT through framework allocator. NOT serialized
    /// (managed-side, outside WorldUnsafe).
    /// </summary>
    public sealed class ChangedQueryStorage : IDisposable
    {
        public readonly int WorldId;
        public readonly int TypeIndex;
        public readonly int ComponentSize;

        public NativeList<int> ChangedList;
        public NativeHashMap<int, int> Offsets;
        public NativeList<byte> Values;

        public ChangedQueryStorage(int worldId, int typeIndex, int componentSize)
        {
            WorldId = worldId;
            TypeIndex = typeIndex;
            ComponentSize = componentSize;
            ChangedList = new NativeList<int>(64, Allocator.Persistent);
            Offsets = new NativeHashMap<int, int>(64, Allocator.Persistent);
            Values = new NativeList<byte>(256, Allocator.Persistent);
        }

        public void Dispose()
        {
            if (ChangedList.IsCreated) ChangedList.Dispose();
            if (Offsets.IsCreated) Offsets.Dispose();
            if (Values.IsCreated) Values.Dispose();
        }

        /// <summary>Append a component's raw bytes to the Values buffer, return its offset.</summary>
        public unsafe int AppendBytes(byte* src)
        {
            int start = Values.Length;
            int newLen = start + ComponentSize;
            if (newLen > Values.Capacity)
            {
                int newCap = Values.Capacity > 0 ? Values.Capacity : 16;
                while (newCap < newLen) newCap *= 2;
                Values.Capacity = newCap;
            }
            Values.ResizeUninitialized(newLen);
            UnsafeUtility.MemCpy((byte*)Values.GetUnsafePtr() + start, src, ComponentSize);
            return start;
        }
    }

    /// <summary>
    /// Static registry of <see cref="ChangedQueryStorage"/> per (worldId, typeIndex).
    /// Shared between _Fetch systems and user [System] methods that use Changed&lt;T&gt;.
    /// </summary>
    public static class ChangedQueryStorageRegistry
    {
        private static readonly Dictionary<(int, int), ChangedQueryStorage> ByKey = new();
        private static readonly object Lock = new();
        private static bool _staticCleanupHooked;

        public static ChangedQueryStorage GetOrCreate(World world, int typeIndex, int componentSize)
        {
            HookStaticCleanup();
            var worldId = world.Id;
            lock (Lock)
            {
                if (ByKey.TryGetValue((worldId, typeIndex), out var existing))
                    return existing;
                var storage = new ChangedQueryStorage(worldId, typeIndex, componentSize);
                ByKey[(worldId, typeIndex)] = storage;
                return storage;
            }
        }

        public static bool TryGet(int worldId, int typeIndex, out ChangedQueryStorage storage)
        {
            lock (Lock)
            {
                return ByKey.TryGetValue((worldId, typeIndex), out storage);
            }
        }

        public static void DisposeAll()
        {
            List<ChangedQueryStorage> snapshot;
            lock (Lock)
            {
                snapshot = new List<ChangedQueryStorage>(ByKey.Values);
                ByKey.Clear();
            }
            foreach (var s in snapshot) s.Dispose();
        }

        private static void HookStaticCleanup()
        {
            if (_staticCleanupHooked) return;
            _staticCleanupHooked = true;
            World.OnDisposeStatic(StaticCleanup);
        }

        private static void StaticCleanup()
        {
            DisposeAll();
            _staticCleanupHooked = false;
        }
    }
}

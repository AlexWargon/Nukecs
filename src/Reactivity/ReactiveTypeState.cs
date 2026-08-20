using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Per-(world, type) unmanaged state for the Burst-compiled check pipeline.
    /// All fields are blittable — the struct can live in a <see cref="NativeList{T}"/>
    /// and be addressed through a raw pointer from a Burst job.
    ///
    /// Old component values are stored in a flat <see cref="Values"/> byte buffer,
    /// indexed by <see cref="Offsets"/> (entityId → byte offset). This is what lets
    /// the check job stay non-generic: it doesn't need to know T, only the size.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ReactiveTypeState : IDisposable
    {
        public int TypeIndex;
        public int ComponentSize;

        // entityId → byte offset within Values where the oldValue snapshot lives.
        public NativeHashMap<int, int> Offsets;
        // Flat byte buffer of oldValues, tightly packed per ComponentSize.
        public NativeList<byte> Values;
        // EntityIds that have at least one per-entity subscription. Scanned by the job.
        public NativeList<int> Alive;
        // Spinlock queue filled by the check job (parallel-safe) and drained by dispatch.
        public ChangedQueue<int> Changed;
        // Burst-readable mirror of TriggerPending (deferred TriggerImmediately).
        public NativeHashMap<int, byte> PendingTriggers;

        public bool IsCreated => Values.IsCreated;

        public void Initialize(int typeIndex, int componentSize, int initialCapacity = 16)
        {
            TypeIndex = typeIndex;
            ComponentSize = componentSize;
            Offsets = new NativeHashMap<int, int>(initialCapacity, Allocator.Persistent);
            Values = new NativeList<byte>(initialCapacity * componentSize, Allocator.Persistent);
            Alive = new NativeList<int>(initialCapacity, Allocator.Persistent);
            Changed = new ChangedQueue<int>(initialCapacity, Allocator.Persistent);
            PendingTriggers = new NativeHashMap<int, byte>(4, Allocator.Persistent);
        }

        /// <summary>Append a raw byte block to <see cref="Values"/> and return its offset.</summary>
        public int AppendBytes(byte* src)
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

        public void Dispose()
        {
            if (Offsets.IsCreated) Offsets.Dispose();
            if (Values.IsCreated) Values.Dispose();
            if (Alive.IsCreated) Alive.Dispose();
            Changed.Dispose();
            if (PendingTriggers.IsCreated) PendingTriggers.Dispose();
        }
    }
}

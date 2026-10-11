using Wargon.Nukecs.Collections;
using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Per-(world, type) unmanaged state for the Burst-compiled check pipeline.
    /// All fields are blittable — the struct can live in a <see cref="HeapList{T}"/>
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
        public HeapHashMap<int, int> Offsets;
        // Flat byte buffer of oldValues, tightly packed per ComponentSize.
        public HeapList<byte> Values;
        // EntityIds that have at least one per-entity subscription. Scanned by the job.
        public HeapList<int> Alive;
        // Spinlock queue filled by the check job (parallel-safe) and drained by dispatch.
        public ChangedQueue<int> Changed;
        // Burst-readable mirror of TriggerPending (deferred TriggerImmediately).
        public HeapHashMap<int, byte> PendingTriggers;

        public bool IsCreated => Values.IsCreated;

        public void Initialize(int typeIndex, int componentSize, int initialCapacity = 16)
        {
            TypeIndex = typeIndex;
            ComponentSize = componentSize;
            Offsets = new HeapHashMap<int, int>(initialCapacity, AllocatorHandle.Persistent);
            Values = new HeapList<byte>(initialCapacity * componentSize, AllocatorHandle.Persistent);
            Alive = new HeapList<int>(initialCapacity, AllocatorHandle.Persistent);
            Changed = new ChangedQueue<int>(initialCapacity, AllocatorHandle.Persistent);
            PendingTriggers = new HeapHashMap<int, byte>(4, AllocatorHandle.Persistent);
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
            Mem.MemCpy((byte*)Values.GetUnsafePtr() + start, src, ComponentSize);
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

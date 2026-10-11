using System;
using System.Runtime.CompilerServices;

namespace Wargon.Nukecs
{
    /// <summary>
    /// Heap allocator label used by the core (Persistent / Temp / TempJob). Numerically equal to
    /// Unity's <c>Unity.Collections.Allocator</c>; in Unity both convert implicitly, so APIs that
    /// take an AllocatorHandle accept <c>Allocator.Persistent</c> as before.
    /// Outside Unity, Temp and TempJob are ordinary heap memory and must be freed explicitly.
    /// </summary>
    public readonly struct AllocatorHandle : IEquatable<AllocatorHandle>
    {
        public readonly int Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AllocatorHandle(int value) => Value = value;

        public static AllocatorHandle Invalid => new AllocatorHandle(0);
        public static AllocatorHandle None => new AllocatorHandle(1);
        public static AllocatorHandle Temp => new AllocatorHandle(2);
        public static AllocatorHandle TempJob => new AllocatorHandle(3);
        public static AllocatorHandle Persistent => new AllocatorHandle(4);

        public bool IsValid => Value != 0;

#if UNITY_5_3_OR_NEWER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator AllocatorHandle(Unity.Collections.Allocator allocator) => new AllocatorHandle((int)allocator);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Unity.Collections.Allocator(AllocatorHandle handle) => (Unity.Collections.Allocator)handle.Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Unity.Collections.AllocatorManager.AllocatorHandle(AllocatorHandle handle) =>
            (Unity.Collections.Allocator)handle.Value;
#endif

        public bool Equals(AllocatorHandle other) => Value == other.Value;
        public override bool Equals(object obj) => obj is AllocatorHandle other && Equals(other);
        public override int GetHashCode() => Value;
        public static bool operator ==(AllocatorHandle a, AllocatorHandle b) => a.Value == b.Value;
        public static bool operator !=(AllocatorHandle a, AllocatorHandle b) => a.Value != b.Value;
        public override string ToString() => Value switch
        {
            0 => "Invalid", 1 => "None", 2 => "Temp", 3 => "TempJob", 4 => "Persistent", _ => $"Allocator({Value})"
        };
    }
}

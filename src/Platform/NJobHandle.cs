using System;
using System.Runtime.CompilerServices;

namespace Wargon.Nukecs
{
    /// <summary>
    /// Dependency handle of a scheduled job. In Unity it holds Unity's <c>JobHandle</c> (same layout)
    /// and both convert implicitly, so Unity jobs take and return <c>State.Dependencies</c> as before.
    /// Outside Unity it identifies a job of the Nukecs scheduler.
    /// </summary>
    public struct NJobHandle : IEquatable<NJobHandle>
    {
#if UNITY_5_3_OR_NEWER
        internal Unity.Jobs.JobHandle value;

        public bool IsCompleted
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => value.IsCompleted;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Complete() => value.Complete();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NJobHandle Combine(NJobHandle a, NJobHandle b) =>
            Unity.Jobs.JobHandle.CombineDependencies(a.value, b.value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NJobHandle Combine(NJobHandle a, NJobHandle b, NJobHandle c) =>
            Unity.Jobs.JobHandle.CombineDependencies(a.value, b.value, c.value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator NJobHandle(Unity.Jobs.JobHandle handle) => new NJobHandle { value = handle };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Unity.Jobs.JobHandle(NJobHandle handle) => handle.value;

        public bool Equals(NJobHandle other) => value.Equals(other.value);
        public override int GetHashCode() => value.GetHashCode();
#else
        // Every job of the current scheduler finishes inside Schedule, so any handle is complete.
        internal ulong id;
        internal int version;

        public bool IsCompleted => true;

        public void Complete() { }

        public static NJobHandle Combine(NJobHandle a, NJobHandle b) => default;

        public static NJobHandle Combine(NJobHandle a, NJobHandle b, NJobHandle c) => default;

        public bool Equals(NJobHandle other) => id == other.id && version == other.version;
        public override int GetHashCode() => id.GetHashCode() * 397 ^ version;
#endif

        /// <summary>True for the default handle, which depends on nothing.</summary>
        public bool IsDefault
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Equals(default);
        }

        public override bool Equals(object obj) => obj is NJobHandle other && Equals(other);
        public static bool operator ==(NJobHandle a, NJobHandle b) => a.Equals(b);
        public static bool operator !=(NJobHandle a, NJobHandle b) => !a.Equals(b);
    }
}

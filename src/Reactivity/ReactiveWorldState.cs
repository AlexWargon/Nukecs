using System.Collections.Generic;
using Unity.Collections;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Per-world registry of all reactive type states. Held by
    /// <see cref="ReactiveWorldRegistry"/> and addressed by the non-generic
    /// <see cref="ReactiveCheckSystem"/> when scheduling the check job.
    ///
    /// This is a CLASS (not struct) intentionally — modern Unity.Collections
    /// stores <see cref="NativeList{T}.Length"/> as an inline field of the
    /// NativeList struct, so a struct containing a NativeList would copy the
    /// Length field on assignment and mutations made through the copy wouldn't
    /// be visible to other holders of the original.
    /// </summary>
    public sealed class ReactiveWorldState : System.IDisposable
    {
        // Flat list of type states — the Burst job iterates this via raw pointer.
        public NativeList<ReactiveTypeState> TypeStates;
        // typeIndex → index in TypeStates.
        public NativeHashMap<int, int> TypeIndexToStateIdx;

        public bool IsCreated => TypeStates.IsCreated;

        public void Initialize()
        {
            TypeStates = new NativeList<ReactiveTypeState>(4, Allocator.Persistent);
            TypeIndexToStateIdx = new NativeHashMap<int, int>(4, Allocator.Persistent);
        }

        public ref ReactiveTypeState GetOrCreate(int typeIndex, int componentSize)
        {
            if (TypeIndexToStateIdx.TryGetValue(typeIndex, out var idx))
                return ref TypeStates.ElementAt(idx);

            idx = TypeStates.Length;
            // Grow TypeStates — note: this may move the underlying buffer. Existing
            // pointers obtained from GetUnsafePtr() become invalid; callers must
            // re-fetch. We never cache the pointer across mutations.
            TypeStates.ResizeUninitialized(idx + 1);
            ref var state = ref TypeStates.ElementAt(idx);
            state.Initialize(typeIndex, componentSize);
            TypeIndexToStateIdx.TryAdd(typeIndex, idx);
            return ref state;
        }

        public bool TryGet(int typeIndex, out int stateIdx)
        {
            return TypeIndexToStateIdx.TryGetValue(typeIndex, out stateIdx);
        }

        public void Dispose()
        {
            if (TypeStates.IsCreated)
            {
                for (int i = 0; i < TypeStates.Length; i++)
                    TypeStates.ElementAt(i).Dispose();
                TypeStates.Dispose();
            }
            if (TypeIndexToStateIdx.IsCreated) TypeIndexToStateIdx.Dispose();
        }
    }
}

using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Burst-compiled check job. Iterates all reactive type states for a world in
    /// parallel (one type per worker). For each type, scans the per-entity
    /// <c>Alive</c> list, finds the component's byte* in the archetype via the
    /// non-generic API, and compares against the stored oldValue via MemCmp.
    ///
    /// The job is fully non-generic (oldValues stored as raw bytes), which is what
    /// makes Burst compilation stable — no generic specialization is needed.
    /// </summary>
    [BurstCompile]
    public unsafe struct ReactiveCheckJob : IJobParallelFor
    {
        [NativeDisableUnsafePtrRestriction]
        public World.WorldUnsafe* WorldPtr;

        [NativeDisableUnsafePtrRestriction]
        public ReactiveTypeState* States;

        public void Execute(int stateIdx)
        {
            ref var state = ref States[stateIdx];
            var sz = state.ComponentSize;
            var typeIdx = state.TypeIndex;

            var entityLocationsPtr = WorldPtr->entityLocations.Ptr;
            var archetypesListPtr = WorldPtr->archetypesList.Ptr;
            var entitiesPtr = WorldPtr->entities.Ptr;
            var valuesBase = (byte*)state.Values.GetUnsafePtr();
            var alive = state.Alive;
            var offsets = state.Offsets;
            var changed = state.Changed;
            var pending = state.PendingTriggers;

            // Scan subscribed entities. Each worker handles one type state,
            // so there is no contention here.
            for (int i = 0; i < alive.Length; i++)
            {
                var id = alive[i];

                // IsValid: slot is empty if id==0.
                if (entitiesPtr[id].id == 0) continue;

                var loc = entityLocationsPtr[id];
                var arch = archetypesListPtr[loc.archetypeIndex].Ptr;
                if (!arch->Has(typeIdx)) continue;

                var localIdx = arch->GetComponentLocalIndex(typeIdx);
                var offset = arch->GetComponentOffset(localIdx);
                byte* currentPtr = arch->data.Ptr + offset + loc.row * sz;

                if (offsets.TryGetValue(id, out var oldOffset))
                {
                    byte* oldPtr = valuesBase + oldOffset;
                    if (UnsafeUtility.MemCmp(currentPtr, oldPtr, sz) != 0)
                    {
                        UnsafeUtility.MemCpy(oldPtr, currentPtr, sz);
                        changed.EnqueuePar(id);
                    }
                }
                else
                {
                    // Bootstrap: copy current value into the flat buffer.
                    var newOffset = state.AppendBytes(currentPtr);
                    offsets.TryAdd(id, newOffset);

                    // Consume pending trigger (deferred TriggerImmediately).
                    if (pending.TryGetValue(id, out var p) && p != 0)
                    {
                        pending.Remove(id);
                        changed.EnqueuePar(id);
                    }
                }
            }
        }
    }
}

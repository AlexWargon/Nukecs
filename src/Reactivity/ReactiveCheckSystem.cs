using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Non-generic check system. Schedules <see cref="ReactiveCheckJob"/> once per
    /// frame for the current world. The job processes every reactive type state in
    /// parallel (one type per worker).
    ///
    /// Only one instance of this system exists per world (registered automatically
    /// on the first <c>OnChange&lt;T&gt;</c>). Replaces N per-T generic check systems
    /// with a single Burst-compiled pipeline.
    /// </summary>
    public unsafe struct ReactiveCheckSystem : ISystem, IOnCreate
    {
        private World.WorldUnsafe* worldPtr;
        private ReactiveWorldState worldState;

        public void OnCreate(ref World world)
        {
            worldPtr = world.UnsafeWorld;
            worldState = ReactiveWorldRegistry.GetOrCreate(world);
        }

        public void OnUpdate(ref State state)
        {
            var count = worldState.TypeStates.Length;
            if (count == 0) return;

            var statesPtr = (ReactiveTypeState*)worldState.TypeStates.GetUnsafePtr();
            state.Dependencies = new ReactiveCheckJob
            {
                WorldPtr = worldPtr,
                States = statesPtr,
            }.Schedule(count, 1, state.Dependencies);
            // Store handle so dispatch can wait on THIS job only (not all dependencies).
            ReactiveJobSync.SetCheckHandle(worldPtr->Id, state.Dependencies);
        }
    }
}

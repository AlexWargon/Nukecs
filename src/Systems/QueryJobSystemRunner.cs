using Unity.Jobs;

namespace Wargon.Nukecs
{
    internal class QueryJobSystemRunner<TSystem> : ISystemRunner where TSystem : struct, IQueryJobSystem {
        public TSystem System;
        public Query Query;
        public SystemMode Mode;
        public ECBJob EcbJob;

        public JobHandle Schedule(UpdateContext updateContext, ref State state)
        {
            ref var world = ref state.World;
            if (Mode == SystemMode.Main) {
                world.CurrentContext = updateContext;
                System.OnUpdate(ref Query, state.DeltaTime);
                EcbJob.ECB = world.GetEcbVieContext(updateContext);
                EcbJob.world = world;
                EcbJob.Run();
            }
            else {
                state.Dependencies = System.Schedule(ref Query, state.DeltaTime, Mode, state.Dependencies);
                EcbJob.ECB = world.GetEcbVieContext(updateContext);
                EcbJob.world = world;
                state.Dependencies = EcbJob.Schedule(state.Dependencies);
            }
            return state.Dependencies;
        }

        public void Run(ref State state) {
            for (int i = 0; i < Query.Count; i++) {
                System.OnUpdate(ref Query, state.DeltaTime);
            }
            state.World.ECB.Playback(ref state.World);
        }
    }
}
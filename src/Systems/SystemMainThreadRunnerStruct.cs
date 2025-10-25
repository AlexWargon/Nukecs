using Unity.Jobs;

namespace Wargon.Nukecs
{
    internal class SystemMainThreadRunnerStruct<TSystem> : ISystemRunner where TSystem : struct, ISystem {
        internal TSystem System;
        internal ECBJob EcbJob;
        public string Name => System.GetType().Name;
#if NUKECS_DEBUG
        private Marker _marker;
#endif
        public JobHandle Schedule(UpdateContext updateContext, ref State state)
        {
#if NUKECS_DEBUG
            _marker.Autostart(System);
#endif
            ref var world = ref state.World;
            System.OnUpdate(ref state);
            EcbJob.ECB = world.GetEcbVieContext(updateContext);
            EcbJob.ECB.PlaybackMainThread(ref world);
#if NUKECS_DEBUG
            _marker.End();
#endif
            return state.Dependencies;
        }

        public void Run(ref State state) {
            System.OnUpdate(ref state);
            state.World.ECB.Playback(ref state.World);
        }
    }
}
using Unity.Jobs;

namespace Wargon.Nukecs
{
    internal class SystemMainThreadRunnerClass<TSystem> : ISystemRunner where TSystem : class, ISystem, new() {
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
            System.OnUpdate(ref state);
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
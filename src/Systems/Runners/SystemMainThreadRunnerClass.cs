using System.Reflection;
using Unity.Jobs;

namespace Wargon.Nukecs
{
    internal class SystemMainThreadRunnerClass<TSystem> : ISystemRunner, Systems.ISystemWithDeserialization where TSystem : class, ISystem, new() {
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

        public void OnWorldDeserialize(World world) {
            FixQueryFields(world);
            if (System is IOnWorldDeserialize deser)
                deser.OnWorldDeserialize(ref world);
        }

        private void FixQueryFields(World world) {
            var fields = typeof(TSystem).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields) {
                if (field.FieldType == typeof(Query)) {
                    var q = (Query)field.GetValue(System);
                    q.FixAfterDeserialize(world);
                    field.SetValue(System, q);
                }
            }
        }
    }
}
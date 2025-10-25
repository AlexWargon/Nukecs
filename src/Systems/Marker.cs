namespace Wargon.Nukecs {
    public struct Marker {
        private Unity.Profiling.ProfilerMarker _marker;
        public bool isCreated;
        public Marker(string name) {
            _marker = new Unity.Profiling.ProfilerMarker($"NUKECS.{name}");
            isCreated = true;
        }

        public void Autostart<TContext>(TContext ctx) {
            if (isCreated == false) {
                _marker = new Unity.Profiling.ProfilerMarker($"NUKECS.{ctx.GetType().Name}");
            }
            _marker.Begin();
        }
        public void Start() => _marker.Begin();
        public void End() => _marker.End();

    }
}
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Wargon.Nukecs {
    public abstract class WorldBaker : MonoBehaviour {
        public string path;
        private IOnUpdate _onUpdate;
        private World _runtimeWorld;
        private Systems _systems;
        internal bool HasRuntimeWorld => _runtimeWorld.IsAlive;
        internal bool IsRuntimeReady => _systems != null && _runtimeWorld.IsAlive;
        private string FullPath => Path.Combine(path, $"{name}.wrld");

        private async void Awake() {
            await LoadAsync();
        }

        private void Update() {
            _onUpdate?.OnUpdate(ref _systems.State);
            _systems?.OnUpdate(Time.deltaTime, Time.time);
        }

        private void OnDestroy() {
            if (_runtimeWorld.IsAlive) {
                _runtimeWorld.Dispose();
            }
        }

        internal void Load() {
            if (HasRuntimeWorld) throw new System.InvalidOperationException("A runtime world is already loaded.");
            _runtimeWorld = World.Create(WorldConfig.Default16384);
            try {
                World.Load(FullPath, ref _runtimeWorld);
                InitializeRuntime();
            }
            catch {
                Cleanup();
                throw;
            }
        }

        internal async Task LoadAsync() {
            if (HasRuntimeWorld) throw new System.InvalidOperationException("A runtime world is already loaded.");
            _runtimeWorld = World.Create(WorldConfig.Default16384);
            dbug.log("Loading world...");
            try {
                _runtimeWorld = await World.LoadAsync(FullPath, _runtimeWorld);
                InitializeRuntime();
                dbug.log("World loaded!");
            }
            catch {
                Cleanup();
                throw;
            }
        }

        private void InitializeRuntime() {
            _systems = new Systems(ref _runtimeWorld);
            _systems.AddDefaults();
            AddSystems(_systems);
            if (this is IOnCreate onCreate) onCreate.OnCreate(ref _runtimeWorld);
            if (this is IOnUpdate onUpdate) _onUpdate = onUpdate;
        }

        internal Task Save() {
            if (!IsRuntimeReady) throw new System.InvalidOperationException("The runtime world is not ready.");
            return _runtimeWorld.SaveToFileAsync(FullPath);
        }

        protected abstract void AddSystems(Systems systems);


        internal async Task BakeInternal() {
            var world = World.Create(WorldConfig.Default16384);
            try {
                Bake(ref world);
                world.Update();
                await world.SaveToFileAsync(FullPath);
            }
            finally {
                if (world.IsAlive) world.Dispose();
            }
        }
        /// <summary>
        /// Bake all world data to file.
        /// It will be loaded on awake
        /// </summary>
        public abstract void Bake(ref World world);

        private void Cleanup() {
            if (_runtimeWorld.IsAlive) _runtimeWorld.Dispose();
            _runtimeWorld = default;
            _systems = null;
            _onUpdate = null;
        }
    }
}

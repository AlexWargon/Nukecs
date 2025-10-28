using System.IO;
using System.Threading.Tasks;
using TriInspector;
using UnityEngine;

namespace Wargon.Nukecs {
    public abstract class WorldBaker : MonoBehaviour {
        public string path;
        private IOnUpdate _onUpdate;
        private World _runtimeWorld;
        private Systems _systems;
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
                World.DisposeStatic();
            }
        }

        [Button]
        private void Load() {
            _runtimeWorld = World.Create(WorldConfig.Default16384);
            ;

            World.Load(FullPath, ref _runtimeWorld);
            _systems = new Systems(ref _runtimeWorld);
            _systems.AddDefaults();
            AddSystems(_systems);
            if (this is IOnCreate onCreate) onCreate.OnCreate(ref _runtimeWorld);
            if (this is IOnUpdate onUpdate) _onUpdate = onUpdate;
        }

        [Button]
        private async Task LoadAsync() {
            _runtimeWorld = World.Create(WorldConfig.Default16384);
            ;
            dbug.log("Loading world...");

            await World.LoadAsync(FullPath, _runtimeWorld);
            _systems = new Systems(ref _runtimeWorld);
            _systems.AddDefaults();
            AddSystems(_systems);
            if (this is IOnCreate onCreate) onCreate.OnCreate(ref _runtimeWorld);
            if (this is IOnUpdate onUpdate) _onUpdate = onUpdate;
        }

        [Button]
        private void Save() {
            _runtimeWorld.SaveToFileAsync(FullPath);
        }

        protected abstract void AddSystems(Systems systems);


        [Button]
        private async void BakeInternal() {
            var world = World.Create(WorldConfig.Default16384);
            Bake(ref world);
            world.Update();
            await world.SaveToFileAsync(FullPath);
            world.Dispose();
            World.DisposeStatic();
        }
        /// <summary>
        /// Bake all world data to file.
        /// It will be loaded on awake
        /// </summary>
        public abstract void Bake(ref World world);

        private void Cleanup() { }
    }
}
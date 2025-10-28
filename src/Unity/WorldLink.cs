using System.IO;
using System.Threading.Tasks;
using TriInspector;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;
using Transform = Wargon.Nukecs.Transforms.Transform;

namespace Wargon.Nukecs {
    public abstract class WorldLink : MonoBehaviour {
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

        private float range(float a, float b) {
            return Random.Range(a, b);
        }

        private void FakeLoad() {
            _runtimeWorld = World.Create(WorldConfig.Default16384);
            _systems = new Systems(ref _runtimeWorld);
            _systems.AddDefaults()
                ;
            for (var i = 0; i < 1000; i++) {
                var scale = range(1f, 2f);
                var e = _runtimeWorld.Entity();
                e.Add(new Transform {
                    Position = new float3(range(-55f, 55f), 0, range(-55f, 55f)),
                    Rotation = quaternion.RotateY(range(0, 360f)),
                    Scale = new float3(scale, scale, scale)
                });
            }
        }

        [Button]
        private void Load() {
            _runtimeWorld = World.Create(WorldConfig.Default16384);
            ;
            dbug.log("Loading world...");

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
        public void BakeInternal() {
            var world = World.Create(WorldConfig.Default16384);
            Bake(ref world);
            world.Update();
            world.SaveToFileAsync(FullPath);
            world.Dispose();
            World.DisposeStatic();
        }

        public abstract void Bake(ref World world);

        private void Cleanup() { }
    }
}
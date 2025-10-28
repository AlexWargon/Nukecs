using System;
using System.IO;
using TriInspector;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Wargon.Nukecs
{
    public abstract class WorldLink : MonoBehaviour
    {
        public string path;
        public string name;
        private string fullPath => Path.Combine(path, $"{name}.wrld");
        private World _runtimeWorld;
        private Systems _systems;

        private void Awake()
        {
            _runtimeWorld = World.Create(WorldConfig.Default16384);
            _systems = new Systems(ref _runtimeWorld);
            _systems.AddDefaults()
                ;
            for (int i = 0; i < 1000; i++)
            {
                var scale = range(1f, 2f);
                var e = _runtimeWorld.Entity();
                e.Add(new Wargon.Nukecs.Transforms.Transform
                {
                    Position= new float3(range(-55f, 55f), 0, range(-55f, 55f)),
                    Rotation = quaternion.RotateY(range(0, 360f)),
                    Scale = new float3(scale, scale, scale)
                });
            }
        }
        private float range(float a, float b) => Random.Range(a, b);
        [Button]
        private void Load()
        {
            // dbug.log("create world link");
            // _runtimeWorld = World.Create(WorldConfig.Default16384);

                ;
            dbug.log("Loading world...");
            
            World.Load(fullPath, ref _runtimeWorld);
            _systems = new Systems(ref _runtimeWorld);
            _systems.AddDefaults()
                ;
        }
        [Button]
        private void Save()
        {
            _runtimeWorld.SaveToFileAsync(fullPath);
        }
        protected virtual void OnUpdate(){}
        private void Update()
        {
            OnUpdate();
            _systems?.OnUpdate(Time.deltaTime, Time.time);
        }

        private void OnDestroy()
        {
            if (_runtimeWorld.IsAlive)
            {
                _runtimeWorld.Dispose();
                World.DisposeStatic();
            }
        }

        [Button]
        public void BakeInternal()
        {
            var world = World.Create(WorldConfig.Default16384);
            var systems = new Systems(ref world);
            Bake(ref world);
            world.Update();
            world.SaveToFileAsync(fullPath);
            world.Dispose();
            World.DisposeStatic();
        }

        public abstract void Bake(ref World world);
        
        private void Cleanup()
        {
            
        }
    }
}
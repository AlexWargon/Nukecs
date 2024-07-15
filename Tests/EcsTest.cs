using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs.Tests {
    public unsafe class EcsTest : MonoBehaviour {
        private World world;
        private Systems systems;
        public EntityLink link;
        void Awake() {

            world = World.Create();
            systems = new Systems(ref world);
            systems
                .Add<ViewSystem>()
                .Add<MoveSystem>()
                ;
            //link.Convert(ref world);
            //Debug.Log(ComponentsMap.Index(typeof(View)));
            //var pool = GenericPool.Create(typeof(View), 256, Allocator.Persistent);
            for (int i = 0; i < 100; i++)
            {
                var e = world.CreateEntity();
                e.Add(new Speed{value = 10f});
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(cube.GetComponent<BoxCollider>());
                e.Add(new View {
                    value = cube
                });
                e.Add(new Transform {
                    position = RandomEx.Float3(-2.0f,2.0f)
                });
                
                // e.Add(new C1());
                // e.Add(new C2());
            }
        }
        
        // Update is called once per frame
        private void Update() {
            systems.OnUpdate(Time.deltaTime);
            //systems.Run(Time.deltaTime);
        }

        private void OnDestroy() {
            
            world.Dispose();
        }
    }
    [BurstCompile]
    public struct ViewSystem : ISystem, ICreate {
        private Query Query;
        public void OnCreate(ref World world) {
            Query = world.CreateQuery().With<View>().With<Transform>();
        }

        public void OnUpdate(ref World world, float deltaTime) {
            for (var i = 0; i < Query.Count; i++) {
                ref var entity = ref Query.GetEntity(i);
                ref var view = ref entity.Get<View>();
                ref var transform = ref entity.Get<Transform>();
                if (float.IsNaN(transform.position.x)) {
                    continue;
                }
                view.value.Value.transform.position = transform.position;
                view.value.Value.transform.rotation = transform.rotation;
            }
        }
    }
    [BurstCompile]
    public struct MoveSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<Transform>().With<Speed>();
        }
        public void OnUpdate(ref Entity entity, float deltaTime) {
            ref var transform = ref entity.Get<Transform>();
            var speed = entity.Read<Speed>();
            transform.position += speed.value * deltaTime * math.right();
            if (transform.position.x > 20f) entity.Destroy();

        }
    }

    [BurstCompile]
    public struct TestSystem2 : IJobSystem, ICreate {
        private Query _query;

        public void OnCreate(ref World world) {
            _query = world.CreateQuery().None<Money>().With<Player>();
        }

        public void OnUpdate(ref World world, float deltaTime) {
            if (_query.Count > 0) {
                //Log();
            }

            for (var i = 0; i < _query.Count; i++) {

                ref var e = ref _query.GetEntity(i);
            }
        }
        [BurstDiscard]
        private void Log() {
            //Debug.Log(_query.Count);
        }
    }
    [BurstCompile]
    public struct BurstTest {
        public void Execute() {
            
        }
    }
    [Serializable]
    public struct HP : IComponent {
        public int value;
    }
    public struct Player : IComponent { }
    [Serializable]
    public struct Money : IComponent {
        public int amount;
        public override string ToString() {
            return $"Money.amount ={amount.ToString()}";
        }
    }

    [Serializable]
    public struct View : IComponent {
        public UnityObjectRef<GameObject> value;
    }
    [Serializable]
    public struct Speed : IComponent {
        public float value;
    }

    public static class RandomEx {
        public static float3 Float3(float min, float max) {
            return new float3(UnityEngine.Random.Range(min, max),UnityEngine.Random.Range(min, max),UnityEngine.Random.Range(min, max));
        }
    }
    [BurstCompile]
    public struct MigrationTest1 : IEntityJobSystem {
        [NativeSetThreadIndex] internal int ThreadIndex;
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<C1>().With<C2>().None<C3>();
        }
        public void OnUpdate(ref Entity entity, float deltaTime) {
            entity.Add(new C3());
        }
    }
    [BurstCompile]
    public struct MigrationTest2 : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<C1>().With<C2>().With<C3>();
        }

        public void OnUpdate(ref Entity entity, float deltaTime) {
            entity.Remove<C3>();
        }
    }
    public struct C1 : IComponent{}
    public struct C2 : IComponent{}
    public struct C3 : IComponent{}
}

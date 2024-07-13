using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Wargon.Nukecs.Tests {
    public unsafe class EcsTest : MonoBehaviour {
        private World world;
        private Systems systems;

        void Start() {
            // var mask = new DynamicBitmask(128);
            //
            //
            // mask.Add(5);
            //
            // mask.Add(123);
            //
            // Debug.Log(mask.Has(5));
            // Debug.Log(mask.Has(124));
            // Debug.Log(mask.Has(123));
            // Debug.Log(mask.Has(1));
            // mask.Dispose();
            world = World.Create();
            systems = new Systems(ref world);
            systems
                .Add<ViewSystem>()
                .Add<MoveSystem>()
                
                ;

            for (int i = 0; i < 50; i++)
            {
                var e = world.CreateEntity();
                e.Add(new Speed{value = 10f});
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(cube.GetComponent<BoxCollider>());
                e.Add(new View {
                    value = cube
                });
                e.Add(new Transform() {
                    position = RandomEx.Float3(-2.0f,2.0f)
                });
            }

            //Debug.unityLogger.logEnabled = false;
            // Debug.Log($"{e.Get<HP>().value}");
            // Debug.Log($"{e.Has<Speed>()}");
            // Debug.Log($"{e.Has<HP>()}");
            // Debug.Log($"{e.Has<Money>()}");
            // Debug.Log($"{e.Has<Player>()}");
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
            Query = world.CreateQuery().None<Dead>().With<View>().With<Transform>();
        }

        public void OnUpdate(ref World world, float deltaTime) {
            for (var i = 0; i < Query.Count; i++) {
                ref var entity = ref Query.GetEntity(i);
                ref var view = ref entity.Get<View>();
                ref var transform = ref entity.Get<Transform>();
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
            ref var speed = ref entity.Get<Speed>();
            transform.position += speed.value * deltaTime * math.right();
            if (transform.position.x > 50f) entity.Add(new Dead());
        }
    }
    [BurstCompile]
    public struct TestSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        private Query _query;
        public Query GetQuery(ref World world)
        {
            _query = world.CreateQuery().With<Money>().None<Dead>();
            return _query;
        }
        
        public void OnUpdate(ref Entity e, float deltaTime) {
            ref var money = ref e.Get<Money>();
            money.amount++;
            //Log(ref money);
            if (money.amount >= 1200) {
                //if(e.Has<Dead>())
                    e.Remove<Money>();
                //e.Add(new Dead());
                
            }
        }
        
        [BurstDiscard]
        private void Log(ref Money money) {
            Debug.Log($"YOU ARE MILLINER {money.amount}");
        }
        [BurstDiscard]
        private void Log2(ref Money money) {
            Debug.Log($" {money.amount}");
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
    public struct HP : IComponent {
        public int value;
    }
    public struct Player : IComponent { }

    public struct Money : IComponent {
        public int amount;
        public override string ToString() {
            return $"Money.amount ={amount.ToString()}";
        }
    }

    public struct Dead : IComponent { }

    public struct View : IComponent {
        public UnityObjectRef<GameObject> value;
    }

    public struct Speed : IComponent {
        public float value;
    }

    public static class RandomEx {
        public static float3 Float3(float min, float max) {
            return new float3(UnityEngine.Random.Range(min, max),UnityEngine.Random.Range(min, max),UnityEngine.Random.Range(min, max));
        }
    }
}

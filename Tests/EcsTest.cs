using Unity.Burst;
using Unity.Collections;
using UnityEngine;

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
                ;
            for (int i = 0; i < 1; i++)
            {
                var e = world.CreateEntity();
                e.Add(new Speed{value = 5f});
                e.Add(new View {
                    value = GameObject.CreatePrimitive(PrimitiveType.Cube)
                });
            }
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

    public struct ViewSystem : ISystem, ICreate {
        private Query Query;
        public void OnCreate(ref World world) {
            Query =  world.CreateQuery().With<View>().With<Speed>().None<Dead>();
        }
        public void OnUpdate(ref World world, float deltaTime) {
            for (var i = 0; i < Query.Count; i++) {
                ref var entity = ref Query.GetEntity(i);
                ref var view = ref entity.Get<View>();
                ref var speed = ref entity.Get<Speed>();
                view.value.Value.transform.position += speed.value * deltaTime * Vector3.right;
            }
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
}

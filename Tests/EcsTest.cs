using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace Wargon.Nukecs.Tests {
    public unsafe class EcsTest : MonoBehaviour {
        private World world;
        private Systems systems;
        public GameObject sphere;
        void Awake() {
            world = World.Create(WorldConfig.Default16384);
            systems = new Systems(ref world);
            systems
                //.Add<MoveSystem>()
                //.Add<ViewSystem2>()
                //.Add<ViewSystem>()
                .Add<DBTestSystem>()
                ;
            //link.Convert(ref world);
            //Debug.Log(ComponentsMap.Index(typeof(View)));
            //var pool = GenericPool.Create(typeof(View), 256, Allocator.Persistent);
            // for (int i = 0; i < 1000; i++)
            // {
            //     var e = world.CreateEntity();
            //     e.Add(new Speed{value = 20f});
            //     var cube = Instantiate(sphere);
            //     //var cube = new GameObject($"e:{e.id}");
            //     cube.name = $"{cube.name}_{e.id}";
            //     //Destroy(cube.GetComponent<BoxCollider>());
            //     e.Add(new View {
            //         value = cube
            //     });
            //     e.Add(new Transform {
            //         position = RandomEx.Vector3(-10.0f,10.0f)
            //     });
            //     e.Add(new ViewPosition());
            //     // e.Add(new C1());
            //     // e.Add(new C2());
            // }

            var e1 = world.CreateEntity();
            ref var b1 = ref e1.AddBuffer<InventoryItem>();
            b1.Add(new InventoryItem() {
                name = new FixedString512Bytes("Шапка"),
                slot = 0
            });
            b1.Add(new InventoryItem() {
                name = new FixedString512Bytes("Мобила"),
                slot = 1
            });
        }

        private void Update() {
            systems.OnUpdate(Time.deltaTime);
            //systems.Run(Time.deltaTime);
        }
        private void OnDestroy() {
            world.Dispose();
            World.DisposeStatic();
        }
    }

    public struct DBTestSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<DynamicBuffer<InventoryItem>>();
        }

        public void OnUpdate(ref Entity entity, float deltaTime) {
            ref var b = ref entity.Get<DynamicBuffer<InventoryItem>>();

            foreach (var inventoryItem in b.list) {
                Debug.Log(inventoryItem.name.ToString());
            }
            Debug.Log(b.list.Length);
            b.list.Dispose();
            entity.Destroy();
        }
    }

    public struct ViewSystem : ISystem, ICreate {
        private Query _query;
        public void OnCreate(ref World world) {
            _query = world.CreateQuery().With<View>().With<Transform>();
        }

        public void OnUpdate(ref World world, float deltaTime) {
            for (var i = 0; i < _query.Count; i++) {
                ref var entity = ref _query.GetEntity(i);
                var view = entity.Read<View>();
                var transform = entity.Read<Transform>();
                view.value.Value.transform.position = transform.position;
                view.value.Value.transform.rotation = transform.rotation;
            }
        }
    }

    public struct ViewSystem2 : IJobParallelForTransform {
        public Query Query;
        public void Execute(int index, TransformAccess transform) {
            
        }
    }
    // [BurstCompile]
    // public struct ViewSystem2 : IEntityJobSystem {
    //     public SystemMode Mode => SystemMode.Parallel;
    //     public Query GetQuery(ref World world) {
    //         return world.CreateQuery().With<ViewPosition>().With<Transform>();
    //     }
    //     
    //     public void OnUpdate(ref Entity entity, float deltaTime) {
    //         ref var view = ref entity.Get<ViewPosition>();
    //         var transform = entity.Read<Transform>();
    //         view.Value = new Vector3(transform.position.x, transform.position.y, transform.position.z);
    //     }
    // } 
    [BurstCompile(CompileSynchronously = true)]
    public struct MoveSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<Transform>().With<Speed>();
        }

        private const float fixedDeltaTime = 1f / 60f;
        [BurstCompile(CompileSynchronously = true)]
        public void OnUpdate(ref Entity entity, float deltaTime) {
            ref var transform = ref entity.Get<Transform>();
            var speed = entity.Read<Speed>();
    
            float remainingTime = deltaTime;
            while (remainingTime > 0) {
                float stepTime = math.min(fixedDeltaTime, remainingTime);
                float3 newPosition = transform.position + speed.value * stepTime * math.right();
        
                if (newPosition.x > 140f) {
                    transform.position.x = 140f;
                    entity.Destroy();
                    break;
                }
                transform.position = newPosition;
                remainingTime -= stepTime;
            }
        }
    }
    
    // [BurstCompile]
    // public struct MoveSystem2 : IEntityJobSystem<Transform, Speed> {
    //     public SystemMode Mode => SystemMode.Parallel;
    //     public Query GetQuery(ref World world) {
    //         return world.CreateQuery().With<Transform>().With<Speed>();
    //     }
    //
    //     public void OnUpdate(ref Entity entity, ref Transform transform, ref Speed speed, float deltaTime) {
    //         transform.position += speed.value * deltaTime * math.right();
    //
    //         if (transform.position.x > 140f) {
    //             entity.Destroy();
    //         }
    //     }
    // }

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
    public struct ViewPosition : IComponent {
        public Vector3 Value;
    }
    [Serializable]
    public struct View : IComponent {
        public UnityObjectRef<GameObject> value;
    }
    [Serializable]
    public struct Speed : IComponent {
        public float value;
    }

    public struct InventoryItem {
        public FixedString512Bytes name;
        public int slot;
    }
    
    public static class RandomEx {
        public static float3 Float3(float min, float max) {
            return new float3(UnityEngine.Random.Range(min, max),UnityEngine.Random.Range(min, max),UnityEngine.Random.Range(min, max));
        }
        public static Vector3 Vector3(float min, float max) {
            return new Vector3(UnityEngine.Random.Range(min, max),UnityEngine.Random.Range(min, max),UnityEngine.Random.Range(min, max));
        }
    }
}

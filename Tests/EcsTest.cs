using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs.Tests {
    public class EcsTest : MonoBehaviour {
        private World world;
        private Systems systems;
        public GameObject sphere;
        public UnityEngine.Mesh mesh;
        public UnityEngine.Material material;
        public SpriteAnimationList animationData;
        void Awake() {
            //Application.targetFrameRate = 144;
            SpriteAnimationsStorage.Instance.Initialize(4);
            world = World.Create(WorldConfig.Default_1_000_000);
            systems = new Systems(ref world);
            systems
                //.Add<MoveSystem4>()
                
                .Add<UpdateCameraCullingSystem>()
                //.Add<CullingSystem>()
                .Add<CullSpritesSystem>()
                .Add<UnCullSpritesSystem>()
                .Add<UpdateChunkDataSystem>()
                .Add<UserInputSystem>()
                .Add<MoveSystem>()
                .Add<SpriteChangeAnimationSystem>()
                .Add<SpriteAnimationSystem>()
                
                .Add<SpriteRenderSystem>()
                
                //.Add<ViewSystem>()
                ;

            for (var i = 0; i < 1000; i++) {
                Entity e = animationData.Convert(ref world, RandomEx.Float3(-15,15));
                e.Add(new Input());
                e.Add(new Speed{value = 4f});
            }
        }

        private void Update() {
            InputService.Instance.Update();
            systems.OnUpdate(Time.deltaTime);
        }
        private void OnDestroy() {
            world.Dispose();
            World.DisposeStatic();
            SpriteAnimationsStorage.Instance.Dispose();
            SpriteArchetypesStorage.Singleton.Dispose();
        }

        private Entity CreatePlayer() {
            var e = world.CreateEntity();
            e.Add(new Player());
            e.AddBuffer<InventoryItem>();
            return e;
        }

    }

    public struct ViewSystem : ISystem, IOnCreate {
        private Query _query;
        public void OnCreate(ref World world) {
            _query = world.CreateQuery().With<View>().With<Transform>();
        }

        public void OnUpdate(ref World world, float deltaTime) {
            for (var i = 0; i < _query.Count; i++) {
                ref var entity = ref _query.GetEntity(i);
                var view = entity.Read<View>();
                var transform = entity.Read<Transform>();
                view.value.Value.transform.position = transform.Position;
                view.value.Value.transform.rotation = transform.Rotation;
            }
        }
    }
    
    [BurstCompile(CompileSynchronously = true)]
    public struct MoveSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<Transform>().With<Speed>().With<Input>();
        }

        public void OnUpdate(ref Entity entity, float deltaTime) {
            ref var transform = ref entity.Get<Transform>();
            var speed = entity.Read<Speed>();
            var input = entity.Read<Input>();

            transform.Position += new float3(input.h, input.v, 0) * speed.value * deltaTime;

        }
    }
    [BurstCompile(CompileSynchronously = true)]
    public struct MoveSystem4 : IQueryJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<Transform>().With<Speed>();
        }

        private const float fixedDeltaTime = 1f / 60f;
        [BurstCompile(CompileSynchronously = true)]
        public void OnUpdate(ref Query query, float deltaTime) {
            foreach (ref var entity in query) {
                ref var transform = ref entity.Get<Transform>();
                var speed = entity.Read<Speed>();
    
                float remainingTime = deltaTime;
                while (remainingTime > 0) {
                    float stepTime = math.min(fixedDeltaTime, remainingTime);
                    float3 newPosition = transform.Position + speed.value * stepTime * math.right();
        
                    if (newPosition.x > 100) {
                        transform.Position.x = 100;
                        entity.Destroy();
                        break;
                    }
                    transform.Position = newPosition;
                    remainingTime -= stepTime;
                }
            }
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
        public int slot;
        public Entity entity;
    }

    public struct Inventory : IComponent {
        public int totalSlots;
        public int lastEmptySlot;
    }

    public struct MeshReference : IComponent {
        public UnityObjectRef<UnityEngine.Mesh> value;
    }

    public struct MaterialReference : IComponent {
        public UnityObjectRef<UnityEngine.Material> value;
    }

    public struct Input : IComponent {
        public float h;
        public float v;
        public bool fire;
        public bool use;
    }
    
    
    [BurstCompile]
    public struct UserInputSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<Input>();
        }

        public void OnUpdate(ref Entity entity, float deltaTime) {
            ref var input = ref entity.Get<Input>();
            input.v = InputService.Instance.GetAxis(InputAxis.Vertical);
            input.h = InputService.Instance.GetAxis(InputAxis.Horizontal);
        }
    }

    public struct AddItemEvent : IComponent { public Entity entity; }
    
    [BurstCompile]
    public struct AddItemSystem : IEntityJobSystem, IOnCreate {
        public SystemMode Mode => SystemMode.Parallel;
        private Query _playerQuery;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<AddItemEvent>().With<DynamicBuffer<InventoryItem>>();
        }
        public void OnCreate(ref World world) {
            _playerQuery = world.CreateQuery().With<Player>();
        }
        public void OnUpdate(ref Entity entity, float deltaTime) {
            ref var addItemEvent = ref entity.Get<AddItemEvent>();

            ref var playerE = ref _playerQuery.GetEntity(0);
            ref var inventory = ref playerE.Get<Inventory>();
            ref var inventorySlots = ref playerE.GetBuffer<InventoryItem>();
            
            inventorySlots.Add(new InventoryItem {
                entity = addItemEvent.entity,
                slot = inventory.lastEmptySlot++
            });
        }
    }


    public static class RandomEx {
        public static float3 Float3(float min, float max) {
            return new float3(UnityEngine.Random.Range(min, max),UnityEngine.Random.Range(min, max),UnityEngine.Random.Range(min, max));
        }
        public static Vector3 Vector3(float min, float max) {
            return new Vector3(UnityEngine.Random.Range(min, max),UnityEngine.Random.Range(min, max),UnityEngine.Random.Range(min, max));
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ShaderProperties
    {
        public float GlowIntensity;
        public Color32 GlowColor;
    }


    public struct InputService
    {
        public static ref InputService Instance => ref Singleton<InputService>.Instance;
        private float Vertical;
        private float Horizontal;
        private byte Fire;
        private byte Intercat;
        public void Update()
        {
            Horizontal = UnityEngine.Input.GetAxisRaw(nameof(Horizontal));
            Vertical = UnityEngine.Input.GetAxisRaw(nameof(Vertical));
        }

        public float GetAxis(InputAxis key)
        {
            return key switch
            {
                InputAxis.Vertical => Vertical,
                _ => Horizontal
            };
        }
    }


    public enum InputAxis : short
    {
        Vertical,
        Horizontal
    }
}

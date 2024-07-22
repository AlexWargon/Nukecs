using System;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Wargon.Nukecs.Tests {
    public unsafe class EcsTest : MonoBehaviour {
        private World world;
        private Systems systems;
        public GameObject sphere;
        public UnityEngine.Mesh mesh;
        public UnityEngine.Material material;
        void Awake() {
            world = World.Create(WorldConfig.Default16384);
            systems = new Systems(ref world);
            systems
                .Add<MoveSystem4>()
                .Add<ViewSystem2>()
                //.Add<ViewSystem>()
                ;

            for (var i = 0; i < 1000; i++)
            {
                var e = world.CreateEntity();
                e.Add(new Transform {
                    position = RandomEx.Vector3(-10.0f,10.0f),
                    rotation = quaternion.identity
                });
                e.Add(new Speed{value = 20f});
                e.Add(new Mesh{value = mesh});
                e.Add(new Material{value = material});
                // e.Add(new C1());
                // e.Add(new C2());
            }

        }

        private void Update() {
            systems.OnUpdate(Time.deltaTime);
            //systems.Run(Time.deltaTime);
        }
        private void OnDestroy() {
            world.Dispose();
            World.DisposeStatic();
        }

        private Entity CreatePlayer() {
            var e = world.CreateEntity();
            e.Add(new Player());
            e.AddBuffer<InventoryItem>();
            
            return e;
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
    
    public struct ViewSystem2 : IQueryJobSystem, ICreate {
        public CommandBuffer cmd;
        private ComputeBuffer _instanceBuffer;
        public SystemMode Mode => SystemMode.Main;
        
        public void OnCreate(ref World world) {
            cmd = new CommandBuffer();
            cmd.name = "Custom Mesh Renderer";
            cmd.SetViewProjectionMatrices(Camera.main.worldToCameraMatrix, Camera.main.projectionMatrix);

            Camera.main.AddCommandBuffer(CameraEvent.AfterForwardOpaque,cmd);
        }
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<Material>().With<Mesh>().With<Transform>().With<SpriteRenderData>();
        }

        public void OnUpdate(ref Query query, float deltaTime) {
            if (_instanceBuffer == null || _instanceBuffer.count < query.Count)
            {
                _instanceBuffer?.Release();
                _instanceBuffer = new ComputeBuffer(query.Count, 32); // 3 (position) + 4 (uv) + 1 (atlasIndex) = 8 floats * 4 bytes
            }
            
            foreach (ref var entity in query) {
                ref var transform = ref entity.Get<Transform>();
                ref var material = ref entity.Get<Material>();
                ref var mesh = ref entity.Get<Mesh>();
                ref var anim = ref entity.Get<SpriteAnimation>();
                // cmd.SetGlobalVector("_SpriteUV", new Vector4(anim.currentUV.x, anim.currentUV.y, anim.frameSize.x, anim.frameSize.y));
                // cmd.SetGlobalTexture("_MainTex", atlases[anim.atlasIndex]);
                // cmd.DrawMesh(mesh.value, 
                //     Matrix4x4.TRS(transform.position, new Quaternion(
                //             transform.rotation.value.x, 
                //             transform.rotation.value.y, 
                //             transform.rotation.value.z, 
                //             transform.rotation.value.w), 
                //         Vector3.one), material.value);
            }
            Graphics.ExecuteCommandBuffer(cmd);
            
        }

    }

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
        
                if (newPosition.x > 100f) {
                    transform.position.x = 100;
                    entity.Destroy();
                    break;
                }
                transform.position = newPosition;
                remainingTime -= stepTime;
            }
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
                    float3 newPosition = transform.position + speed.value * stepTime * math.right();
        
                    if (newPosition.x > 100) {
                        transform.position.x = 100;
                        entity.Destroy();
                        break;
                    }
                    transform.position = newPosition;
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

    public struct Mesh : IComponent {
        public UnityObjectRef<UnityEngine.Mesh> value;
    }

    public struct Material : IComponent {
        public UnityObjectRef<UnityEngine.Material> value;
    }

    public struct Input : IComponent {
        public float h;
        public float v;
        public bool fire;
        public bool use;
    }
    public struct SpriteAnimation : IComponent
    {
        public int atlasIndex;    // Индекс атласа в массиве атласов
        public int frameCount;    // Общее количество кадров в анимации
        public int currentFrame;  // Текущий кадр анимации
        public float frameTime;   // Время отображения одного кадра
        public float elapsedTime; // Прошедшее время с начала текущего кадра
        public float2 frameSize;  // Размер одного кадра в атласе (в UV координатах)
        public float2 startUV;    // Начальные UV координаты первого кадра в атласе
        public int framesPerRow;  // Количество кадров в одном ряду атласа
    }
    
    public struct SpriteRenderData : IComponent
    {
        public float3 position;
        public float4 uv;
        public int atlasIndex;
    }
    public struct AddItemEvent : IComponent { public Entity entity; }
    
    [BurstCompile]
    public struct AddItemSystem : IEntityJobSystem, ICreate {
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
    public struct SpriteAnimationSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<SpriteAnimation>().With<SpriteRenderData>().With<Transform>();
        }

        public void OnUpdate(ref Entity entity, float deltaTime) {
            ref var anim = ref entity.Get<SpriteAnimation>();
            ref var renderData = ref entity.Get<SpriteRenderData>();
            var position = entity.Read<Transform>().position;
            anim.elapsedTime += deltaTime;
            if (anim.elapsedTime >= anim.frameTime)
            {
                anim.currentFrame = (anim.currentFrame + 1) % anim.frameCount;
                anim.elapsedTime -= anim.frameTime;
            }

            var row = anim.currentFrame / anim.framesPerRow;
            var col = anim.currentFrame % anim.framesPerRow;
            var currentUV = new float2(
                anim.startUV.x + col * anim.frameSize.x,
                anim.startUV.y + row * anim.frameSize.y
            );

            renderData.position = position;
            renderData.uv = new float4(currentUV.x, currentUV.y, anim.frameSize.x, anim.frameSize.y);
            renderData.atlasIndex = anim.atlasIndex;
        }
    }

    public class SpriteAnimationData : ScriptableObject {
        public Texture2D atlas;
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

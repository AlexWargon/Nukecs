using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs.Tests {
    public class EcsTest : MonoBehaviour {
        private World world;
        private Systems systems;
        public GameObject sphere;
        public Mesh mesh;
        public Material material;
        public SpriteAnimationList animationData;
        public SpriteData bulletSprite;
        void Awake() {
            //Application.targetFrameRate = 144;
            SpriteAnimationsStorage.Instance.Initialize(4);
            world = World.Create(WorldConfig.Default163840);
            systems = new Systems(ref world)

                //.Add<MoveSystem4>()
                .Add<SpriteRenderSystem>()
                .Add<UpdateCameraCullingSystem>()
                //.Add<CullingSystem>()
                .Add<CullSpritesSystem>()
                .Add<UnCullSpritesSystem>()
                .Add<UpdateChunkDataSystem>()
                .Add<UserInputSystem>()
                .Add<MoveSystem>()
                .Add<MoveBulletSystem>()
                .Add<SpriteChangeAnimationSystem>()
                .Add<SpriteAnimationSystem>()
                .Add<RotateSpriteSystem>()
                //.Add<ViewSystem>()
                ;
            
            CreatePlayerPrefab();
            CreateBulletPrefab();
            world.Update();
            for (var i = 0; i < 2; i++) {
                // var e = animationData.Convert(ref world, RandomEx.Float3(-50,50));
                // e.Add(new Input());
                // e.Add(new Speed{value = 4f});

                var e = playerPrefab.Copy();
                e.Get<Transform>().Position = RandomEx.Float3(-5, 5);
                e.Get<SpriteChunkReference>().ChunkRef.Add(in e);
                e.Remove<IsPrefab>();
                //e.Remove<IsPrefab>();
            }
            Debug.Log(UnsafeUtility.SizeOf<IsAlive>());
            Debug.Log(UnsafeUtility.SizeOf<IsPrefab>());
            Debug.Log(UnsafeUtility.SizeOf<DestroyEntity>());
            Debug.Log(UnsafeUtility.SizeOf<Transform>());
            //var s = new Query<(Transform, SpriteAnimation)>();

        }

        private Entity playerPrefab;
        private Entity bulletPrefab;
        private void CreatePlayerPrefab() {
            playerPrefab = animationData.Convert(ref world, RandomEx.Float3(-5,5));
            playerPrefab.Add(new Input());
            playerPrefab.Add(new Speed{value = 4f});
            playerPrefab.Add(new IsPrefab());
        }
        private void CreateBulletPrefab() {
            bulletPrefab = world.CreateEntity();
            bulletSprite.AddToEntity(ref world, ref bulletPrefab);
            bulletPrefab.Add(new IsPrefab());
            bulletPrefab.Add(new Bullet());
            bulletPrefab.Add(new Transform(RandomEx.Float3(-5,5)));
            bulletPrefab.Add(new Speed{value = 22f});

        }
        
        private void Update() {
            if (UnityEngine.Input.GetKey(KeyCode.Space)) {
                for (int i = 0; i < 10; i++) {
                    var e = playerPrefab.Copy();
                    e.Get<Transform>().Position = RandomEx.Float3(-5, 5);
                    e.Get<SpriteChunkReference>().ChunkRef.Add(in e);
                    e.Remove<IsPrefab>();
                }
            }
            if (UnityEngine.Input.GetKey(KeyCode.R)) {
                for (int i = 0; i < 10; i++) {
                    var e = bulletPrefab.Copy();
                    e.Get<Transform>().Position = RandomEx.Float3(-5, 5);
                    e.Get<SpriteChunkReference>().ChunkRef.Add(in e);
                    e.Remove<IsPrefab>();
                }
            }
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
    public struct Bullet : IComponent {}
    [BurstCompile]
    public struct MoveBulletSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<Bullet>().With<Transform>().With<Speed>();
        }

        public void OnUpdate(ref Entity entity, float deltaTime) {
            ref var t = ref entity.Get<Transform>();
            ref readonly var s = ref entity.Read<Speed>();
            t.Position += math.mul(t.Rotation, math.right()) * s.value * deltaTime;
        }
    }
    [BurstCompile]
    public struct RotateSpriteSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<Transform>();
        }
        public void OnUpdate(ref Entity entity, float deltaTime) {
            ref var t = ref entity.Get<Transform>();
            var deltaRotation = quaternion.Euler(0f, 0f, 2f * deltaTime);
            t.Rotation = math.mul(t.Rotation, deltaRotation);
        }
    }
    public struct TSTSystem : IQueryJobSystem<(Transform, Speed)> {
        public void OnUpdate(Query<(Transform, Speed)> query) {
            for (var i = 0; i < query.Count; i++) {
                var (t, s) = query.Get<Transform, Speed>(i);
                t.Value.Position += math.right() * s.Value.value * 0.1f;
            }
        }

        public static void TestSystem(Query<(Transform, Speed)> query) {
            for (var i = 0; i < query.Count; i++) {
                var (t, s) = query.Get<Transform, Speed>(i);
                t.Value.Position += math.right() * s.Value.value * 0.1f;
            }
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

    [BurstCompile]
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
        public float2 MousePos;
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
    
    public struct AddRemove : IComponent{}
    
    public struct AddComponentSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().None<AddRemove>().With<Transform>();
        }

        public void OnUpdate(ref Entity entity, float deltaTime) {
            entity.Add(new AddRemove());
        }
    }

    public struct RemoveComponentSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<AddRemove>().With<Transform>();
        }

        public void OnUpdate(ref Entity entity, float deltaTime) {
            entity.Remove<AddRemove>();
        }
    }
}

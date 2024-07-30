using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Wargon.Nukecs.Tests {
    public class EcsTest : MonoBehaviour {
        [SerializeField] private int fps = 144;
        private World world;
        private Systems systems;
        public GameObject sphere;
        public Mesh mesh;
        public Material material;
        public SpriteAnimationList animationData;
        public SpriteData bulletSprite;
        public int reserved;
        public int entities;
        void Awake() {
            Application.targetFrameRate = fps;
            SpriteAnimationsStorage.Instance.Initialize(4);
            world = World.Create(WorldConfig.Default_1_000_000);
            systems = new Systems(ref world)

                //.Add<MoveSystem4>()
                .Add<SpriteRenderSystem>()
                .Add<UpdateCameraCullingSystem>()
                //.Add<CullingSystem>()
                .Add<LifetimeSystem>()
                //.Add<ClearRenderOnEntityDestroySystem>()
                .Add<CullSpritesSystem>()
                .Add<UnCullSpritesSystem>()
                
                .Add<UpdateChunkDataSystem>()
                .Add<UserInputSystem>()
                .Add<MoveSystem>()
                .Add<MoveBulletSystem>()
                .Add<SpriteChangeAnimationSystem>()
                .Add<SpriteAnimationSystem>()
                //.Add<RotateSpriteSystem>()
                .Add<ShootSystem>()
                
                //.Add<ViewSystem>()
                ;
            CreateBulletPrefab();
            CreatePlayerPrefab();
            world.Update();
            for (var i = 0; i < 1; i++) {
                // var e = animationData.Convert(ref world, RandomEx.Float3(-50,50));
                // e.Add(new Input());
                // e.Add(new Speed{value = 4f});
                var e = world.SpawnPrefab(in playerPrefab);
                e.Get<Transform>().Position = RandomEx.Float3(-5, 5);
                e.Get<SpriteChunkReference>().ChunkRef.Add(in e);
                //e.Remove<IsPrefab>();
            }
            //var s = new Query<(Transform, SpriteAnimation)>();
        }
        private Entity playerPrefab;
        private Entity bulletPrefab;
        private void CreatePlayerPrefab() {
            playerPrefab = animationData.Convert(ref world, RandomEx.Float3(-5,5));
            playerPrefab.Add(new Input());
            playerPrefab.Add(new Speed{value = 4f});
            playerPrefab.Add(new IsPrefab());
            playerPrefab.Add(new Gun{BulletsAmount = 300, Cooldown = 0.1f, Spread = 10f});
            playerPrefab.Add(new BulletPrefab{Value = bulletPrefab});
        }
        private void CreateBulletPrefab() {
            bulletPrefab = world.CreateEntity();
            bulletSprite.AddToEntity(ref world, ref bulletPrefab);
            bulletPrefab.Add(new IsPrefab());
            bulletPrefab.Add(new Bullet());
            bulletPrefab.Add(new Transform(RandomEx.Float3(-5,5)));
            bulletPrefab.Add(new Speed{value = 42f});
            bulletPrefab.Add(new Lifetime{value = 1f});

        }
        
        private void Update() {
            unsafe {
                reserved = world.Unsafe->reservedEntities.Length;
                entities = world.Unsafe->entitiesAmount;
            }
            
            if (UnityEngine.Input.GetKey(KeyCode.Space)) {
                for (int i = 0; i < 10; i++) {
                    var e = world.SpawnPrefab(in playerPrefab);
                    e.Get<Transform>().Position = RandomEx.Float3(-5, 5);
                    e.Get<SpriteChunkReference>().ChunkRef.Add(in e, e.Get<Transform>());
                }
            }
            if (UnityEngine.Input.GetKey(KeyCode.R)) {
                for (int i = 0; i < 10; i++) {
                    var e = world.SpawnPrefab(in bulletPrefab);
                    ref var t = ref e.Get<Transform>();
                    t.Position = RandomEx.Float3(-5, 5);
                    e.Get<SpriteChunkReference>().ChunkRef.Add(in e, in t);
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
            input.fire = InputService.Instance.Fire;
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
        public bool Fire;
        public byte Intercat;
        public float3 MousePos;
        public void Update()
        {
            Horizontal = UnityEngine.Input.GetAxisRaw(nameof(Horizontal));
            Vertical = UnityEngine.Input.GetAxisRaw(nameof(Vertical));
            var mousePosV = Camera.main.ScreenToWorldPoint(UnityEngine.Input.mousePosition);
            MousePos = new float3(mousePosV.x, mousePosV.y, 0);
            Fire = UnityEngine.Input.GetButton("Fire1");
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
    public struct Bullet : IComponent {}

    public struct BulletPrefab : IComponent {
        public Entity Value;
    }
    
    public struct Lifetime : IComponent {
        public float value;
    }

    public struct Gun : IComponent {
        public int BulletsAmount;
        public float Spread;
        public float Cooldown;
        public float CooldownCounter;
    }
    [BurstCompile]
    public struct LifetimeSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<Lifetime>().With<Culled>(); // only that entities that not rendering
        }
        public void OnUpdate(ref Entity entity, float deltaTime) {
            ref var lifetime = ref entity.Get<Lifetime>();
            lifetime.value -= deltaTime;
            if (lifetime.value <= 0f) {
                entity.DestroyLate();
            }
        }
    }
    [BurstCompile]
    public struct ShootSystem : IEntityJobSystem {
        private World World;
        public SystemMode Mode => SystemMode.Main;
        public Query GetQuery(ref World world) {
            World = world;
            return world.CreateQuery().With<Gun>().With<Input>().With<Transform>().With<BulletPrefab>();
        }

        public void OnUpdate(ref Entity entity, float deltaTime) {
            ref readonly var input = ref entity.Read<Input>();
            ref var gun = ref entity.Get<Gun>();
            gun.CooldownCounter -= deltaTime;
            if (input.fire && gun.CooldownCounter <= 0) {
                ref readonly var t = ref entity.Read<Transform>();
                ref readonly var prefab = ref entity.Read<BulletPrefab>();
                //var (t, prefab) = entity.Get<Transform, BulletPrefab>();
                var mpos = InputService.Instance.MousePos;   
                var dif = mpos - t.Position;
                var rotZ = math.atan2(dif.y, dif.x) * Mathf.Rad2Deg;

                for (int i = 0; i < gun.BulletsAmount; i++) {
                    var bullet =  World.SpawnPrefab(in prefab.Value);
                    ref var bt = ref bullet.Get<Transform>();                
                    var rot = Quaternion.AngleAxis(rotZ + Random.Range(-gun.Spread,gun.Spread), Vector3.forward);
                    bt.Rotation = rot;
                    bt.Position = t.Position;
                    bullet.Get<SpriteChunkReference>().ChunkRef.Add(in bullet, in bt);
                    gun.CooldownCounter = gun.Cooldown;                    
                }
            }
        }
    }
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

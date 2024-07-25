using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs.Tests {
    public unsafe class EcsTest : MonoBehaviour {
        private World world;
        private Systems systems;
        public GameObject sphere;
        public UnityEngine.Mesh mesh;
        public UnityEngine.Material material;
        public SpriteAnimationList animationData;
        void Awake() {

            SpriteAnimationsStorage.Instance.Initialize(4);
            world = World.Create(WorldConfig.Default16384);
            systems = new Systems(ref world);
            systems
                //.Add<MoveSystem4>()
                .Add<UserInputSystem>()
                .Add<MoveSystem>()
                .Add<SpriteChangeAnimationSystem>()
                .Add<SpriteAnimationSystem>()
                .Add<UpdateChunkDataSystem>()
                //.Add<SpriteRenderSystem>()
                //.Add<ViewSystem>()
                ;

            // for (var i = 0; i < 3; i++)
            // {
            //     var e = world.CreateEntity();
            //     e.Add(new Transform {
            //         position = RandomEx.Vector3(-10.0f,10.0f),
            //         rotation = quaternion.identity
            //     });
            //     e.Add(new Speed{value = 20f});
            //     e.Add(new Mesh{value = mesh});
            //     e.Add(new Material{value = material});
            //     // e.Add(new C1());
            //     // e.Add(new C2());
            // }
            for (var i = 0; i < 12; i++)
            {
                var e = animationData.Convert(ref world, RandomEx.Float3(-2,2));
                e.Add(new Input());
                e.Add(new Speed{value = 4f});
            }
        }

        private void Update() {
            InputService.Instance.Update();
            
            systems.OnUpdate(Time.deltaTime);
            SpriteArchetypesStorage.Singleton.OnUpdate();
            //Sprites.SpriteRender.Singleton.Clear();
            //systems.Run(Time.deltaTime);
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
                view.value.Value.transform.position = transform.position;
                view.value.Value.transform.rotation = transform.rotation;
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

            transform.position += new float3(input.h, input.v, 0) * speed.value * deltaTime;

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
    public struct SpriteRenderData : IComponent {
        public int SpriteIndex;
        public float3 Position;
        public quaternion Rotation;
        public float3 Scale;
        public Color32 Color;
        public float4 SpriteTiling;
        [MarshalAs(UnmanagedType.U1)] public bool FlipX;
        [MarshalAs(UnmanagedType.U1)] public bool FlipY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SpriteAnimation : IComponent
    {
        public const int MaxFrames = 16;
        public int FrameCount;
        public float FrameRate;
        public float CurrentTime;
        public int row;
        public int col;
        public int AnimationID;
    }
    public struct RenderMatrix : IComponent {
        public Matrix4x4 Matrix;
    }
    public struct IndexInChunk : IComponent {
        public int value;
        public unsafe SpriteChunk* chunk;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct ShaderProperties
    {
        public float GlowIntensity;
        public Color32 GlowColor;
    }
    [BurstCompile]
    public struct SpriteChangeAnimationSystem : IEntityJobSystem, IOnCreate {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<SpriteAnimation>().With<Input>();
        }
        
        public void OnUpdate(ref Entity entity, float deltaTime) {
            var input = entity.Read<Input>();
            ref var anim = ref entity.Get<SpriteAnimation>();
            anim.AnimationID = input.h is > 0f or < 0f ? Run : Idle;
        }

        public void OnCreate(ref World world) {
            Run = Animator.StringToHash(nameof(Run));
            Idle = Animator.StringToHash(nameof(Idle));
        }

        private int Run;
        private int Idle;
    } 
    [BurstCompile]
    public struct SpriteAnimationSystem : IEntityJobSystem
    {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<SpriteAnimation>().With<SpriteRenderData>().With<Transform>().With<Input>();
        }

        public unsafe void OnUpdate(ref Entity entity, float deltaTime) {
            ref var animation = ref entity.Get<SpriteAnimation>();
            ref var renderData = ref entity.Get<SpriteRenderData>();
            ref var transform = ref entity.Get<Transform>();
            var input = entity.Read<Input>();
            animation.CurrentTime += deltaTime;
            float frameDuration = 1f / animation.FrameRate;
            int frameIndex = (int)(animation.CurrentTime / frameDuration) % animation.FrameCount;
            renderData.SpriteIndex = frameIndex;
            renderData.Position = transform.position;
            renderData.Rotation = transform.rotation;
            renderData.Scale = new float3(1, 1, 1);
            renderData.FlipX = input.h < 0f;
            //renderData.SpriteTiling = CalculateSpriteTiling(renderData.SpriteIndex, animation.row, animation.col);
            renderData.SpriteTiling = GetSpriteTiling(renderData.SpriteIndex, animation.AnimationID);
            transform.position.z = transform.position.y*.1f;
        }
        public static float4 CalculateSpriteTiling(int spriteIndex, int spritesPerRow, int spritesPerColumn)
        {
            var row = spriteIndex / spritesPerRow;
            var col = spriteIndex % spritesPerRow;
            var tileWidth = 1f / spritesPerRow;
            var tileHeight = 1f / spritesPerColumn;
    
            var x = col * tileWidth;
            var y = (spritesPerColumn - 1 - row) * tileHeight;
            var r = new float4(x, y, tileWidth, tileHeight);
            Debug.Log(r);
            return r;
        }

        private static float4 GetSpriteTiling(int spriteIndex, int animationID)
        {
            var r = SpriteAnimationsStorage.Instance.GetFrames(animationID).List.ElementAt(spriteIndex);
            return r;
        }
    }

    public struct SpriteRenderSystem : IQueryJobSystem, IOnCreate
    {
        private World _world;
        public SystemMode Mode => SystemMode.Main;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<SpriteRenderData>().With<RenderMatrix>();
        }
        public void OnCreate(ref World world)
        {
            _world = world;
        }
        public void OnUpdate(ref Query query, float deltaTime) {
            SpriteArchetypesStorage.Singleton.OnUpdate();
            //SpriteRendering.Singleton.Render(ref query, ref _world);
        }
    }
    public class SpriteRendering : SingletonBase<SpriteRendering> {
        
        private Texture2D atlasTexture;
        private Material spriteMaterial;
        private Mesh quadMesh;
        private static readonly int color = Shader.PropertyToID("_Color");
        private static readonly int texCoord = Shader.PropertyToID("_TexCoord");
        private static readonly int flip = Shader.PropertyToID("_Flip");

        public void Initialize(Texture2D texture2D) {
            atlasTexture = texture2D;
            spriteMaterial = new Material(Shader.Find("Custom/SpriteShaderCompatible"));
            spriteMaterial.mainTexture = atlasTexture;
            quadMesh = CreateQuadMesh();
        }
        private Mesh CreateQuadMesh()
        {
            var mesh = new Mesh {
                vertices = new Vector3[] {
                    new (-0.5f, -0.5f, 0),
                    new (0.5f, -0.5f, 0),
                    new (0.5f, 0.5f, 0),
                    new (-0.5f, 0.5f, 0)
                },
                uv = new Vector2[] {
                    new (0, 0),
                    new (1, 0),
                    new (1, 1),
                    new (0, 1)
                },
                triangles = new [] { 0, 1, 2, 0, 2, 3 }
            };
            return mesh;
        }

        public void Render(ref Query query, ref World world)
        {
            ref var dataPool = ref world.GetPool<SpriteRenderData>();
            ref var matrixPool = ref world.GetPool<RenderMatrix>();
            var props = new MaterialPropertyBlock();

            for (int i = 0; i < query.Count; i++) {
                var e = query.GetEntity(i);
                var data = dataPool.GetRef<SpriteRenderData>(e.id);
                var matrix = matrixPool.GetRef<RenderMatrix>(e.id);

                props.Clear();
                props.SetColor(color, data.Color);
                props.SetVector(texCoord, data.SpriteTiling);
                //props.SetVector(flip, matrix.Vector);
                Graphics.DrawMesh(quadMesh, matrix.Matrix, spriteMaterial, 0, null, 0, props);
            }
        }
    }

    [BurstCompile]
    public struct UpdateChunkDataSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<RenderMatrix>().With<SpriteRenderData>().With<IndexInChunk>();
        }

        public void OnUpdate(ref Entity entity, float deltaTime) {
            var data = entity.Read<SpriteRenderData>();
            ref var matrix = ref entity.Get<RenderMatrix>();
            ref var chunkIndex = ref entity.Get<IndexInChunk>();
            
            GetMatrix(in data, ref matrix);

            unsafe {
                chunkIndex.chunk->UpdateData(chunkIndex.value, data, matrix);
            }
            
        }
        [BurstCompile]
        private static void GetMatrix(in SpriteRenderData data, ref RenderMatrix renderMatrix) {
            var scale = new Vector3(
                data.Scale.x * (data.FlipX ? -1 : 1),
                data.Scale.y * (data.FlipY ? -1 : 1),
                data.Scale.z
            );

            // Создаем матрицу трансформации
            var scaleMatrix = Matrix4x4.Scale(scale);
            var rotationMatrix = Matrix4x4.Rotate(data.Rotation);
            var positionMatrix = Matrix4x4.Translate(data.Position);

            // Комбинируем матрицы
            renderMatrix.Matrix = positionMatrix * rotationMatrix * scaleMatrix;
        }
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

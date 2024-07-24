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
        public SpriteAnimationData animationData;
        void Awake() {

            world = World.Create(WorldConfig.Default16384);
            systems = new Systems(ref world);
            systems
                //.Add<MoveSystem4>()
                .Add<UserInputSystem>()
                .Add<MoveSystem>()
                .Add<SpriteAnimationSystem>()
                .Add<GetRenderMatrixSystem>()
                .Add<SpriteRenderSystem>()
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
            for (var i = 0; i < 1000; i++)
            {
                var e = animationData.CreateAnimatedSpriteEntity(ref world, RandomEx.Float3(-5,5));
                e.Add(new Input());
                e.Add(new Speed(){value = 4f});
            }
        }

        private void Update() {
            
            systems.OnUpdate(Time.deltaTime);
            Sprites.SpriteRender.Singleton.Clear();
            //systems.Run(Time.deltaTime);
        }
        private void OnDestroy() {
            world.Dispose();
            World.DisposeStatic();
                Sprites.SpriteRender.Singleton.OnDestroy();
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

    public struct UserInputSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Main;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<Input>();
        }

        public void OnUpdate(ref Entity entity, float deltaTime) {
            ref var input = ref entity.Get<Input>();
            input.v = UnityEngine.Input.GetAxis("Vertical");
            input.h = UnityEngine.Input.GetAxis("Horizontal");
        }
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
        public int Layer;
        public int SpriteIndex;
        public float3 Position;
        public quaternion Rotation;
        public float3 Scale;
        public Color32 Color;
        public float4 SpriteTiling;
        [MarshalAs(UnmanagedType.U1)]public bool FlipX;
        [MarshalAs(UnmanagedType.U1)]public bool FlipY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct SpriteAnimation : IComponent
    {
        public const int MaxFrames = 16;
        public fixed int SpriteIndices[MaxFrames];
        public int FrameCount;
        public float FrameRate;
        public float CurrentTime;
        public int StartIndex;
        public int row;
        public int col;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ShaderProperties
    {
        public float GlowIntensity;
        public Color32 GlowColor;
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
            renderData.SpriteIndex = animation.SpriteIndices[frameIndex];
            renderData.Position = transform.position;
            renderData.Rotation = transform.rotation;
            renderData.Scale = new float3(1, 1, 1);
            renderData.FlipX = input.h < 0f;
            renderData.SpriteTiling = CalculateSpriteTiling(renderData.SpriteIndex, animation.row, animation.col);
            transform.position.z = transform.position.y*.1f;
        }
        public static float4 CalculateSpriteTiling(int spriteIndex, int spritesPerRow, int spritesPerColumn)
        {
            int row = spriteIndex / spritesPerRow;
            int col = spriteIndex % spritesPerRow;
            float tileWidth = 1f / spritesPerRow;
            float tileHeight = 1f / spritesPerColumn;
    
            float x = col * tileWidth;
            float y = (spritesPerColumn - 1 - row) * tileHeight;
    
            return new float4(x, y, tileWidth, tileHeight);
        }
    }

    public struct SpriteRenderSystem : IQueryJobSystem {
        public SystemMode Mode => SystemMode.Main;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<SpriteRenderData>().With<RenderMatrix>();
        }

        public void OnUpdate(ref Query query, float deltaTime) {
            SpriteRender.Singleton.Render(ref query);
        }
    }
    public unsafe class SpriteRender : SingletonBase<SpriteRender> {
        
        private Texture2D atlasTexture;
        private Material spriteMaterial;
        private Mesh quadMesh;
        private static readonly int color = Shader.PropertyToID("_Color");
        private static readonly int texCoord = Shader.PropertyToID("_TexCoord");
        private static readonly int flip = Shader.PropertyToID("_Flip");


        public void Initialize(Texture2D texture2D) {
            atlasTexture = texture2D; // Загрузите ваш атлас
            spriteMaterial = new Material(Shader.Find("Custom/SpriteShaderCompatible"));
            spriteMaterial.mainTexture = atlasTexture;
            quadMesh = CreateQuadMesh();
        }
        private Mesh CreateQuadMesh()
        {
            Mesh mesh = new Mesh();
            mesh.vertices = new Vector3[] {
                new (-0.5f, -0.5f, 0),
                new (0.5f, -0.5f, 0),
                new (0.5f, 0.5f, 0),
                new (-0.5f, 0.5f, 0)
            };
            mesh.uv = new Vector2[] {
                new (0, 0),
                new (1, 0),
                new (1, 1),
                new (0, 1)
            };
            mesh.triangles = new [] { 0, 1, 2, 0, 2, 3 };
            return mesh;
        }

        public void Render(ref Query query)
        {
            var props = new MaterialPropertyBlock();

            for (int i = 0; i < query.Count; i++) {
                var e = query.GetEntity(i);
                var data = e.Read<SpriteRenderData>();
                var matrix = e.Read<RenderMatrix>();
                // Изменим логику установки _Flip
                //GetMatrix(in data, out var finalTransform, out var vector);

                props.Clear();
                props.SetColor(color, data.Color);
                props.SetVector(texCoord, data.SpriteTiling);
                props.SetVector(flip, matrix.vector);
                Graphics.DrawMesh(quadMesh, matrix.matrix, spriteMaterial, 0, null, 0, props);

                //Debug.DrawLine(data.Transform.c3.xyz, data.Transform.c3.xyz + Vector3.up, Color.red, 0.1f);
            }
        }
        
        [BurstCompile]
        private static void GetMatrix(in SpriteRenderData data, out Matrix4x4 finalTransform, out Vector4 vector4) {
            var flipX = data.FlipX ? -1 : 1;
            var flipY = data.FlipY ? -1 : 1;
            // Создаем матрицу трансформации
            var scaleMatrix = Matrix4x4.Scale(new Vector3(
                data.Scale.x * math.abs(flipX),
                data.Scale.y * math.abs(flipY),
                data.Scale.z
            ));

            var rotationMatrix = Matrix4x4.Rotate(data.Rotation);
            var positionMatrix = Matrix4x4.Translate(data.Position);
            finalTransform = positionMatrix * rotationMatrix * scaleMatrix;
            vector4 =  new Vector4(flipX, flipY, 1, 1);
        }
    }

    public struct RenderMatrix : IComponent {
        public Matrix4x4 matrix;
        public Vector4 vector;
    }

    [BurstCompile]
    public struct GetRenderMatrixSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<RenderMatrix>().With<SpriteRenderData>();
        }

        public void OnUpdate(ref Entity entity, float deltaTime) {
            var data = entity.Read<SpriteRenderData>();
            ref var matrix = ref entity.Get<RenderMatrix>();
            
            GetMatrix(in data, ref matrix);

        }
        [BurstCompile]
        private static void GetMatrix(in SpriteRenderData data, ref RenderMatrix renderMatrix) {
            var flipX = data.FlipX ? -1 : 1;
            var flipY = data.FlipY ? -1 : 1;
            // Создаем матрицу трансформации
            var scaleMatrix = Matrix4x4.Scale(new Vector3(
                data.Scale.x * math.abs(flipX),
                data.Scale.y * math.abs(flipY),
                data.Scale.z
            ));
            var rotationMatrix = Matrix4x4.Rotate(data.Rotation);
            var positionMatrix = Matrix4x4.Translate(data.Position);
            renderMatrix.matrix = positionMatrix * rotationMatrix * scaleMatrix;
            renderMatrix.vector = new Vector4(flipX, flipY, 1, 1);
        }
    }

    public struct SpriteChunk {
        internal unsafe SpriteRenderData* renderDataChunk;
        internal unsafe RenderMatrix* matrixChunk;
        internal int count;
        internal int capacity;
        internal int lastRemoved;

        public static unsafe SpriteChunk* Craete(int size) {
            var ptr = (SpriteChunk*)UnsafeUtility.Malloc(sizeof(SpriteChunk), UnsafeUtility.AlignOf<SpriteChunk>(),
                Allocator.Persistent);
            *ptr = new SpriteChunk() {
                renderDataChunk = UnsafeHelp.Malloc<SpriteRenderData>(size, Allocator.Persistent),
                matrixChunk = UnsafeHelp.Malloc<RenderMatrix>(size, Allocator.Persistent),
                count = 0,
                capacity = size,
                lastRemoved = 0
            };
            return ptr;
        }
        public unsafe int Add(SpriteRenderData data, RenderMatrix matrix) {
            var index = count;
            renderDataChunk[count] = data;
            matrixChunk[count] = matrix;
            count++;
            return index;
        }

        public unsafe void Remove(int index) {
            var last = count - 1;
            var lastData = renderDataChunk[last];
            var lastMatrix = matrixChunk[last];
            
            renderDataChunk[index] = lastData;
            matrixChunk[index] = lastMatrix;
            count--;
        }
        public unsafe NativeArray<SpriteRenderData> RenderDataArray() {
            return NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<SpriteRenderData>(renderDataChunk, count, Allocator.None);
        }
        public unsafe NativeArray<SpriteRenderData> MatrixArray() {
            return NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<SpriteRenderData>(matrixChunk, count, Allocator.None);
        }
    }

    public struct RenderChunkIndex : IComponent {
        public int value;
    }
}

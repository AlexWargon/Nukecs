using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs.Tests {
    public static class ShaderNames {
        public const string Sprites = "Custom/SpriteShaderInstanced";
        public const string SpritesWithShadow = "Custom/SpriteShaderInstancedWithShadow";
    }
    public class SpriteArchetypesStorage : SingletonBase<SpriteArchetypesStorage> {
        internal SpriteArchetype[] archetypes = new SpriteArchetype[3];
        internal int count;
        
        public void OnUpdate(ref World world) {
            for (int i = 0; i < count; i++) {
                archetypes[i].OnUpdate(ref world);
            }
        }
        public unsafe ref SpriteArchetype Add(Texture2D atlas, ref World world) {
            Resize();
            var instanceID = atlas.GetInstanceID();
            var h = Has(instanceID);
            if (h.has) {
                return ref archetypes[h.index];
            };
            var material = new Material(Shader.Find(ShaderNames.Sprites)) {
                mainTexture = atlas
            };
            var arch = new SpriteArchetype {
                Material = material,
                mesh = CreateQuadMesh(),
                instanceID = instanceID,
                entities = new NativeHashSet<Entity>(world.Unsafe->config.StartEntitiesAmount, Allocator.Persistent),
                Chunk = SpriteChunk.Create(world.Unsafe->config.StartEntitiesAmount),
                indexes = new NativeHashMap<int, int>(world.Unsafe->config.StartEntitiesAmount, Allocator.Persistent)
            };
            archetypes[count] = arch;
            count++;
            return ref archetypes[count-1];
        }

        private (int index, bool has) Has(int id) {
            for (var i = count; i >= 0; i--) {
                if (archetypes[i].instanceID == id) return (i, true);
            }

            return (0, false);
        }
        private void Resize() {
            if (count >= archetypes.Length) {
                Array.Resize(ref archetypes, archetypes.Length*2);
            }
        }
        public void Dispose() {
            for (var i = 0; i < count; i++) {
                archetypes[i].Dispose();
            }
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
    }
    
        public unsafe struct SpriteArchetype : IDisposable {
        internal SpriteChunk* Chunk;
        internal int count;
        public int instanceID;
        internal NativeHashSet<Entity> entities;
        internal NativeHashMap<int, int> indexes;
        internal Material Material;
        internal Mesh mesh;
        private ComputeBuffer matricesBuffer;
        private ComputeBuffer propertiesBuffer;
        private ComputeBuffer argsBuffer;
        private static readonly int matrices = Shader.PropertyToID("_Matrices");
        private static readonly int properties = Shader.PropertyToID("_Properties");

        public void Add(Entity entity) {
            if (entities.Add(entity)) {
                var indexInChunk = Chunk->Add();
                indexes[entity.id] = count;
                count++;
                entity.Add(new IndexInChunk {
                    value = indexInChunk,
                    chunk = Chunk
                });
            }
        }

        public void Remove(Entity entity) {
            if (entities.Remove(entity)) {
                int index = indexes[entity.id];
                if (index != -1) {
                    Chunk->Remove(index);
                    count--;
                }
                entity.Remove<IndexInChunk>();
            }
        }
        
        public void OnUpdate(ref World world) {
            
            if(count == 0) return;
            var dataArray = RenderDataArray();
            var matrixArray = MatrixArray();

            
            if (matricesBuffer == null || matricesBuffer.count != count)
            {
                matricesBuffer?.Release();
                matricesBuffer = new ComputeBuffer(count, UnsafeUtility.SizeOf<RenderMatrix>());
            }
            
            if (propertiesBuffer == null || propertiesBuffer.count != count)
            {
                propertiesBuffer?.Release();
                propertiesBuffer = new ComputeBuffer(count, UnsafeUtility.SizeOf<SpriteRenderData>());
            }
            
            matricesBuffer.SetData(matrixArray);
            propertiesBuffer.SetData(dataArray);
            
            Material.SetBuffer(matrices, matricesBuffer);
            Material.SetBuffer(properties, propertiesBuffer);
    
            var bounds = new Bounds(Vector3.zero, Vector3.one * 1000);
            //Graphics.DrawMeshInstancedProcedural(mesh, 0, Material, bounds, count);
            var r = new RenderParams();
            r.material = Material;
            r.worldBounds = bounds;
            r.receiveShadows = false;
            
            Graphics.RenderMeshPrimitives(in r, mesh, 0, count);

            matrixArray.Dispose();
            dataArray.Dispose();
            
        }

        private NativeArray<SpriteRenderData> RenderDataArray() 
        {
            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<SpriteRenderData>(
                Chunk->renderDataChunk, 
                count, 
                Allocator.None
            );

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, AtomicSafetyHandle.Create());
#endif

            return array;
        }

        private NativeArray<RenderMatrix> MatrixArray() 
        {
            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<RenderMatrix>(
                Chunk->matrixChunk, 
                count, 
                Allocator.None
            );

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, AtomicSafetyHandle.Create());
#endif

            return array;
        }

        public void Dispose() {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var dataArray = RenderDataArray();
            if (dataArray.IsCreated)
            {
                AtomicSafetyHandle.Release(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(dataArray));
            }

            var matrixArray = MatrixArray();
            if (matrixArray.IsCreated)
            {
                AtomicSafetyHandle.Release(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(matrixArray));
            }
#endif
            count = 0;
            SpriteChunk.Destroy(Chunk);
            entities.Dispose();
            indexes.Dispose();
            matricesBuffer?.Release();
            propertiesBuffer?.Release();
            argsBuffer?.Release();
        }
    }
    [BurstCompile]
    public struct FillDataJob : IJob {
        public GenericPool datas;
        public GenericPool matrixs;
        public NativeArray<SpriteRenderData> dataOut;
        public NativeArray<RenderMatrix> matrixOut;
        public int count;
        public void Execute() {
            for (int index = 1; index < count+1; index++) {
                dataOut[index] = datas.GetRef<SpriteRenderData>(index);
                matrixOut[index] = matrixs.GetRef<RenderMatrix>(index);
            }
        }
    }
    public struct SpriteChunk {
        internal unsafe SpriteRenderData* renderDataChunk;
        internal unsafe RenderMatrix* matrixChunk;
        internal int count;
        internal int capacity;
        internal int lastRemoved;

        public static unsafe SpriteChunk* Create(int size) {
            var ptr = (SpriteChunk*)UnsafeUtility.Malloc(sizeof(SpriteChunk), UnsafeUtility.AlignOf<SpriteChunk>(),
                Allocator.Persistent);
            *ptr = new SpriteChunk {
                renderDataChunk = UnsafeHelp.Malloc<SpriteRenderData>(size, Allocator.Persistent),
                matrixChunk = UnsafeHelp.Malloc<RenderMatrix>(size, Allocator.Persistent),
                count = 0,
                capacity = size,
                lastRemoved = 0
            };
            
            return ptr;
        }
        public int Add() {
            var index = count;
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

        public unsafe void UpdateData(int index, SpriteRenderData data, RenderMatrix matrix) {
            renderDataChunk[index] = data;
            matrixChunk[index] = matrix;
        }

        public static unsafe void Destroy(SpriteChunk* chunk) {
            UnsafeUtility.Free(chunk->renderDataChunk, Allocator.Persistent);
            UnsafeUtility.Free(chunk->matrixChunk, Allocator.Persistent);
            UnsafeUtility.Free(chunk, Allocator.Persistent);
        }
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
            var frames = SpriteAnimationsStorage.Instance.GetFrames(animation.AnimationID).List;
            int frameIndex = (int)(animation.CurrentTime / frameDuration) % frames.Length;
            renderData.SpriteIndex = frameIndex;
            renderData.FlipX = input.h < 0 ? -1 : 0;
            //renderData.SpriteTiling = CalculateSpriteTiling(renderData.SpriteIndex, animation.row, animation.col);
            renderData.SpriteTiling = GetSpriteTiling(renderData.SpriteIndex, ref frames);
            transform.position.z = transform.position.y*.1f;
        }
        private static float4 GetSpriteTiling(int spriteIndex, ref UnsafeList<float4> frames) {
            var r = frames.ElementAt(spriteIndex);
            return r;
        }
    }

    [BurstCompile]
    public struct UpdateChunkDataSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<RenderMatrix>().With<SpriteRenderData>().With<IndexInChunk>().With<Transform>();
        }

        public void OnUpdate(ref Entity entity, float deltaTime) {
            var data = entity.Read<SpriteRenderData>();
            ref var matrix = ref entity.Get<RenderMatrix>();
            ref var chunkIndex = ref entity.Get<IndexInChunk>();
            var transform = entity.Read<Transform>();
            GetMatrix(in data, ref matrix, in transform);

            unsafe {
                chunkIndex.chunk->UpdateData(chunkIndex.value, data, matrix);
            }
            
        }
        [BurstCompile]
        private static void GetMatrix(in SpriteRenderData data, ref RenderMatrix renderMatrix, in Transform transform) {
            var scale = new Vector3(
                transform.scale.x * (data.FlipX > 0 ? -1 : 1),
                transform.scale.y * (data.FlipY > 0 ? -1 : 1),
                transform.scale.z
            );

            // Создаем матрицу трансформации
            var scaleMatrix = Matrix4x4.Scale(scale);
            var rotationMatrix = Matrix4x4.Rotate(transform.rotation);
            var positionMatrix = Matrix4x4.Translate(transform.position);

            // Комбинируем матрицы
            renderMatrix.Matrix = positionMatrix * rotationMatrix * scaleMatrix;
        }
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
    [StructLayout(LayoutKind.Sequential)]
    public struct SpriteRenderData : IComponent {
        public int SpriteIndex;
        public float4 Color;
        public float4 SpriteTiling;
        public float FlipX; // Changed from bool to float
        public float FlipY; // Changed from bool to float
        public float ShadowAngle;
        public float ShadowLength;
        public float ShadowDistortion;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct RenderMatrix : IComponent {
        public Matrix4x4 Matrix;
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
    [StructLayout(LayoutKind.Sequential)]
    public struct IndexInChunk : IComponent {
        public int value;
        public unsafe SpriteChunk* chunk;
    }
}
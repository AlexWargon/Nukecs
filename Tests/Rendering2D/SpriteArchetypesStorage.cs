using System;
using System.Runtime.CompilerServices;
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
        
        public void OnUpdate() {
            for (int i = 0; i < count; i++) {
                archetypes[i].OnUpdate();
            }
        }
        public unsafe ref SpriteArchetype Add(Texture2D atlas, ref World world) {
            Resize();
            var instanceID = atlas.GetInstanceID();
            var h = Has(instanceID);
            if (h.has) {
                return ref archetypes[h.index];
            };
            var material = new Material(Shader.Find(ShaderNames.SpritesWithShadow)) {
                mainTexture = atlas
            };
            var arch = new SpriteArchetype {
                Material = material,
                mesh = CreateQuadMesh(),
                instanceID = instanceID,
                Chunk = SpriteChunk.Create(world.Unsafe->config.StartEntitiesAmount)
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
        private static Mesh CreateQuadMesh()
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
        [NativeDisableUnsafePtrRestriction]
        internal SpriteChunk* Chunk;
        private int count => Chunk->count;
        public int instanceID;
        internal Material Material;
        internal Mesh mesh;
        private ComputeBuffer transformsBuffer;
        private ComputeBuffer propertiesBuffer;
        private static readonly int matrices = Shader.PropertyToID("_Transforms");
        private static readonly int properties = Shader.PropertyToID("_Properties");
        public void AddInitial(ref Entity entity) {
            Chunk->AddInitial(entity.id);
            entity.Add(new SpriteChunkReference {
                chunk = Chunk
            });
        }
        public void Add(ref Entity entity, ref SpriteChunkReference spriteChunkReference) {
            Chunk->Add(in entity);
        }

        public void Remove(ref Entity entity, in SpriteChunkReference spriteChunkReference) {
            Chunk->Remove(in entity);
        }
        
        public void OnUpdate() {
            
            if(count == 0) return;
            var dataArray = RenderDataArray();
            var matrixArray = MatrixArray();
            
            if (transformsBuffer == null || transformsBuffer.count != count)
            {
                transformsBuffer?.Release();
                transformsBuffer = new ComputeBuffer(count, UnsafeUtility.SizeOf<Transform>());
            }
            
            if (propertiesBuffer == null || propertiesBuffer.count != count)
            {
                propertiesBuffer?.Release();
                propertiesBuffer = new ComputeBuffer(count, UnsafeUtility.SizeOf<SpriteRenderData>());
            }
            
            transformsBuffer.SetData(matrixArray);
            propertiesBuffer.SetData(dataArray);
            
            Material.SetBuffer(matrices, transformsBuffer);
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

        private NativeArray<Transform> MatrixArray() 
        {
            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<Transform>(
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
            SpriteChunk.Destroy(Chunk);
            transformsBuffer?.Release();
            propertiesBuffer?.Release();
        }
    }

    public struct SpriteChunk {
        [NativeDisableUnsafePtrRestriction] 
        internal unsafe SpriteRenderData* renderDataChunk;
        [NativeDisableUnsafePtrRestriction] 
        internal unsafe Transform* matrixChunk;

        internal UnsafeList<int> entityToIndex;
        internal UnsafeList<int> indexToEntity;
        internal UnsafeParallelHashSet<int> entitiesSet;
        internal int count;
        internal int capacity;
        internal int lastRemoved;

        public static unsafe SpriteChunk* Create(int size) {
            var ptr = (SpriteChunk*)UnsafeUtility.Malloc(sizeof(SpriteChunk), UnsafeUtility.AlignOf<SpriteChunk>(),
                Allocator.Persistent);
            *ptr = new SpriteChunk {
                renderDataChunk = UnsafeHelp.Malloc<SpriteRenderData>(size, Allocator.Persistent),
                matrixChunk = UnsafeHelp.Malloc<Transform>(size, Allocator.Persistent),
                entityToIndex = UnsafeHelp.UnsafeListWithMaximumLenght<int>(size, Allocator.Persistent, NativeArrayOptions.ClearMemory),
                indexToEntity = UnsafeHelp.UnsafeListWithMaximumLenght<int>(size, Allocator.Persistent, NativeArrayOptions.ClearMemory),
                entitiesSet = new UnsafeParallelHashSet<int>(size, Allocator.Persistent),
                count = 0,
                capacity = size,
                lastRemoved = 0
            };
            
            return ptr;
        }
        public int AddInitial(int entity) {
            var index = count;
            indexToEntity[count] = entity;
            entityToIndex[entity] = count;
            count++;
            return index;
        }
        public int Add(in Entity entity) {
            var index = count;
            indexToEntity[count] = entity.id;
            entityToIndex[entity.id] = count;
            count++;
            return index;
        }

        public unsafe void Remove(in Entity entity) {
            var lastIndex = count - 1;
            var lastEntityID = indexToEntity[lastIndex];
            var entityID = entity.id;
            if (lastEntityID != entityID && count > 0) {
                var entityIndex = entityToIndex[entityID];
                entityToIndex[lastEntityID] = entityIndex;
                indexToEntity[entityIndex] = lastEntityID;
                renderDataChunk[lastIndex] = renderDataChunk[entityIndex];
                matrixChunk[lastIndex] = matrixChunk[entityIndex];
            }
            count--;
            // var index = entityToIndex[entity.id] - 1;
            // entityToIndex[entity.id] = 0;
            // count--;
            // if (count > index) {
            //     indexToEntity[index] = indexToEntity[count];
            //     entityToIndex[indexToEntity[index]] = index + 1;
            // }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void UpdateData(int entity, in SpriteRenderData data, in Transform matrix) {
            var index = entityToIndex[entity];
            renderDataChunk[index] = data;
            matrixChunk[index] = matrix;
        }

        public static unsafe void Destroy(SpriteChunk* chunk) {
            UnsafeUtility.Free(chunk->renderDataChunk, Allocator.Persistent);
            UnsafeUtility.Free(chunk->matrixChunk, Allocator.Persistent);
            chunk->indexToEntity.Dispose();
            chunk->entityToIndex.Dispose();
            chunk->entitiesSet.Dispose();
            UnsafeUtility.Free(chunk, Allocator.Persistent);
        }
    }
    [BurstCompile]
    public struct SpriteAnimationSystem : IEntityJobSystem
    {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery()
                .With<SpriteAnimation>()
                .With<SpriteRenderData>()
                .With<Transform>()
                .With<Input>()
                .None<Culled>();
        }

        public unsafe void OnUpdate(ref Entity entity, float deltaTime) {
            ref var animation = ref entity.Get<SpriteAnimation>();
            ref var renderData = ref entity.Get<SpriteRenderData>();
            ref var transform = ref entity.Get<Transform>();
            ref readonly var input = ref entity.Read<Input>();
            
            animation.CurrentTime += deltaTime;
            float frameDuration = 1f / animation.FrameRate;
            var frames = SpriteAnimationsStorage.Instance.GetFrames(animation.AnimationID).List;
            int frameIndex = (int)(animation.CurrentTime / frameDuration) % frames.Length;
            renderData.SpriteIndex = frameIndex;
            renderData.FlipX = input.h < 0 ? -1 : 0;
            //renderData.SpriteTiling = CalculateSpriteTiling(renderData.SpriteIndex, animation.row, animation.col);
            renderData.SpriteTiling = GetSpriteTiling(renderData.SpriteIndex, ref frames);
            transform.Position.z = transform.Position.y*.1f;
        }
        private static float4 GetSpriteTiling(int spriteIndex, ref UnsafeList<float4> frames) {
            var r = frames.ElementAt(spriteIndex);
            return r;
        }
    }

    [BurstCompile]
    public unsafe struct UpdateChunkDataSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery()
                .With<SpriteRenderData>()
                .With<SpriteChunkReference>()
                .With<Transform>()
                .None<Culled>();
        }

        public void OnUpdate(ref Entity entity, float deltaTime) {
            ref readonly var data = ref entity.Read<SpriteRenderData>();
            ref var chunkIndex = ref entity.Get<SpriteChunkReference>();
            ref readonly var transform = ref entity.Read<Transform>();
            ref var chunk = ref *chunkIndex.chunk;
            chunk.UpdateData(entity.id, in data, in transform);
        }
    }
    
    public struct SpriteRenderSystem : ISystem
    {
        // public SystemMode Mode => SystemMode.Main;
        // public Query GetQuery(ref World world) {
        //     return world.CreateQuery()
        //         .With<SpriteRenderData>()
        //         .None<Culled>();
        // }
        // public void OnUpdate(ref Query query, float deltaTime) {
        //     SpriteArchetypesStorage.Singleton.OnUpdate();
        // }

        public void OnUpdate(ref World world, float deltaTime) {
            SpriteArchetypesStorage.Singleton.OnUpdate();
        }
    }
    [BurstCompile]
    public struct SpriteChangeAnimationSystem : IEntityJobSystem, IOnCreate {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery()
                .With<SpriteAnimation>()
                .With<Input>()
                .None<Culled>();
        }
        
        public void OnUpdate(ref Entity entity, float deltaTime) {
            ref readonly var input = ref entity.Read<Input>();
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
        public float4x4 Matrix;
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
    public struct SpriteChunkReference : IComponent {
        public unsafe SpriteChunk* chunk;
    }
    public struct Culled : IComponent { }
    
    public struct UpdateCameraCullingSystem : ISystem, IOnCreate {
        private Camera _camera;
        public void OnCreate(ref World world) {
            _camera = Camera.main;
        }
        public void OnUpdate(ref World world, float deltaTime) {
            CullingData.instance.Update(_camera);
        }
    }
    [BurstCompile]
    public struct CullSpritesSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery()
                .With<SpriteRenderData>()
                .With<SpriteChunkReference>()
                .None<Culled>();
        }

        public void OnUpdate(ref Entity entity, float deltaTime) {
            var data = CullingData.instance;
            var xMax = data.xMax;
            var yMax = data.yMax;
            var xMin = data.xMin;
            var yMin = data.yMin;
            ref readonly var transform = ref entity.Read<Transform>();
            if (!(transform.Position.x < xMax && 
                  transform.Position.x > xMin &&
                  transform.Position.y < yMax && 
                  transform.Position.y > yMin)) {
                entity.Cull();
            }
        }
    }
    [BurstCompile]
    public struct UnCullSpritesSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery()
                .With<SpriteRenderData>()
                .With<Culled>()
                .With<SpriteChunkReference>();
        }

        public void OnUpdate(ref Entity entity, float deltaTime) {
            var data = CullingData.instance;
            var xMax = data.xMax;
            var yMax = data.yMax;
            var xMin = data.xMin;
            var yMin = data.yMin;
            ref readonly var transform = ref entity.Read<Transform>();
            if (transform.Position.x < xMax && transform.Position.x > xMin && transform.Position.y < yMax &&
                transform.Position.y > yMin) {
                entity.UnCull();
            }
        }
    }

    public struct CullingSystem : ISystem, IOnCreate {
        public void OnCreate(ref World world) {
            culled = world.CreateQuery()
                .With<SpriteRenderData>()
                .With<SpriteChunkReference>()
                .With<Culled>();
                
            unculled = world.CreateQuery()
                .With<SpriteRenderData>()
                .With<SpriteChunkReference>()
                .None<Culled>();
        }

        private Query unculled;
        private Query culled;
        public void OnUpdate(ref World world, float deltaTime) {
            var data = CullingData.instance;
            var transforms = world.GetPool<Transform>().AsComponentPool<Transform>();
            world.Dependecies = new CullJob {
                xMax = data.xMax,
                xMin = data.xMin,
                yMax = data.yMax,
                yMin = data.yMin,
                transforms = transforms,
                query = unculled,
            }.Schedule(unculled.Count, 1, world.Dependecies);
            world.Dependecies = new UnCullJob {
                xMax = data.xMax,
                xMin = data.xMin,
                yMax = data.yMax,
                yMin = data.yMin,
                transforms = transforms,
                query = culled,
            }.Schedule(culled.Count, 1, world.Dependecies);
        }
        [BurstCompile]
        private struct CullJob : IJobParallelFor {
            public ComponentPool<Transform> transforms;
            public Query query;

            public float xMax;
            public float yMax;
            public float xMin;
            public float yMin;
            public void Execute(int index) {
                ref var entity = ref query.GetEntity(index);
                ref var transform = ref transforms.Get(entity.id);
                if (!(transform.Position.x < xMax && 
                      transform.Position.x > xMin &&
                      transform.Position.y < yMax && 
                      transform.Position.y > yMin)) {
                    entity.Cull();
                }
            }
        }
        
        [BurstCompile]
        private struct UnCullJob : IJobParallelFor {
            public ComponentPool<Transform> transforms;
            public Query query;

            public float xMax;
            public float yMax;
            public float xMin;
            public float yMin;
            public void Execute(int index) {
                ref var entity = ref query.GetEntity(index);
                ref var transform = ref transforms.Get(entity.id);
                if (transform.Position.x < xMax && transform.Position.x > xMin && transform.Position.y < yMax &&
                    transform.Position.y > yMin) {
                    entity.UnCull();
                }
            }
        }
    }
    public static class EntityRenderExtensions {
        public static unsafe void Cull(ref this Entity entity) {
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.Cull(entity.id);
        }

        public static unsafe void UnCull(ref this Entity entity) {
            ref var ecb = ref entity.worldPointer->ECB;
            ecb.UnCull(entity.id);
        }
    }
    public struct OnRemove<T> : IComponent where T : unmanaged, IComponent {
        
    }

    public struct OnAdd<T> : IComponent where T : unmanaged, IComponent {
        
    }
    public struct CullingData {
        public static ref CullingData instance => ref Singleton<CullingData>.Instance;
        public float Width;
        public float Height;
        public float2 CameraPositions;
        public float xMax;
        public float yMax;
        public float xMin;
        public float yMin;
        public void Update(Camera camera) {
            Height = camera.orthographicSize * 2f + 1f;
            Width = Height * camera.aspect;
            CameraPositions = new float2(camera.transform.position.x, camera.transform.position.y);
            xMax = CameraPositions.x + Width / 2;
            yMax = CameraPositions.y + Height / 2;
            xMin = CameraPositions.x - Width / 2;
            yMin = CameraPositions.y - Height / 2;
        }
    }
}
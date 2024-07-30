using System;
using System.Runtime.CompilerServices;
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
                Chunk = SpriteChunk.Create(world.Unsafe->config.StartEntitiesAmount),
                camera = Camera.main
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
        public int instanceID;
        internal Material Material;
        internal Mesh mesh;
        private ComputeBuffer transformsBuffer;
        private ComputeBuffer propertiesBuffer;
        private static readonly int matrices = Shader.PropertyToID("_Transforms");
        private static readonly int properties = Shader.PropertyToID("_Properties");
        public Camera camera;
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
            var count = Chunk->count;
            if(count == 0) return;
            var dataArray = RenderDataArray(count);
            var matrixArray = MatrixArray(count);
            
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

        private NativeArray<SpriteRenderData> RenderDataArray(int count) 
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

        private NativeArray<Transform> MatrixArray(int count) 
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
            var dataArray = RenderDataArray(Chunk->count);
            if (dataArray.IsCreated)
            {
                AtomicSafetyHandle.Release(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(dataArray));
            }

            dataArray.Dispose();
            var matrixArray = MatrixArray(Chunk->count);
            if (matrixArray.IsCreated)
            {
                AtomicSafetyHandle.Release(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(matrixArray));
            }

            matrixArray.Dispose();
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
            if (entity >= entityToIndex.m_length) {
                entityToIndex.Resize(entity * 2, NativeArrayOptions.ClearMemory);
                entityToIndex.m_length = entityToIndex.m_capacity;
                indexToEntity.Resize(entity * 2, NativeArrayOptions.ClearMemory);
                indexToEntity.m_length = indexToEntity.m_capacity;
            }
            indexToEntity[count] = entity;
            entityToIndex[entity] = count;
            count++;
            return index;
        }
        public int Add(in Entity entity) {
            var index = count;
            if (entity.id >= entityToIndex.m_length) {
                var newCapacity = entity.id * 2;
                entityToIndex.Resize(newCapacity, NativeArrayOptions.ClearMemory);
                entityToIndex.m_length = entityToIndex.m_capacity;
                indexToEntity.Resize(newCapacity, NativeArrayOptions.ClearMemory);
                indexToEntity.m_length = indexToEntity.m_capacity;
                unsafe {
                    UnsafeHelp.Resize(capacity, newCapacity, matrixChunk, Allocator.Persistent);
                    UnsafeHelp.Resize(capacity, newCapacity, renderDataChunk, Allocator.Persistent);
                }
                capacity = newCapacity;
            }
            indexToEntity[count] = entity.id;
            entityToIndex[entity.id] = count;
            count++;
            return index;
        }

        public unsafe void Remove(in Entity entity) {
            if(count <= 0) return;
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
            world.Dependencies = new CullJob {
                xMax = data.xMax,
                xMin = data.xMin,
                yMax = data.yMax,
                yMin = data.yMin,
                transforms = transforms,
                query = unculled,
            }.Schedule(unculled.Count, 1, world.Dependencies);
            world.Dependencies = new UnCullJob {
                xMax = data.xMax,
                xMin = data.xMin,
                yMax = data.yMax,
                yMin = data.yMin,
                transforms = transforms,
                query = culled,
            }.Schedule(culled.Count, 1, world.Dependencies);
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

    public struct ClearRenderOnEntityDestroySystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<DestroyEntity>().With<SpriteChunkReference>().None<Culled>();
        }

        public void OnUpdate(ref Entity entity, float deltaTime) {
            entity.Get<SpriteChunkReference>().ChunkRef.Remove(in entity);
        }
    }
}
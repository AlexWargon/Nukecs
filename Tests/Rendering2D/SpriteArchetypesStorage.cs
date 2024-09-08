namespace Wargon.Nukecs.Tests {
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;
    using UnityEngine;
    
    using Transform = Transforms.Transform;
    
    public static class ShaderNames {
        public const string Sprites = "Custom/SpriteShaderInstanced";
        public const string SpritesWithShadow = "Custom/SpriteShaderInstancedWithShadow";
    }
    public class SpriteArchetypesStorage : SingletonBase<SpriteArchetypesStorage> {
        internal SpriteArchetype[] archetypes = new SpriteArchetype[6];
        internal int count;
        
        public void OnUpdate() {
            for (var i = 0; i < count; i++) {
                archetypes[i].OnUpdate();
                archetypes[i].Clear();
            }
        }

        public void Clear() {
            for (var i = 0; i < count; i++) {
                archetypes[i].Clear();
            }
        }
        public unsafe ref SpriteArchetype Add(Texture2D atlas, Shader shader, ref World world) {
            Resize();
            var shaderID = shader.GetInstanceID();
            var instanceID = atlas.GetInstanceID();
            var h = Has(instanceID, shaderID);
            if (h.has) {
                return ref archetypes[h.index];
            };
            var material = new Material(shader) {
                mainTexture = atlas
            };
            var arch = new SpriteArchetype {
                Material = material,
                mesh = CreateQuadMesh(),
                instanceID = instanceID,
                shaderID = shaderID,
                Chunk = SpriteChunk.Create(world.UnsafeWorld->config.StartEntitiesAmount),
                camera = Camera.main
            };
            archetypes[count] = arch;
            count++;
            return ref archetypes[count-1];
        }
        
        public unsafe ref SpriteArchetype Add(Texture2D atlas, ref World world) {
            Resize();
            var shader = Shader.Find(ShaderNames.SpritesWithShadow);
            var shaderID = shader.GetInstanceID();
            var instanceID = atlas.GetInstanceID();
            var h = Has(instanceID, shaderID);
            if (h.has) {
                return ref archetypes[h.index];
            };
            var material = new Material(shader) {
                mainTexture = atlas
            };
            var arch = new SpriteArchetype {
                Material = material,
                mesh = CreateQuadMesh(),
                instanceID = instanceID,
                shaderID = shaderID,
                Chunk = SpriteChunk.Create(world.UnsafeWorld->config.StartEntitiesAmount),
                camera = Camera.main
            };
            archetypes[count] = arch;
            count++;
            return ref archetypes[count-1];
        }

        private (int index, bool has) Has(int id, int shader) {
            for (var i = count; i >= 0; i--)
            {
                ref readonly var  arch = ref archetypes[i];
                if (arch.instanceID == id && arch.shaderID == shader) return (i, true);
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
                    new (0, 0, 0),
                    new (1, 0, 0),
                    new (1, 1, 0),
                    new (0, 1, 0)
                },
                uv = new Vector2[] {
                    new (0, 0),
                    new (1, 0),
                    new (1, 1),
                    new (0, 1)
                },
                triangles = new [] { 0, 2, 1, 0, 3, 2 }
            };
            return mesh;
        }
        
        private static Mesh CreateQuad() {
            Mesh mesh = new Mesh();
            Vector3[] vertices = new Vector3[4];
            vertices[0] = new Vector3(0, 0, 0);
            vertices[1] = new Vector3(1, 0, 0);
            vertices[2] = new Vector3(0, 1, 0);
            vertices[3] = new Vector3(1, 1, 0);
            mesh.vertices = vertices;
 
            int[] tri = new int[6];
            tri[0] = 0;
            tri[1] = 2;
            tri[2] = 1;
            tri[3] = 2;
            tri[4] = 3;
            tri[5] = 1;
            mesh.triangles = tri;
 
            Vector3[] normals = new Vector3[4];
            normals[0] = -Vector3.forward;
            normals[1] = -Vector3.forward;
            normals[2] = -Vector3.forward;
            normals[3] = -Vector3.forward;
            mesh.normals = normals;
 
            Vector2[] uv = new Vector2[4];
            uv[0] = new Vector2(0, 0);
            uv[1] = new Vector2(1, 0);
            uv[2] = new Vector2(0, 1);
            uv[3] = new Vector2(1, 1);
            mesh.uv = uv;
 
            return mesh;
        }
    }
    
    public unsafe struct SpriteArchetype : IDisposable {
        [NativeDisableUnsafePtrRestriction]
        internal SpriteChunk* Chunk;
        public int instanceID;
        public int shaderID;
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
        
        public void Clear() {
            Chunk->Clear();
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
                Chunk->transforms, 
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
            SpriteChunk.Destroy(ref Chunk);
            transformsBuffer?.Release();
            propertiesBuffer?.Release();
        }
    }

    public struct SpriteChunk {
        [NativeDisableUnsafePtrRestriction] 
        internal unsafe SpriteRenderData* renderDataChunk;
        [NativeDisableUnsafePtrRestriction] 
        internal unsafe Transform* transforms;
        internal UnsafeList<int> entityToIndex;
        internal UnsafeList<int> indexToEntity;
        internal volatile int count;
        internal int capacity;
        internal int lastRemoved;

        public static unsafe SpriteChunk* Create(int size) {
            var ptr = Unsafe.Allocate<SpriteChunk>(Allocator.Persistent);
            *ptr = new SpriteChunk {
                renderDataChunk = Unsafe.Allocate<SpriteRenderData>(size, Allocator.Persistent),
                transforms = Unsafe.Allocate<Transform>(size, Allocator.Persistent),
                entityToIndex = UnsafeHelp.UnsafeListWithMaximumLenght<int>(size, Allocator.Persistent, NativeArrayOptions.ClearMemory),
                indexToEntity = UnsafeHelp.UnsafeListWithMaximumLenght<int>(size, Allocator.Persistent, NativeArrayOptions.ClearMemory),
                count = 0,
                capacity = size,
                lastRemoved = 0
            };
            
            return ptr;
        }
        public int AddInitial(int entity) {
            var index = count;
            if (entity >= entityToIndex.m_length) {
                var newCapacity = entity * 2;
                entityToIndex.Resize(newCapacity, NativeArrayOptions.ClearMemory);
                entityToIndex.m_length = entityToIndex.m_capacity;
                indexToEntity.Resize(newCapacity, NativeArrayOptions.ClearMemory);
                indexToEntity.m_length = indexToEntity.m_capacity;
                unsafe {
                    UnsafeHelp.Resize(capacity, newCapacity, ref transforms, Allocator.Persistent);
                    UnsafeHelp.Resize(capacity, newCapacity, ref renderDataChunk, Allocator.Persistent);
                }
                capacity = newCapacity;
            }
            indexToEntity[count] = entity;
            entityToIndex[entity] = count;
            //count++;
            Interlocked.Increment(ref count);
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
                    UnsafeHelp.Resize(capacity, newCapacity, ref transforms, Allocator.Persistent);
                    UnsafeHelp.Resize(capacity, newCapacity, ref renderDataChunk, Allocator.Persistent);
                }
                capacity = newCapacity;
            }
            indexToEntity[count] = entity.id;
            entityToIndex[entity.id] = count;
            Interlocked.Increment(ref count);
            return index;
        }

        public unsafe int Add(in Entity entity, in Transform transform, in SpriteRenderData data) {
            var index = count;
            Interlocked.Increment(ref count);
            if (entity.id >= entityToIndex.m_length) {
                var newCapacity = entity.id * 2;
                entityToIndex.Resize(newCapacity, NativeArrayOptions.ClearMemory);
                entityToIndex.m_length = entityToIndex.m_capacity;
                indexToEntity.Resize(newCapacity, NativeArrayOptions.ClearMemory);
                indexToEntity.m_length = indexToEntity.m_capacity;
                UnsafeHelp.Resize(capacity, newCapacity, ref transforms, Allocator.Persistent);
                UnsafeHelp.Resize(capacity, newCapacity, ref renderDataChunk, Allocator.Persistent);
                capacity = newCapacity;
            }
            indexToEntity[index] = entity.id;
            entityToIndex[entity.id] = index;
            transforms[index] = transform;
            renderDataChunk[index] = data;
            return index;
        }

        public unsafe void AddToFill(in Entity entity, in Transform transform, in SpriteRenderData data) {
            var index = count;
            Interlocked.Increment(ref count);
            if (index >= capacity) {
                var newCapacity = capacity * 2;
                UnsafeHelp.Resize(capacity, newCapacity, ref transforms, Allocator.Persistent);
                UnsafeHelp.Resize(capacity, newCapacity, ref renderDataChunk, Allocator.Persistent);
                capacity = newCapacity;
            }
            transforms[index] = transform;
            renderDataChunk[index] = data;
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
                transforms[lastIndex] = transforms[entityIndex];
            }

            Interlocked.Decrement(ref count);
            //count--;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void UpdateData(int entity, in SpriteRenderData data, in Transform transform) {
            var index = entityToIndex[entity];
            renderDataChunk[index] = data;
            transforms[index] = transform;
        }

        public unsafe void Clear() {
            indexToEntity.Clear();
            indexToEntity.m_length = indexToEntity.m_capacity;
            entityToIndex.Clear();
            entityToIndex.m_length = entityToIndex.m_capacity;
            count = 0;
        }
        public static unsafe void Destroy(ref SpriteChunk* chunk) {
            Unsafe.Free(chunk->transforms, chunk->capacity, Allocator.Persistent);
            Unsafe.Free(chunk->renderDataChunk, chunk->capacity, Allocator.Persistent);
            chunk->indexToEntity.Dispose();
            chunk->entityToIndex.Dispose();
            Unsafe.Free(chunk, AllocatorManager.Persistent);
        }
    }

    // public struct CullingSystem : ISystem, IOnCreate {
    //     public void OnCreate(ref World world) {
    //         culled = world.Query()
    //             .With<SpriteRenderData>()
    //             .With<SpriteChunkReference>()
    //             .With<Culled>();
    //             
    //         unculled = world.Query()
    //             .With<SpriteRenderData>()
    //             .With<SpriteChunkReference>()
    //             .None<Culled>();
    //     }
    //
    //     private Query unculled;
    //     private Query culled;
    //     public void OnUpdate(ref World world, float deltaTime) {
    //         var data = CullingData.instance;
    //         var transforms = world.GetPool<Transform>().AsComponentPool<Transform>();
    //         world.DependenciesUpdate = new CullJob {
    //             xMax = data.xMax,
    //             xMin = data.xMin,
    //             yMax = data.yMax,
    //             yMin = data.yMin,
    //             transforms = transforms,
    //             query = unculled,
    //         }.Schedule(unculled.Count, 1, world.DependenciesUpdate);
    //         world.DependenciesUpdate = new UnCullJob {
    //             xMax = data.xMax,
    //             xMin = data.xMin,
    //             yMax = data.yMax,
    //             yMin = data.yMin,
    //             transforms = transforms,
    //             query = culled,
    //         }.Schedule(culled.Count, 1, world.DependenciesUpdate);
    //     }
    //     [BurstCompile]
    //     private struct CullJob : IJobParallelFor {
    //         public ComponentPool<Transform> transforms;
    //         public Query query;
    //
    //         public float xMax;
    //         public float yMax;
    //         public float xMin;
    //         public float yMin;
    //         public void Execute(int index) {
    //             ref var entity = ref query.GetEntity(index);
    //             ref var transform = ref transforms.Get(entity.id);
    //             if (!(transform.Position.x < xMax && 
    //                   transform.Position.x > xMin &&
    //                   transform.Position.y < yMax && 
    //                   transform.Position.y > yMin)) {
    //                 entity.Cull();
    //             }
    //         }
    //     }
    //     
    //     [BurstCompile]
    //     private struct UnCullJob : IJobParallelFor {
    //         public ComponentPool<Transform> transforms;
    //         public Query query;
    //
    //         public float xMax;
    //         public float yMax;
    //         public float xMin;
    //         public float yMin;
    //         public void Execute(int index) {
    //             ref var entity = ref query.GetEntity(index);
    //             ref var transform = ref transforms.Get(entity.id);
    //             if (transform.Position.x < xMax && transform.Position.x > xMin && transform.Position.y < yMax &&
    //                 transform.Position.y > yMin) {
    //                 entity.UnCull();
    //             }
    //         }
    //     }
    // }

    public struct CullingData : IInit {
        public static ref CullingData instance => ref Singleton<CullingData>.Instance;
        public float Width;
        public float Height;
        public float2 CameraPositions;
        public float xMax;
        public float yMax;
        public float xMin;
        public float yMin;
        public void Init(){}
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
            return world.Query().With<DestroyEntity>().With<SpriteChunkReference>().None<Culled>();
        }

        public void OnUpdate(ref Entity entity, ref State state) {
            entity.Get<SpriteChunkReference>().ChunkRef.Remove(in entity);
        }
    }
}
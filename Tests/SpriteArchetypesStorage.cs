using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Wargon.Nukecs.Tests {
    public class SpriteArchetypesStorage : SingletonBase<SpriteArchetypesStorage> {
        internal SpriteArchetype[] archetypes = new SpriteArchetype[3];
        internal int count;

        public void OnUpdate() {
            for (int i = 0; i < count; i++) {
                archetypes[i].OnUpdate();
            }
        }
        public unsafe ref SpriteArchetype Add(Texture2D atlas) {
            Resize();
            var instanceID = atlas.GetInstanceID();
            var h = Has(instanceID);
            if (h.has) {
                return ref archetypes[h.index];
            };
            var material = new Material(Shader.Find("Custom/SpriteInstancing")) {
                mainTexture = atlas
            };
            var arch = new SpriteArchetype {
                Material = material,
                mesh = CreateQuadMesh(),
                instanceID = instanceID,
                entities = new NativeHashSet<Entity>(256, Allocator.Persistent),
                Chunk = SpriteChunk.Create(256),
                indexes = new NativeHashMap<int, int>(256, Allocator.Persistent)
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

        public void OnUpdate() {

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
            Graphics.DrawMeshInstancedProcedural(mesh, 0, Material, bounds, count);

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
}
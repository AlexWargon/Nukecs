using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Transform = Wargon.Nukecs.Transforms.Transform;

namespace Wargon.Nukecs.Tests
{
    public unsafe struct SpriteArchetype : IDisposable {
        [NativeDisableUnsafePtrRestriction]
        internal ptr<SpriteChunk> Chunk;
        public int instanceID;
        public int shaderID;
        public int index;
        internal Material Material;
        internal Material ShadowMaterial;
        internal Mesh Mesh;
        private ComputeBuffer transformsBuffer;
        private ComputeBuffer propertiesBuffer;
        private static readonly int matrices = Shader.PropertyToID("_Transforms");
        private static readonly int properties = Shader.PropertyToID("_Properties");
        public Camera camera;
        public bool RenderShadow;
        public void AddInitial(ref Entity entity) {
            Chunk.Ref.AddInitial(entity.id);
            entity.Add(new SpriteChunkReference {
                chunk = Chunk,
                achetypeIndex = index
            });
        }
        // public void Add(ref Entity entity, ref SpriteChunkReference spriteChunkReference) {
        //     Chunk.Ref.Add(in entity);
        // }
        //
        // public void Remove(ref Entity entity, in SpriteChunkReference spriteChunkReference) {
        //     Chunk.Ref.Remove(in entity);
        // }
        
        public void Clear() {
            Chunk.Ref.Clear();
        }
        public void OnUpdate() {
            var count = Chunk.Ref.count;

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
            var bounds = new Bounds(Vector3.zero, Vector3.one * 1000);
            
            transformsBuffer.SetData(matrixArray);
            propertiesBuffer.SetData(dataArray);
            
            Material.SetBuffer(matrices, transformsBuffer);
            Material.SetBuffer(properties, propertiesBuffer);
            if (RenderShadow)
            {
                ShadowMaterial.SetBuffer(matrices, transformsBuffer);
                ShadowMaterial.SetBuffer(properties, propertiesBuffer);
                Graphics.DrawMeshInstancedProcedural(Mesh, 0, ShadowMaterial, bounds, count);
            }
            
            Graphics.DrawMeshInstancedProcedural(Mesh, 0, Material, bounds, count);
            
            // var r = new RenderParams();
            // r.material = Material;
            // r.worldBounds = bounds;
            // r.receiveShadows = false;
            //
            // Graphics.RenderMeshPrimitives(in r, mesh, 0, count);
            matrixArray.Dispose();
            dataArray.Dispose();
        }

        private NativeArray<SpriteRenderData> RenderDataArray(int count) 
        {
            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<SpriteRenderData>(
                Chunk.Ref.renderDataChunk, 
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
                Chunk.Ref.transforms, 
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
            var dataArray = RenderDataArray(Chunk.Ref.count);
            if (dataArray.IsCreated)
            {
                AtomicSafetyHandle.Release(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(dataArray));
            }

            dataArray.Dispose();
            var matrixArray = MatrixArray(Chunk.Ref.count);
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
}
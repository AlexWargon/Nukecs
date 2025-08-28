using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Transform = Wargon.Nukecs.Transforms.Transform;

namespace Wargon.Nukecs.Tests
{
    public unsafe struct SpriteArchetype : IDisposable {
        [NativeDisableUnsafePtrRestriction]
        internal ptr<SpriteChunk> chunk;
        public int instanceID;
        public int shaderID;
        public int index;
        internal Material material;
        internal Material shadowMaterial;
        internal Mesh mesh;
        private ComputeBuffer _transformsBuffer;
        private ComputeBuffer _propertiesBuffer;
        private static readonly int matrices = Shader.PropertyToID("_Transforms");
        private static readonly int properties = Shader.PropertyToID("_Properties");
        public Camera camera;
        public bool renderShadow;
        public void AddInitial(ref Entity entity) {
            chunk.Ref.AddInitial(entity.id);
            entity.Add(new SpriteChunkReference {
                chunk = chunk,
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
            chunk.Ref.Clear();
        }
        public void OnUpdate() {
            var count = chunk.Ref.count;

            if(count == 0) return;
            
            var dataArray = RenderDataArray(count);
            var matrixArray = MatrixArray(count);
            
            if (_transformsBuffer == null || _transformsBuffer.count != count)
            {
                _transformsBuffer?.Release();
                _transformsBuffer = new ComputeBuffer(count, UnsafeUtility.SizeOf<Transform>());
            }
            
            if (_propertiesBuffer == null || _propertiesBuffer.count != count)
            {
                _propertiesBuffer?.Release();
                _propertiesBuffer = new ComputeBuffer(count, UnsafeUtility.SizeOf<SpriteRenderData>());
            }
            var bounds = new Bounds(Vector3.zero, Vector3.one * 1000);
            
            _transformsBuffer.SetData(matrixArray);
            _propertiesBuffer.SetData(dataArray);
            
            material.SetBuffer(matrices, _transformsBuffer);
            material.SetBuffer(properties, _propertiesBuffer);
            if (renderShadow)
            {
                shadowMaterial.SetBuffer(matrices, _transformsBuffer);
                shadowMaterial.SetBuffer(properties, _propertiesBuffer);
                Graphics.DrawMeshInstancedProcedural(mesh, 0, shadowMaterial, bounds, count);
            }
            
            Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, count);

            matrixArray.Dispose();
            dataArray.Dispose();
        }

        private NativeArray<SpriteRenderData> RenderDataArray(int count) 
        {
            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<SpriteRenderData>(
                chunk.Ref.renderDataChunk, 
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
                chunk.Ref.transforms, 
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
            var dataArray = RenderDataArray(chunk.Ref.count);
            if (dataArray.IsCreated)
            {
                AtomicSafetyHandle.Release(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(dataArray));
            }

            dataArray.Dispose();
            var matrixArray = MatrixArray(chunk.Ref.count);
            if (matrixArray.IsCreated)
            {
                AtomicSafetyHandle.Release(NativeArrayUnsafeUtility.GetAtomicSafetyHandle(matrixArray));
            }

            matrixArray.Dispose();
#endif
            SpriteChunk.Destroy(ref chunk);
            _transformsBuffer?.Release();
            _propertiesBuffer?.Release();
            dbug.log("Sprite archetype disposed", Color.green);
        }
    }
}
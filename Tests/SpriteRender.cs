using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Wargon.Nukecs.Tests.Sprites {
    public class SpriteRender : SingletonBase<SpriteRender> {
        public List<Texture2D> atlases = new(3);
        public UnityEngine.Mesh mesh;
        public UnityEngine.Material material;
        private CommandBuffer cmd;
        private ComputeBuffer instanceBuffer;
        private List<SpriteRenderData> renderDataArray =
            new (12);
        private ComputeBuffer argsBuffer;
        private uint[] args;
        private Camera _camera;
        void CreateArgsBuffer()
        {
            args = new uint[5] { 0, 0, 0, 0, 0 };
            // Индекс 0 = количество индексов на инстанс (количество индексов меша)
            // Индекс 1 = количество инстансов
            // Индекс 2-4 = начальный индекс, базовый вертекс, и смещение инстансов

            argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            argsBuffer.SetData(args);
        }

        void ResetArgs(uint meshGetIndexCount, uint spriteCount) {
            args[0] = meshGetIndexCount;
            args[1] = spriteCount;
            
            argsBuffer.SetData(args);
        }
        public int Count => renderDataArray.Count;
        // public NativeArray<SpriteRenderData> GetDataAsNativeArray() {
        //     return renderDataArray.AsArray();
        // }
        public void Add(SpriteRenderData data) {
            renderDataArray.Add(data);
        }

        public void Clear() {
            renderDataArray.Clear();
        }

        public void OnStart(UnityEngine.Mesh mesh, UnityEngine.Material material) {
            this.material = material;
            this.mesh = mesh;
            cmd = new CommandBuffer();
            
            cmd.name = "Sprite Render";
            _camera = Camera.main;
            _camera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, cmd);
            CreateArgsBuffer();
            
        }
        public void OnUpdate(ref Query query) {
            if(query.Count == 0) return;
            if (instanceBuffer == null || instanceBuffer.count < query.Count)
            {
                instanceBuffer?.Release();
                instanceBuffer = new ComputeBuffer(query.Count, 32); // 3 (position) + 4 (uv) + 1 (atlasIndex) = 8 floats * 4 bytes
            }

            instanceBuffer.SetData(renderDataArray);

            if (argsBuffer == null)
            {
                CreateArgsBuffer();
            }
            material.SetBuffer("_InstanceBuffer", instanceBuffer);
            ResetArgs(mesh.GetIndexCount(0), (uint) query.Count);
            cmd.Clear();
            for (var i = 0; i < atlases.Count; i++) {
                cmd.SetGlobalTexture("_MainTex", atlases[i]);
                cmd.SetGlobalBuffer("_InstanceBuffer", instanceBuffer);
                cmd.DrawMeshInstancedIndirect(mesh, 0, material, 0, argsBuffer);    
            }
            Graphics.ExecuteCommandBuffer(cmd);
        }
        public void SpawnSprite(ref World world, float3 position, int atlasIndex, int frameCount, float frameTime, float2 frameSize, float2 startUV, int framesPerRow)
        {
            var entity = world.CreateEntity();
            entity.Add(new Transform(){Position = position});
            entity.Add( new SpriteAnimation
            {
                atlasIndex = atlasIndex,
                frameCount = frameCount,
                currentFrame = 0,
                frameTime = frameTime,
                elapsedTime = 0,
                frameSize = frameSize,
                startUV = startUV,
                framesPerRow = framesPerRow
            });
            entity.Add(new SpriteRenderData());
        }

        public void OnDestroy() {
            if (instanceBuffer != null)
            {
                instanceBuffer.Release();
                instanceBuffer = null;
            }
            if (argsBuffer != null)
            {
                argsBuffer.Release();
                argsBuffer = null;
            }
            if (cmd != null)
            {
                _camera.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, cmd);
            }
        }
    }
}
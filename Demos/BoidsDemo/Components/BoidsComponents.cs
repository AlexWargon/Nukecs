using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs.Demos.Boids
{
    public struct BoidTag : IComponent { }

    public struct Velocity : IComponent
    {
        public float3 Value;
    }

    public struct BoidCount : IRes
    {
        public int Value;
        public void OnCreate(ref World world)
        {
            
        }

        public void OnUpdate(ref World world)
        {
            
        }
    }
    public struct BoidRenderData : IRes
    {
        NativeArray<Matrix4x4> matrices;
        public int count;

        public void OnCreate(ref World world) { }
        public void OnUpdate(ref World world) { }

        public NativeArray<Matrix4x4> Matrices => matrices;
        public bool IsCreated => matrices.IsCreated;

        public void Allocate(int size)
        {
            if (matrices.IsCreated) matrices.Dispose();
            matrices = new NativeArray<Matrix4x4>(size + 256, Allocator.Persistent);
        }

        public void Dispose()
        {
            if (matrices.IsCreated) matrices.Dispose();
        }
    }

    public class MeshData : IRes
    {
        public Mesh Mesh;
        public Material Material;
        public void OnCreate(ref World world) { }
        public void OnUpdate(ref World world) { }
    }
}

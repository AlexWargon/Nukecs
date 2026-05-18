using Unity.Collections;
using UnityEngine;

namespace Wargon.Nukecs.Demos.CubeSculpture
{
    public struct RenderBridge : IRes
    {
        NativeArray<Matrix4x4> matrices;
        public int count;
        public void OnCreate(ref World world)
        {
            var cfg = CubeSculptureBootstrap.Instance;
            if (cfg == null) return;
            if (matrices.IsCreated) return;
            matrices = new NativeArray<Matrix4x4>(cfg.TargetCount + 256, Allocator.Persistent);
        }

        public void OnUpdate(ref World world) { }

        public NativeArray<Matrix4x4> Matrices => matrices;

        public int Length => matrices.IsCreated ? matrices.Length : 0;
    }
}

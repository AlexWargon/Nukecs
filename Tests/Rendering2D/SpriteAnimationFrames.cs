using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Wargon.Nukecs.Tests {
    public struct SpriteAnimationFrames : IDisposable {
        public UnsafeList<float4> List;
        public int AnimationID;
        public SpriteAnimationFrames(int amount, int animationID) {
            List = new UnsafeList<float4>(amount, Allocator.Persistent);
            AnimationID = animationID;
        }

        public void Dispose() {
            List.Dispose();
        }
    }
}
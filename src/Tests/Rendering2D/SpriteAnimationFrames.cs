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

    public unsafe struct SpriteAnimationGroup : IDisposable {
        public UnsafeHashMap<int, SpriteAnimationFrames>* frames;

        public SpriteAnimationGroup(int size) {
            frames = AllocatorManager.Allocate<UnsafeHashMap<int, SpriteAnimationFrames>>(Allocator.Persistent);
            *frames = new UnsafeHashMap<int, SpriteAnimationFrames>(size, Allocator.Persistent);
        }
        public bool Has(int id) => frames->ContainsKey(id);
        public void Add(int id, ref SpriteAnimationFrames animationFrames) {
            frames->Add(id, animationFrames);
        }
        public SpriteAnimationFrames GetFrames(int id) {
            frames->TryGetValue(id, out var data);
            return data;
        }
        public void Dispose() {
            if(frames == null) return;
            ref var f = ref *frames;
            foreach (var kvPair in f) {
                kvPair.Value.Dispose();
            }
            frames->Dispose();
            
            AllocatorManager.Free(Allocator.Persistent, frames);
        }
    }
}
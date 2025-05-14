using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Wargon.Nukecs.Tests {
    public struct SpriteAnimationFrames : IDisposable {
        public UnsafeList<float4> List;
        public int AnimationID;
        public SpriteAnimationFrames(int amount, int animationID, Allocator allocator) {
            List = new UnsafeList<float4>(amount, allocator);
            AnimationID = animationID;
        }

        public void Dispose() {
            List.Dispose();
        }
    }

    public unsafe struct SpriteAnimationGroup : IDisposable {
        public UnsafeHashMap<int, SpriteAnimationFrames>* frames;

        public SpriteAnimationGroup(int size, Allocator allocator)
        {
            frames = World.Default.UnsafeWorld->_allocate<UnsafeHashMap<int,SpriteAnimationFrames>>();
            *frames = new UnsafeHashMap<int, SpriteAnimationFrames>(size, allocator);
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
            
            World.Default.UnsafeWorld->_free(frames);
        }
    }
}
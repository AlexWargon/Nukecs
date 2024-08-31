using System;
using Unity.Collections;

namespace Wargon.Nukecs.Tests {
    public struct SpriteAnimationsStorage : IDisposable, IInit {
        public static ref SpriteAnimationsStorage Instance => ref Singleton<SpriteAnimationsStorage>.Instance;
        private NativeHashMap<int, SpriteAnimationFrames> _frames;
        private bool _isInitialized;

        public bool Has(int id) => _frames.ContainsKey(id);
        public void Add(int id, SpriteAnimationFrames animationFrames) {
            _frames[id] = animationFrames;
        }
        public SpriteAnimationFrames GetFrames(int id) {
            return _frames[id];
        }

        public void Dispose() {
            foreach (var kvPair in _frames) {
                kvPair.Value.Dispose();
            }
            _frames.Dispose();
        }

        public void Init()
        {
            if(_isInitialized) return;
            _frames = new NativeHashMap<int, SpriteAnimationFrames>(6, Allocator.Persistent);
            _isInitialized = true;
        }
    }
}
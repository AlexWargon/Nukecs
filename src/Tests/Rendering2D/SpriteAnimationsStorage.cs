using System;
using Unity.Collections;

namespace Wargon.Nukecs.Tests {
    public struct SpriteAnimationsStorage : IDisposable, IInit {
        public static ref SpriteAnimationsStorage Singleton => ref Singleton<SpriteAnimationsStorage>.Instance;
        private NativeHashMap<int, SpriteAnimationGroup> groups;
        private bool _isInitialized;

        public bool Has(int id, int group) {
            if (groups.ContainsKey(group))
                return groups[group].Has(id);
            return false;
        }
        public void Add(int id, int group, ref SpriteAnimationFrames animationFrames) {
            if (!groups.ContainsKey(group)) {
                groups[group] = new SpriteAnimationGroup(6);
            }
            groups[group].Add(id, ref animationFrames);
        }
        public SpriteAnimationFrames GetFrames(int group ,int id) {
            
            return groups[group].GetFrames(id);
        }

        public bool TryGetFrames(int group, int id, out SpriteAnimationFrames frames)
        {
            if (groups.ContainsKey(group))
            {
                frames = groups[group].GetFrames(id);
                return true;
            }
            dbug.error($"Group {group} doesn't exist or doesn't have frames with id {id}");
            frames = default;
            return false;
        }

        public void Dispose() {
            foreach (var kvPair in groups) {
                kvPair.Value.Dispose();
            }
            groups.Dispose();
        }

        public void Init()
        {
            if(_isInitialized) return;
            groups = new NativeHashMap<int, SpriteAnimationGroup>(6, Allocator.Persistent);
            _isInitialized = true;
        }
    }
}
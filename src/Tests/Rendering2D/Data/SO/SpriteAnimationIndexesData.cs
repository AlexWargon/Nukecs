using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Wargon.Nukecs.Tests
{
    [CreateAssetMenu(fileName = "New SpriteAnimationIndexesData", menuName = "ECS/Sprite Animation Indexes")]
    public class SpriteAnimationIndexesData : Convertor
    {
        public SpriteAnimationList[] Animations;
        public override void Convert(ref World world, ref Entity entity)
        {
            var animator = new SpriteAnimationIndexes(Animations.Length, world.Allocator);
            foreach (var spriteAnimationList in Animations)
            {
                spriteAnimationList.Convert(ref world, ref entity);
                animator.Groups.Add(Animator.StringToHash(spriteAnimationList.name));
            }
            entity.Add(animator);
        }
    }
    public struct SpriteAnimationIndexes : IComponent, IDisposable, ICopyable<SpriteAnimationIndexes>
    {
        public UnsafeList<int> Groups;
        private readonly Allocator _allocator;
        public SpriteAnimationIndexes(int size)
        {
            Groups = new UnsafeList<int>(size, Allocator.Persistent);
            _allocator = Allocator.Persistent;
        }
        public SpriteAnimationIndexes(int size, Allocator allocator)
        {
            Groups = new UnsafeList<int>(size, allocator);
            _allocator = allocator;
        }
        public void Dispose()
        {
            Groups.Dispose();
        }

        public SpriteAnimationIndexes Copy(int to)
        {
            var copy = new SpriteAnimationIndexes(Groups.Length, _allocator);
            copy.Groups.CopyFrom(in Groups);
            return copy;
        }
    }
}
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
            var animator = new SpriteAnimationIndexes(Animations.Length);
            foreach (var spriteAnimationList in Animations)
            {
                spriteAnimationList.Convert(ref world, ref entity);
                animator.Groups.Add(Animator.StringToHash(spriteAnimationList.name));
            }
            entity.Add(animator);
        }
    }
    public struct SpriteAnimationIndexes : IComponent, IDisposable<SpriteAnimationIndexes>, ICopyable<SpriteAnimationIndexes>
    {
        public NativeList<int> Groups;

        public SpriteAnimationIndexes(int size)
        {
            Groups = new NativeList<int>(size, Allocator.Persistent);
        }
        public void Dispose(ref SpriteAnimationIndexes value)
        {
            try
            {
                value.Groups.Dispose();
            }
            catch (Exception ex)
            {
                Debug.Log(ex);
            }
                
        }

        public SpriteAnimationIndexes Copy(ref SpriteAnimationIndexes toCopy, int to)
        {
            var copy = new SpriteAnimationIndexes(toCopy.Groups.Length);
            copy.Groups.CopyFrom(in toCopy.Groups);
            return copy;
        }
    }
}
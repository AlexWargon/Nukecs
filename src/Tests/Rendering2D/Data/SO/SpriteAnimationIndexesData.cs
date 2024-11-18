using System;
using Unity.Collections;
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
    public struct SpriteAnimationIndexes : IComponent, IDisposable, ICopyable<SpriteAnimationIndexes>
    {
        public NativeList<int> Groups;

        public SpriteAnimationIndexes(int size)
        {
            Groups = new NativeList<int>(size, Allocator.Persistent);
        }
        public void Dispose()
        {
            Groups.Dispose();
            //dbug.log($"sprite animation indexes disposed");
        }

        public SpriteAnimationIndexes Copy(ref SpriteAnimationIndexes toCopy, int to)
        {
            var copy = new SpriteAnimationIndexes(toCopy.Groups.Length);
            copy.Groups.CopyFrom(in toCopy.Groups);
            return copy;
        }
    }
}
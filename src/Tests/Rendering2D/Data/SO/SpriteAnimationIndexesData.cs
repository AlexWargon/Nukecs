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
            var animator = new SpriteAnimationIndexes();
            foreach (var spriteAnimationList in Animations)
            {
                spriteAnimationList.Convert(ref world, ref entity);
                animator.Groups.Add(Animator.StringToHash(spriteAnimationList.name));
            }
            entity.Add(animator);
        }
    }
    public struct SpriteAnimationIndexes : IComponent
        //, IDisposable, ICopyable<SpriteAnimationIndexes>
    {
        
        public FixedBuffer4 Groups;
        // private readonly Allocator _allocator;
        // public SpriteAnimationIndexes(int size)
        // {
        //     Groups = new Unity.Collections.LowLevel.Unsafe.UnsafeList<int>(size, Allocator.Persistent);
        //     _allocator = Allocator.Persistent;
        // }
        // public SpriteAnimationIndexes(int size, Allocator allocator)
        // {
        //     Groups = new Unity.Collections.LowLevel.Unsafe.UnsafeList<int>(size, allocator);
        //     _allocator = allocator;
        // }
        // public void Dispose()
        // {
        //     Groups.Dispose();
        // }

        // public SpriteAnimationIndexes Copy(int to)
        // {
        //     var copy = new SpriteAnimationIndexes(Groups.Length, _allocator);
        //     copy.Groups.CopyFrom(in Groups);
        //     return copy;
        // }
    }

    public unsafe struct FixedBuffer4
    {
        private int count;
        public fixed int Buffer[4];

        public void Add(int data)
        {
            if(count == 3) return;
            this.Buffer[count++] = data;
        }
    }
    public unsafe struct FixedBuffer12
    {
        private int count;
        public fixed int Buffer[12];

        public void Add(int data)
        {
            if(count == 11) return;
            this.Buffer[count++] = data;
        }
    }
}
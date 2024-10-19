using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Transform = Wargon.Nukecs.Transforms.Transform;

namespace Wargon.Nukecs.Tests {
    [BurstCompile]
    public struct SpriteAnimationSystem : IEntityJobSystem
    {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.Query()
                .With<SpriteAnimation>()
                .With<SpriteRenderData>()
                .With<Transform>()
                .With<Input>()
                .None<Culled>();
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public void OnUpdate(ref Entity entity, ref State state) {
            ref var animation = ref entity.Get<SpriteAnimation>();
            ref var renderData = ref entity.Get<SpriteRenderData>();
            ref var transform = ref entity.Get<Transform>();
            ref readonly var input = ref entity.Read<Input>();
            
            animation.CurrentTime += state.Time.DeltaTime;
            var frameDuration = 1f / animation.FrameRate;
            if (SpriteAnimationsStorage.Singleton.TryGetFrames(animation.Group, animation.AnimationID, out var frames))
            {
                if (frames.List.m_length == 0)
                {
                    dbug.log($"{animation.AnimationID} : {animation.Group}: No frames available");
                    return;
                }
                var frameIndex = (int)(animation.CurrentTime / frameDuration) % frames.List.m_length;

                if(input.h is > 0 or < 0 && renderData.CanFlip)
                {
                    renderData.FlipX = input.h < 0 ? -1 : 0;
                }
                //renderData.FlipX = math.abs(flipX - renderData.FlipX) > 0.5f ? flipX : renderData.FlipX;
                renderData.SpriteTiling = GetSpriteTiling(frameIndex, ref frames.List);
                transform.Position.z = transform.Position.y*0.01f;
            }
            //var frames = SpriteAnimationsStorage.Singleton.GetFrames(animation.Group, animation.AnimationID).List;

        }
        private static float4 GetSpriteTiling(int spriteIndex, ref UnsafeList<float4> frames) {
            var r = frames.ElementAt(spriteIndex);
            return r;
        }
    }
}
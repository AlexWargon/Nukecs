using Unity.Burst;
using UnityEngine;

namespace Wargon.Nukecs.Tests {
    [BurstCompile]
    public struct SpriteChangeAnimationSystem : IEntityJobSystem, IOnCreate {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.Query()
                .With<SpriteAnimation>()
                .With<Input>()
                .None<Culled>();
        }
        
        public void OnUpdate(ref Entity entity, ref State state) {
            ref readonly var input = ref entity.Read<Input>();
            ref var anim = ref entity.Get<SpriteAnimation>();
            anim.AnimationID = input.h is > 0f or < 0f || input.v is > 0f or < 0f ? Run : Idle;
        }

        public void OnCreate(ref World world) {
            Run = Animator.StringToHash(nameof(Run));
            Idle = Animator.StringToHash(nameof(Idle));
        }
        private int Run;
        private int Idle;
    }
}
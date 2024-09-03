using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs.Tests {
    [CreateAssetMenu(fileName = "New Sprite Animation List", menuName = "ECS/Sprite Animation List")]
    public class SpriteAnimationList : Convertor {
        [SerializeField] private SpriteAnimationData[] Animations;
        public Entity Convert(ref World world, float3 pos) {
            foreach (var spriteAnimationData in Animations) {
                spriteAnimationData.AddToStorage();
            }
            var e = Animations[0].CreateAnimatedSpriteEntity(ref world, pos);
            return e;
        }

        public override void Convert(ref World world, ref Entity entity) {
            foreach (var spriteAnimationData in Animations) {
                spriteAnimationData.AddToStorage();
            }
            Animations[0].Convert(ref world, ref entity);
        }
    }
}
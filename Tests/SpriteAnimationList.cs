using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs.Tests {
    [CreateAssetMenu(fileName = "New Sprite Animation List", menuName = "ECS/Sprite Animation List")]
    public class SpriteAnimationList : ScriptableObject {
        [SerializeField] private SpriteAnimationData[] Animations;
        public Entity Convert(ref World world, float3 pos) {
            foreach (var spriteAnimationData in Animations) {
                spriteAnimationData.AddToStorage();
            }
            var e = Animations[0].CreateAnimatedSpriteEntity(ref world, pos);
            return e;
        }
    }
}
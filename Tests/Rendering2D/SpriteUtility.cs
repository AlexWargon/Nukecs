using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs.Tests {
    internal static class SpriteUtility {
        public static float4 CalculateSpriteTiling(Sprite sprite)
        {
            var texture = sprite.texture;
            var rect = sprite.textureRect;
            return new float4(
                rect.x / texture.width,
                rect.y / texture.height,
                rect.width / texture.width,
                rect.height / texture.height
            );
        }
    }
}
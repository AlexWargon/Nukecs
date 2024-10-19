using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs.Tests {
    internal static class SpriteUtility {
        public static float4 CalculateSpriteTiling(UnityEngine.Sprite sprite)
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
        public static float4 CalculateUV(UnityEngine.Sprite sprite, Vector2 originalSize)
        {
            Texture2D texture = sprite.texture;
            Rect rect = sprite.textureRect;
       
            // Рассчитываем масштаб обрезки
            float scaleX = originalSize.x / rect.width;
            float scaleY = originalSize.y / rect.height;
       
            // Рассчитываем новые UV координаты с учетом обрезки
            float u = rect.x / texture.width;
            float v = rect.y / texture.height;
            float w = (rect.width / texture.width) * scaleX;
            float h = (rect.height / texture.height) * scaleY;
       
            // Центрируем UV координаты
            float centerU = u + (rect.width / texture.width) * 0.5f;
            float centerV = v + (rect.height / texture.height) * 0.5f;
       
            u = centerU - w * 0.5f;
            v = centerV - h * 0.5f;
       
            return new float4(u, v, w, h);
        }
        public static float4 GetTextureST(UnityEngine.Sprite sprite)
        {
            return CalculateSpriteTiling(sprite);
            
            var ratio = new Vector2(1f / sprite.texture.width, 1f / sprite.texture.height);
            var size = Vector2.Scale(sprite.textureRect.size, ratio);
            var offset = Vector2.Scale(sprite.textureRect.position, ratio);
            return new float4(offset.x, offset.y, size.x, size.y);
        }
    }
}
﻿using System;
using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs.Tests {
    [CreateAssetMenu(fileName = "New Sprite", menuName = "ECS/Sprite")]
    public class SpriteData : Convertor{
        public UnityEngine.Sprite sprite;
        public Color color = Color.white;
        public int layer = 0;
        [HideInInspector]
        public float4 uv;
        [SerializeField]
        private Shader shader;
        [SerializeField] 
        private bool renderShadow;
        private void OnValidate() {
            uv = SpriteUtility.GetTextureST(sprite);
        }

        public SpriteRenderData AddToEntity(ref World world, ref Entity entity)
        {
            if (sprite == null)
            {
                Debug.LogError("No sprites defined!");
                return default;
            }

            var d = color;
            
            var renderData = new SpriteRenderData
            {
                Color = new float4(d.r, d.g, d.b, d.a),
                FlipX = 0f,
                FlipY = 0f,
                SpriteTiling = uv,
                ShadowAngle = 135f,
                ShadowLength = 1f,
                ShadowDistortion = 0.5f,
                Layer = layer,
                PixelsPerUnit = sprite.pixelsPerUnit,
                SpriteSize = new float2(sprite.rect.width, sprite.rect.height),
                Pivot = new float2(
                    sprite.pivot.x / sprite.rect.width,
                    sprite.pivot.y / sprite.rect.height
                ),
            };
            entity.Add(in renderData);

            ref var archetype = ref SpriteArchetypesStorage.Singleton.Add(sprite.texture, shader, ref world, renderShadow);
            archetype.AddInitial(ref entity);
            return renderData;
        }

        public override void Convert(ref World world, ref Entity entity) {
            AddToEntity(ref world, ref entity);
        }

        public static void Convert(SpriteRenderer spriteRenderer, ref World world, ref Entity entity, bool shadow) {
            var sprite = spriteRenderer.sprite;
            var shader = spriteRenderer.sharedMaterial.shader;
            var d = spriteRenderer.color;
            var uv = SpriteUtility.GetTextureST(sprite);
            var renderData = new SpriteRenderData
            {
                Color = new float4(d.r, d.g, d.b, d.a),
                FlipX = 0f,
                FlipY = 0f,
                SpriteTiling = uv,
                ShadowAngle = 135f,
                ShadowLength = 1f,
                ShadowDistortion = 0.5f,
                Layer = spriteRenderer.sortingLayerID,
                PixelsPerUnit = sprite.pixelsPerUnit,
                SpriteSize = new float2(sprite.rect.width, sprite.rect.height),
                Pivot = new float2(
                    sprite.pivot.x / sprite.rect.width,
                    sprite.pivot.y / sprite.rect.height
                )
            };
            entity.Add(in renderData);
            ref var archetype = ref SpriteArchetypesStorage.Singleton.Add(sprite.texture, shader, ref world, shadow);
            archetype.AddInitial(ref entity);
        }
    }
}
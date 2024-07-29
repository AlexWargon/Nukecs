using System;
using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs.Tests {
    [CreateAssetMenu(fileName = "New Sprite", menuName = "ECS/Sprite")]
    public class SpriteData : ScriptableObject {
        public Sprite sprite;
        public Color color = Color.white;
        [HideInInspector]
        public float4 uv;
        private void OnValidate() {
            uv = SpriteUtility.CalculateSpriteTiling(sprite);
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
                SpriteIndex = 0,
                Color = new float4(d.r, d.g, d.b, d.a),
                FlipX = 0f,
                FlipY = 0f,
                SpriteTiling = uv,
                ShadowAngle = 135f,
                ShadowLength = 1f,
                ShadowDistortion = 0.5f
            };
            entity.Add(in renderData);

            ref var archetype = ref SpriteArchetypesStorage.Singleton.Add(sprite.texture, ref world);
            archetype.AddInitial(ref entity);
            return renderData;
        }
    }
}
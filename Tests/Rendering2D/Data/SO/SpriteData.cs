using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs.Tests {
    [CreateAssetMenu(fileName = "New Sprite", menuName = "ECS/Sprite")]
    public class SpriteData : ScriptableObject {
        public UnityEngine.Sprite sprite;
        public Color color = Color.white;
        public int layer = 0;
        [HideInInspector]
        public float4 uv;
        [SerializeField]
        private Shader shader;
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
                Color = new float4(d.r, d.g, d.b, d.a),
                FlipX = 0f,
                FlipY = 0f,
                SpriteTiling = uv,
                ShadowAngle = 135f,
                ShadowLength = 1f,
                ShadowDistortion = 0.5f,
                Layer = layer,
                PixelsPerUnit = 1 / sprite.pixelsPerUnit * math.min(sprite.textureRect.width, sprite.textureRect.height)
            };
            entity.Add(in renderData);

            ref var archetype = ref SpriteArchetypesStorage.Singleton.Add(sprite.texture, shader, ref world);
            archetype.AddInitial(ref entity);
            return renderData;
        }
    }
}
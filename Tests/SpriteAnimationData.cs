using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs.Tests
{
    [CreateAssetMenu(fileName = "New Sprite Animation", menuName = "ECS/Sprite Animation")]
    public class SpriteAnimationData : ScriptableObject
    {
        public Sprite[] sprites;
        public float frameRate = 10f;

        public unsafe Entity CreateAnimatedSpriteEntity(ref World world, float3 position)
        {
            if (sprites == null || sprites.Length == 0)
            {
                Debug.LogError("No sprites defined for animation!");
                return Entity.Null;
            }
            
            SpriteRender.Singleton.Initialize(sprites[0].texture);
            var entity = world.CreateEntity();
            var transform = new Transform(position);
            transform.position.z = 0;
            entity.Add(transform);
            var renderData = new SpriteRenderData
            {
                SpriteIndex = 0,
                Position = position,
                Rotation = quaternion.identity,
                Scale = new float3(1,1,1),
                Color = new Color32(255, 255, 255, 255),
                FlipX = true,
                FlipY = false,
                SpriteTiling = new float4(0, 0, 0.25f, 0.25f)
            };
            entity.Add(renderData);

            var animationComponent = new SpriteAnimation
            {
                FrameCount = math.min(sprites.Length, SpriteAnimation.MaxFrames),
                FrameRate = frameRate,
                CurrentTime = 0f,
                col = 1,
                row = 10
            };

            for (int i = 0; i < animationComponent.FrameCount; i++)
            {
                animationComponent.SpriteIndices[i] = i + 4;
            }
            entity.Add(animationComponent);
            entity.Add(new RenderMatrix());

            return entity;
        }

        private float4 CalculateSpriteTiling(Sprite sprite)
        {
            Texture2D texture = sprite.texture;
            Rect rect = sprite.textureRect;
            return new float4(
                rect.x / texture.width,
                rect.y / texture.height,
                rect.width / texture.width,
                rect.height / texture.height
            );
        }
    }
}
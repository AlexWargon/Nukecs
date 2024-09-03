using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;
using Transform = Wargon.Nukecs.Transforms.Transform;

namespace Wargon.Nukecs.Tests
{
#if UNITY_EDITOR
    using UnityEditor;
    [CustomEditor(typeof(SpriteAnimationData))]
    public class SpriteAnimationDataEditor : Editor {
        private float shadowLen;
        private const int MAX_COLOR_VALUE = 255;
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
    
            var world = World.Get(0);
            if(world.IsAlive == false) return;
            var data = target as SpriteAnimationData;

            var color = data.color;
            color = EditorGUILayout.ColorField("Runtime Color", color);
            if (color != data.color) {
                var pool = world.GetPool<SpriteRenderData>();
                for (int i = 0; i < pool.Count; i++) {
                    ref var spriteData = ref pool.GetRef<SpriteRenderData>(i);
                    spriteData.Color = new float4(color.r, color.g, color.b, color.a);
                }
            }
        }

    }
#endif

    [CreateAssetMenu(fileName = "New Sprite Animation", menuName = "ECS/Sprite Animation")]
    public class SpriteAnimationData : Convertor {
        public string AnimationName;
        public UnityEngine.Sprite[] sprites;
        //[HideInInspector]
        public Color color = Color.white;
        public int layer = 0;
        [SerializeField]
        private bool randomColor;
        public float frameRate = 10f;
        [SerializeField][HideInInspector]
        private float4[] framesUV;

        [SerializeField] private Shader shader;
        private void OnValidate() {
            framesUV = new float4[sprites.Length];
            for (var index = 0; index < sprites.Length; index++) {
                var sprite = sprites[index];
                var frame = SpriteUtility.CalculateSpriteTiling(sprite);
                framesUV[index] = frame;
            }

            AnimationName = name;
        }

        public void AddToStorage() {
            var animationID = Animator.StringToHash(AnimationName);

            if (!SpriteAnimationsStorage.Instance.Has(animationID)) {
                var frames = new SpriteAnimationFrames(sprites.Length, animationID);
                foreach (var float4 in framesUV) {
                    frames.List.Add(float4);
                }
                SpriteAnimationsStorage.Instance.Add(animationID, frames);
            }
        }
        public Entity CreateAnimatedSpriteEntity(ref World world, float3 position)
        {
            if (sprites == null || sprites.Length == 0)
            {
                Debug.LogError("No sprites defined for animation!");
                return Entity.Null;
            }

            var entity = world.Entity();
            var transform = new Transform(position);

            transform.Position.z = 0;
            entity.Add(transform);
            var d = color;
            var sprite = sprites[0];
            
            var renderData = new SpriteRenderData
            {
                Color = randomColor ? new float4(Random.value, Random.value, Random.value, 1) : new float4(d.r, d.g, d.b, d.a),
                FlipX = 0f,
                FlipY = 0f,
                SpriteTiling = SpriteUtility.CalculateSpriteTiling(sprite),
                ShadowAngle = 135f,
                ShadowLength = 0.5f,
                ShadowDistortion = 0.5f,
                Layer = layer,
                PixelsPerUnit = sprite.pixelsPerUnit,
                SpriteSize = new float2(sprite.rect.width, sprite.rect.height),
                Pivot = new float2(
                    sprite.pivot.x / sprite.rect.width,
                    sprite.pivot.y / sprite.rect.height
                )
            };
            entity.Add(renderData);


            var animationID = Animator.StringToHash(AnimationName);
            
            var animationComponent = new SpriteAnimation
            {
                FrameCount = math.min(sprites.Length, SpriteAnimation.MaxFrames),
                FrameRate = frameRate,
                CurrentTime = Random.value,
                AnimationID = animationID
            };
            entity.Add(animationComponent);

            ref var archetype = ref SpriteArchetypesStorage.Singleton.Add(sprite.texture, shader, ref world);
            archetype.AddInitial(ref entity);
            return entity;
        }

        public override void Convert(ref World world, ref Entity entity) {
            if (sprites == null || sprites.Length == 0)
            {
                Debug.LogError("No sprites defined for animation!");
                return;
            }

            var d = color;
            var sprite = sprites[0];
            
            var renderData = new SpriteRenderData
            {
                Color = randomColor ? new float4(Random.value, Random.value, Random.value, 1) : new float4(d.r, d.g, d.b, d.a),
                FlipX = 0f,
                FlipY = 0f,
                SpriteTiling = SpriteUtility.CalculateSpriteTiling(sprite),
                ShadowAngle = 135f,
                ShadowLength = 0.5f,
                ShadowDistortion = 0.5f,
                Layer = layer,
                PixelsPerUnit = sprite.pixelsPerUnit,
                SpriteSize = new float2(sprite.rect.width, sprite.rect.height),
                Pivot = new float2(
                    sprite.pivot.x / sprite.rect.width,
                    sprite.pivot.y / sprite.rect.height
                )
            };
            entity.Add(renderData);


            var animationID = Animator.StringToHash(AnimationName);
            
            var animationComponent = new SpriteAnimation
            {
                FrameCount = math.min(sprites.Length, SpriteAnimation.MaxFrames),
                FrameRate = frameRate,
                CurrentTime = Random.value,
                AnimationID = animationID
            };
            entity.Add(animationComponent);

            ref var archetype = ref SpriteArchetypesStorage.Singleton.Add(sprite.texture, shader, ref world);
            archetype.AddInitial(ref entity);
        }
    }
}
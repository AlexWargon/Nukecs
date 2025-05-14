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
            var data = target as SpriteAnimationData;
            EditorGUILayout.IntField("Runtime AnimationID", Animator.StringToHash(data.AnimationName));
            EditorGUILayout.IntField("Runtime Group", Animator.StringToHash(data.AnimationGroup));
            var world = World.Get(0);
            if(world.IsAlive == false) return;
            

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
        public string AnimationGroup;
        public bool canFlip;
        public UnityEngine.Sprite[] sprites;
        //[HideInInspector]
        public Color color = Color.white;
        public int layer = 0;
        [SerializeField]
        private bool randomColor;
        public float frameRate = 10f;
        [SerializeField]
        public float4[] framesUV;

        [SerializeField] private Shader shader;
        [SerializeField] private Material material;
        [SerializeField] private bool renderShadow;
        private void OnValidate() {
            framesUV = new float4[sprites.Length];
            for (var index = 0; index < sprites.Length; index++) {
                var sprite = sprites[index];
                var frame = SpriteUtility.GetTextureST(sprite);
                framesUV[index] = frame;
            }

            AnimationName = name;
        }

        private void InitFramesUV()
        {
            framesUV = new float4[sprites.Length];
            for (var index = 0; index < sprites.Length; index++) {
                var sprite = sprites[index];
                var frame = SpriteUtility.GetTextureST(sprite);
                framesUV[index] = frame;
            }
        }
        public void AddToStorage(ref World world) {
            SpriteAnimationsStorage.Singleton.Initialize(ref world);
            var animationID = Animator.StringToHash(AnimationName);
            var group = Animator.StringToHash(AnimationGroup);
            
            if (!SpriteAnimationsStorage.Singleton.Has(animationID ,group)) {
                var frames = new SpriteAnimationFrames(sprites.Length, animationID, world.Allocator);
                if (framesUV.Length == 0) InitFramesUV();
                foreach (var float4 in framesUV) {
                    frames.List.Add(float4);
                }
                //dbug.log($"id {animationID}:{AnimationName} with group {AnimationGroup}:{group} - added to sprite animations with {frames.List.Length} frames");
                SpriteAnimationsStorage.Singleton.Add(animationID, group, ref frames);
                //Check(animationID, group);
            }
        }

        private void Check(int id, int group)
        {
            var frames = SpriteAnimationsStorage.Singleton.GetFrames(group, id);
            dbug.log($"Check {frames.List.Length} frames in group {group}");
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
                SpriteTiling = SpriteUtility.GetTextureST(sprite),
                ShadowAngle = 35f,
                ShadowLength = 0.5f,
                ShadowDistortion = 0.5f,
                Layer = layer,
                PixelsPerUnit = sprite.pixelsPerUnit,
                SpriteSize = new float2(sprite.rect.width, sprite.rect.height),
                Pivot = new float2(
                    sprite.pivot.x / sprite.rect.width,
                    sprite.pivot.y / sprite.rect.height
                ),
                CanFlip = canFlip
            };
            entity.Add(renderData);

            
            var animationComponent = new SpriteAnimation
            {
                FrameCount = sprites.Length,
                FrameRate = frameRate,
                CurrentTime = Random.value,
                AnimationID = Animator.StringToHash(AnimationName),
                Group = Animator.StringToHash(AnimationGroup)
            };
            entity.Add(animationComponent);

            ref var archetype = ref SpriteArchetypesStorage.Singleton.Add(sprite.texture, material != null ? material.shader : shader, ref world, renderShadow);
            archetype.AddInitial(ref entity);
            return entity;
        }
        /// <summary>
        /// Animation
        /// </summary>
        /// <param name="world"></param>
        /// <param name="entity"></param>
        public override void Convert(ref World world, ref Entity entity) {
            if (sprites == null || sprites.Length == 0)
            {
                Debug.LogError("No sprites defined for animation!");
                return;
            }

            var d = color;
            var sprite = sprites[0];
            if (!entity.Has<SpriteRenderData>()) {
                var renderData = new SpriteRenderData
                {
                    Color = randomColor ? new float4(Random.value, Random.value, Random.value, 1) : new float4(d.r, d.g, d.b, d.a),
                    FlipX = 0f,
                    FlipY = 0f,
                    SpriteTiling = framesUV[0],
                    ShadowAngle = 135f,
                    ShadowLength = 0.5f,
                    ShadowDistortion = 0.5f,
                    Layer = layer,
                    PixelsPerUnit = sprite.pixelsPerUnit,
                    SpriteSize = new float2(sprite.rect.width, sprite.rect.height),
                    Pivot = new float2(
                        sprite.pivot.x / sprite.rect.width,
                        sprite.pivot.y / sprite.rect.height
                    ),
                    CanFlip = canFlip
                };
                entity.Add(renderData);
            }
            
            var animationComponent = new SpriteAnimation
            {
                FrameCount = sprites.Length,
                FrameRate = frameRate,
                CurrentTime = Random.value,
                AnimationID = Animator.StringToHash(AnimationName),
                Group = Animator.StringToHash(AnimationGroup)
            };
            entity.Add(animationComponent);

            ref var archetype = ref SpriteArchetypesStorage.Singleton.Add(sprite.texture, material != null ? material.shader : shader, ref world, renderShadow);
            archetype.AddInitial(ref entity);
        }
    }
}
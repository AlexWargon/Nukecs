﻿using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

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
    public class SpriteAnimationData : ScriptableObject {
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

            var entity = world.CreateEntity();
            var transform = new Transform(position);

            transform.Position.z = 0;
            entity.Add(transform);
            var d = color;
            
            var renderData = new SpriteRenderData
            {
                Color = randomColor ? new float4(Random.value, Random.value, Random.value, 1) : new float4(d.r, d.g, d.b, d.a),
                FlipX = 0f,
                FlipY = 0f,
                SpriteTiling = SpriteUtility.CalculateSpriteTiling(sprites[0]),
                ShadowAngle = 135f,
                ShadowLength = 1f,
                ShadowDistortion = 0.5f,
                Layer = layer
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

            ref var archetype = ref SpriteArchetypesStorage.Singleton.Add(sprites[0].texture, shader, ref world);
            archetype.AddInitial(ref entity);
            return entity;
        }
    }

    public static class RandomIndex {
        private static int current;
        private static int[] array5 = new[] {
            1,
            3,
            2,
            0,
            5,
            4,
            3
        };

        public static int Next() {
            if (current == array5.Length - 1) current = 0;
            return array5[current++];
        }
    }
}
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs.Tests
{

// #if UNITY_EDITOR
//     using UnityEditor;
//     [CustomEditor(typeof(SpriteAnimationData))]
//     public class SpriteAnimationDataEditor : Editor {
//         private float shadowLen;
//         public override void OnInspectorGUI() {
//             base.OnInspectorGUI();
//
//             var world = World.Get(0);
//             if(world.IsAlive == false) return;
//             var data = target as SpriteAnimationData;
//             var shadowLen = data.shadowLen;
//             data.shadowLen = EditorGUILayout.FloatField("shadow len", data.shadowLen);
//             
//         }
//     }
// #endif
    
    [CreateAssetMenu(fileName = "New Sprite Animation", menuName = "ECS/Sprite Animation")]
    public class SpriteAnimationData : ScriptableObject {
        public string AnimationName;
        public Sprite[] sprites;
        public Color color;
        public float frameRate = 10f;
        [SerializeField][HideInInspector]
        private float4[] framesUV;
        [HideInInspector]
        public float shadowLen;
        private void OnValidate() {
            framesUV = new float4[sprites.Length];
            for (var index = 0; index < sprites.Length; index++) {
                var sprite = sprites[index];
                var frame = CalculateSpriteTiling(sprite);
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
            
            //SpriteRendering.Singleton.Initialize(sprites[0].texture);
            var entity = world.CreateEntity();
            var transform = new Transform(position);

            transform.position.z = 0;
            entity.Add(transform);

            var renderData = new SpriteRenderData
            {
                SpriteIndex = 0,
                Color = new float4(1, 1, 1, 1),
                FlipX = 0f,
                FlipY = 0f,
                SpriteTiling = CalculateSpriteTiling(sprites[0])
            };
            renderData.ShadowAngle = 135f; // угол в градусах
            renderData.ShadowLength = 1f; // длина тени
            renderData.ShadowDistortion = 0.5f; // искажение тени
            entity.Add(renderData);


            var animationID = Animator.StringToHash(AnimationName);
            
            var animationComponent = new SpriteAnimation
            {
                FrameCount = math.min(sprites.Length, SpriteAnimation.MaxFrames),
                FrameRate = frameRate,
                CurrentTime = 0f,
                col = 1,
                row = 10,
                AnimationID = animationID
            };
            entity.Add(animationComponent);
            entity.Add(new RenderMatrix());
            
            ref var archetype = ref SpriteArchetypesStorage.Singleton.Add(sprites[0].texture, ref world);
            archetype.Add(entity);
            return entity;
        }

        private float4 CalculateSpriteTiling(Sprite sprite)
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
        
    public struct SpriteAnimationFrames : IDisposable {
        public UnsafeList<float4> List;
        public int AnimationID;
        public SpriteAnimationFrames(int amount, int animationID) {
            List = new UnsafeList<float4>(amount, Allocator.Persistent);
            AnimationID = animationID;
        }

        public void Dispose() {
            List.Dispose();
        }
    }

    public struct SpriteAnimationsStorage : IDisposable {
        public static ref SpriteAnimationsStorage Instance => ref Singleton<SpriteAnimationsStorage>.Instance;

        private NativeHashMap<int, SpriteAnimationFrames> frames;
        public bool isInitialized;
        public void Initialize(int amount) {
            if(isInitialized) return;
            frames = new NativeHashMap<int, SpriteAnimationFrames>(amount, Allocator.Persistent);
            isInitialized = true;
        }

        public bool Has(int id) => frames.ContainsKey(id);
        public void Add(int id, SpriteAnimationFrames animationFrames) {
            frames[id] = animationFrames;
        }
        public SpriteAnimationFrames GetFrames(int id) {
            return frames[id];
        }

        public void Dispose() {
            foreach (var kvPair in frames) {
                kvPair.Value.Dispose();
            }

            frames.Dispose();
        }
    }
}
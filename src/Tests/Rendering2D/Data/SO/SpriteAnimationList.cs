using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs.Tests {
    [CreateAssetMenu(fileName = "New Sprite Animation List", menuName = "ECS/Sprite Animation List")]
    public class SpriteAnimationList : Convertor {
        [SerializeField] public SpriteAnimationData[] Animations;
        public Entity Convert(ref World world, float3 pos) {
            foreach (var spriteAnimationData in Animations) {
                spriteAnimationData.AddToStorage(ref world);
            }
            var e = Animations[0].CreateAnimatedSpriteEntity(ref world, pos);
            return e;
        }

        public override void Convert(ref World world, ref Entity entity) {
            foreach (var spriteAnimationData in Animations) {
                spriteAnimationData.AddToStorage(ref world);
            }
            Animations[0].Convert(ref world, ref entity);
        }
    }

    [UnityEditor.CustomEditor(typeof(SpriteAnimationList))]
    class AnimationListEditor : UnityEditor.Editor {
        private int animationIndex;
        private int frameIndex;
        public int maxFrame => AnimationList.Animations[animationIndex].sprites.Length-1;
        private SpriteAnimationList AnimationList;
        private float animationSpeed = 0.08f;
        private bool runAnimation;
        private double delta;
        private double frameTime;
        private double pFrameTime;

        void UpdateDelta() {
            frameTime = UnityEditor.EditorApplication.timeSinceStartup;
            if(pFrameTime!=0d)
                delta = frameTime - pFrameTime;
            pFrameTime = UnityEditor.EditorApplication.timeSinceStartup;
        }

        public override bool HasPreviewGUI() => true;
        public override void OnPreviewGUI(Rect r, GUIStyle background) {
            if(!AnimationList) AnimationList = target as SpriteAnimationList;
            if(AnimationList && AnimationList.Animations.Length < 1) return;
            
            var sprite = AnimationList ? AnimationList.Animations[animationIndex].sprites[frameIndex] : null;

            if (sprite) {
                
                var previewTexture = UnityEditor.AssetPreview.GetAssetPreview(sprite);
                //UnityEngine.Sprite s = UnityEngine.Sprite.Create(sprite.texture, sprite.rect, sprite.pivot);
                //Texture2D previewTexture = UnityEditor.AssetPreview.GetAssetPreview(s);
                if (previewTexture) {
                    previewTexture.filterMode = FilterMode.Point;
                }
                UnityEditor.EditorGUI.DrawTextureTransparent(r, previewTexture, ScaleMode.ScaleToFit, 1);
            }
        }

        void OnUpdate() {
            if(UnityEditor.Selection.activeObject != target) return;
            UpdateDelta();
            if(AnimationList == null || AnimationList.Animations == null) return;
            
            if(AnimationList.Animations.Length < 1) return;
            if (runAnimation) {
                if (animationSpeed <= 0) {
                    frameIndex++;

                    if (frameIndex >= AnimationList.Animations[animationIndex].sprites.Length) {
                        frameIndex = 0;
                    }
                    
                    animationSpeed = 0.02f;
                }
                else {
                    animationSpeed -= (float)delta;
                }
            }

        }

        public override bool RequiresConstantRepaint() {
            return true;
        }

        private void OnEnable() {
            runAnimation = true;
            UnityEditor.EditorApplication.update += OnUpdate;
        }

        private void OnDisable() {
            UnityEditor.EditorApplication.update -= OnUpdate;
        }

        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            
            AnimationList = target as SpriteAnimationList;
            System.Diagnostics.Debug.Assert(AnimationList != null, nameof(AnimationList) + " != null");
            if (GUILayout.Button("<")) {
                animationIndex++;
                if (animationIndex == AnimationList.Animations.Length)
                    animationIndex = 0;
            }
            if (GUILayout.Button(">")) {
                animationIndex--;
                if (animationIndex == -1)
                    animationIndex = AnimationList.Animations.Length-1;
            }

            if (GUILayout.Button("Play")) {
                runAnimation = !runAnimation;
            }
        }
    }
}
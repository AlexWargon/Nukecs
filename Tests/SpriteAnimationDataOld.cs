using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs.Tests.Sprites {
    [CreateAssetMenu]
    public class SpriteAnimationDataOld : ScriptableObject {
        public Sprite[] Frames;
        public float FrameTime = .8f;

        public void ToEntity(ref World world, float3 pos) {
            var sprite = Frames[0];
            var tex = sprite.texture;
            if (SpriteRender.Singleton.atlases.Contains(tex) == false) {
                SpriteRender.Singleton.atlases.Add(tex);
                Debug.Log("ATLAS ADDED");
            }

            var frameSize = new float2(sprite.rect.width, sprite.rect.height);
            Debug.Log(frameSize.ToString());
            var startUV = new float2(sprite.rect.x / tex.width, sprite.rect.y / tex.height);
            Debug.Log(startUV);
            SpriteRender.Singleton.SpawnSprite(ref world, pos, 0, Frames.Length, FrameTime, frameSize, startUV, 10);
        }
    }
}

namespace Wargon.Nukecs.Tests {
}
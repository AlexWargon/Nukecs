using UnityEngine;

namespace Wargon.Nukecs.Tests {
    public class SpriteRendering : SingletonBase<SpriteRendering> {
        
        private Texture2D atlasTexture;
        private Material spriteMaterial;
        private Mesh quadMesh;
        private static readonly int color = Shader.PropertyToID("_Color");
        private static readonly int texCoord = Shader.PropertyToID("_TexCoord");
        private static readonly int flip = Shader.PropertyToID("_Flip");
        
        public void Initialize(Texture2D texture2D) {
            atlasTexture = texture2D;
            spriteMaterial = new Material(Shader.Find("Custom/SpriteShaderCompatible"));
            spriteMaterial.mainTexture = atlasTexture;
            quadMesh = CreateQuadMesh();
        }
        private Mesh CreateQuadMesh()
        {
            var mesh = new Mesh {
                vertices = new Vector3[] {
                    new (-0.5f, -0.5f, 0),
                    new (0.5f, -0.5f, 0),
                    new (0.5f, 0.5f, 0),
                    new (-0.5f, 0.5f, 0)
                },
                uv = new Vector2[] {
                    new (0, 0),
                    new (1, 0),
                    new (1, 1),
                    new (0, 1)
                },
                triangles = new [] { 0, 1, 2, 0, 2, 3 }
            };
            return mesh;
        }

        public void Render(ref Query query, ref World world)
        {
            ref var dataPool = ref world.GetPool<SpriteRenderData>();
            ref var matrixPool = ref world.GetPool<RenderMatrix>();
            var props = new MaterialPropertyBlock();

            for (int i = 0; i < query.Count; i++) {
                var e = query.GetEntity(i);
                var data = dataPool.GetRef<SpriteRenderData>(e.id);
                var matrix = matrixPool.GetRef<RenderMatrix>(e.id);

                props.Clear();
                //props.SetColor(color, data.Color);
                props.SetVector(texCoord, data.SpriteTiling);
                //props.SetVector(flip, matrix.Vector);
                Graphics.DrawMesh(quadMesh, matrix.Matrix, spriteMaterial, 0, null, 0, props);
            }
        }
    }
}
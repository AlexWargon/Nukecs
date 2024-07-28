Shader "Custom/SpriteInstancing2"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _TexCoord ("Texture Coordinates", Vector) = (0,0,1,1)
        _Flip ("Flip", Vector) = (1,1,1,1)
    }

    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off  // Отключаем отсечение задней грани
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct SpriteRenderData
            {
                int SpriteIndex;
                float3 Position;
                float4 Rotation;
                float3 Scale;
                float4 Color;
                float4 SpriteTiling;
                float FlipX;
                float FlipY;
                float2 Padding;
            };

            struct RenderMatrix
            {
                float4x4 Matrix;
            };

            StructuredBuffer<RenderMatrix> _Matrices;
            StructuredBuffer<SpriteRenderData> _Properties;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;

            v2f vert (appdata_t IN,  uint instanceID : SV_InstanceID)
            {
                v2f OUT;
                SpriteRenderData data = _Properties[instanceID];
                OUT.vertex = UnityObjectToClipPos(IN.vertex);

                float2 uv = IN.uv;
                uv.x = data.FlipX > 0.5 ? 1 - uv.x : uv.x;
                uv.y = data.FlipY > 0.5 ? 1 - uv.y : uv.y;
                float4 coord = data.SpriteTiling;
                OUT.uv = uv * abs(coord.zw) + coord.xy;
                OUT.color = data.Color;
                return OUT;
            }

            fixed4 frag (v2f IN, uint instanceID : SV_InstanceID) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.uv) * IN.color;
                return c;
            }
            ENDCG
        }
    }
}

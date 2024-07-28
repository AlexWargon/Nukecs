Shader "Custom/SpriteShaderInstanced"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _AlphaCutoff ("Alpha Cutoff", Range(0,1)) = 0.1
    }

    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float _AlphaCutoff;
            struct SpriteRenderData
            {
                int SpriteIndex;
                float4 Color;
                float4 SpriteTiling;
                float FlipX;
                float FlipY;
                float ShadowAngle;
                float ShadowLength;
                float ShadowDistortion;
            };

            struct RenderMatrix
            {
                float4x4 Matrix;
            };

            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            StructuredBuffer<RenderMatrix> _Matrices;
            StructuredBuffer<SpriteRenderData> _Properties;
            #endif

            void setup()
            {
                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                unity_ObjectToWorld = _Matrices[unity_InstanceID].Matrix;
                #endif
            }

            v2f vert (appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                SpriteRenderData data = _Properties[unity_InstanceID];
                float2 uv = IN.uv;
                uv.x = data.FlipX < 0 ? 1 - uv.x : uv.x;
                uv.y = data.FlipY < 0 ? 1 - uv.y : uv.y;
                
                OUT.uv = uv * abs(data.SpriteTiling.zw) + data.SpriteTiling.xy;
                OUT.color = data.Color;
                return OUT;
                #else
                OUT.uv = IN.uv;
                OUT.color = float4(1,1,1,1);
                #endif
                
                return OUT;
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                fixed4 c = tex2D(_MainTex, IN.uv) * IN.color;
                clip(c.a - _AlphaCutoff);
                return c;
            }
            ENDCG
        }
    }
}

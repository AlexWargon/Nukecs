Shader "Custom/SpriteInstancing"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        LOD 100
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

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

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

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                SpriteRenderData data = _Properties[unity_InstanceID];

                float2 uv = i.uv * data.SpriteTiling.zw + data.SpriteTiling.xy;
                if (data.FlipX > 0.5) uv.x = data.SpriteTiling.z - (uv.x - data.SpriteTiling.x) + data.SpriteTiling.x;
                if (data.FlipY > 0.5) uv.y = data.SpriteTiling.w - (uv.y - data.SpriteTiling.y) + data.SpriteTiling.y;

                fixed4 col = tex2D(_MainTex, uv) * data.Color;
                #else
                fixed4 col = tex2D(_MainTex, i.uv);
                #endif
                return col;
            }
            ENDCG
            
        }
    }
}

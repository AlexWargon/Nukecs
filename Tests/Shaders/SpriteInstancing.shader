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
        Cull Off  // Отключаем отсечение задней грани
        CGPROGRAM
        #pragma surface surf Lambert alpha:fade
        #pragma instancing_options procedural:setup

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex : TEXCOORD0;
        };

        struct SpriteRenderData
        {
            int SpriteIndex;
            float3 Position;
            float4 Rotation;
            float3 Scale;
            float4 Color;
            float4 SpriteTiling;
            bool FlipX;
            bool FlipY;
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

        void surf (Input IN, inout SurfaceOutput o)
        {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            SpriteRenderData data = _Properties[unity_InstanceID];

            // float2 uv = IN.uv_MainTex * data.SpriteTiling.zw + data.SpriteTiling.xy;
            // if (data.FlipX) uv.x = data.SpriteTiling.z - uv.x + data.SpriteTiling.x;
            // if (data.FlipY) uv.y = data.SpriteTiling.w - uv.y + data.SpriteTiling.y;
            //uv = uv * data.SpriteTiling.zw + data.SpriteTiling.xy;
            float2 uv = IN.uv_MainTex;
            uv.x = data.FlipX ? 1 - uv.x : uv.x;
            uv.y = data.FlipY ? 1 - uv.y : uv.y;
            fixed4 c = tex2D(_MainTex, uv) * data.Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
            #else
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo = c.rgb;
            o.Alpha = c.a;
            
            #endif
        }
        ENDCG
    }
}

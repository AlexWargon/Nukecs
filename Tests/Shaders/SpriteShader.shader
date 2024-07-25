Shader "Custom/SpriteShader"
{
    Properties
    {
        _MainTex ("Sprite Atlas", 2D) = "white" {}
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent"}
        LOD 100

        CGPROGRAM
        #pragma surface surf Lambert alpha

        sampler2D _MainTex;
        float4 _SpriteUV; // xy: начальные UV, zw: размер кадра

        struct Input
        {
            float2 uv_MainTex;
        };

        void surf (Input IN, inout SurfaceOutput o)
        {
            float2 spriteUV = _SpriteUV.xy + frac(IN.uv_MainTex) * _SpriteUV.zw;
            fixed4 c = tex2D(_MainTex, spriteUV);
            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

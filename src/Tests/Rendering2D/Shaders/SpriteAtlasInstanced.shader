Shader "Custom/SpriteShaderCompatible"
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
            fixed4 _Color;
            float4 _TexCoord;
            float4 _Flip;

            v2f vert (appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                
                // Применяем флип к UV координатам
                float2 uv = IN.uv;
                uv.x = _Flip.x < 0 ? 1 - uv.x : uv.x;
                uv.y = _Flip.y < 0 ? 1 - uv.y : uv.y;
                
                OUT.uv = uv * abs(_TexCoord.zw) + _TexCoord.xy;
                OUT.color = _Color;
                return OUT;
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.uv) * IN.color;
                return c;
            }
            ENDCG
        }
    }
}

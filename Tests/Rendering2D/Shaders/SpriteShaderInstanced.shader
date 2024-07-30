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
            struct Transform
            {
                float3 Position;
                float4 Rotation;
                float3 Scale;
            };
            
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
            float4x4 QuaternionToMatrix(float4 quat)
            {
                float4x4 m = float4x4(float4(0, 0, 0, 0), float4(0, 0, 0, 0), float4(0, 0, 0, 0), float4(0, 0, 0, 0));

                const float x = quat.x, y = quat.y, z = quat.z, w = quat.w;
                const float x2 = x + x, y2 = y + y, z2 = z + z;
                const float xx = x * x2, xy = x * y2, xz = x * z2;
                const float yy = y * y2, yz = y * z2, zz = z * z2;
                const float wx = w * x2, wy = w * y2, wz = w * z2;

                m[0][0] = 1.0 - (yy + zz);
                m[0][1] = xy - wz;
                m[0][2] = xz + wy;

                m[1][0] = xy + wz;
                m[1][1] = 1.0 - (xx + zz);
                m[1][2] = yz - wx;

                m[2][0] = xz - wy;
                m[2][1] = yz + wx;
                m[2][2] = 1.0 - (xx + yy);

                m[3][3] = 1.0;
                
                return m;
            }

            float4x4 CalculateTRSMatrix(Transform transform, float flipX, float flipY)
            {
                float3 scale = float3(transform.Scale.x * (flipX > 0 ? -1 : 1),
                        transform.Scale.y * (flipY > 0 ? -1 : 1),
                        transform.Scale.z
                    );

                const float4x4 S = float4x4(
                    scale.x, 0, 0, 0,
                    0, scale.y, 0, 0,
                    0, 0, scale.z, 0,
                    0, 0, 0, 1
                );

                const float4x4 R = QuaternionToMatrix(transform.Rotation);

                const float4x4 T = float4x4(
                    1, 0, 0, transform.Position.x,
                    0, 1, 0, transform.Position.y,
                    0, 0, 1, transform.Position.z,
                    0, 0, 0, 1
                );

                return mul(T, mul(R, S));
            }
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            StructuredBuffer<Transform> _Transforms;
            StructuredBuffer<SpriteRenderData> _Properties;
            #endif

            void setup()
            {
                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                Transform transform = _Transforms[unity_InstanceID];
                SpriteRenderData data = _Properties[unity_InstanceID];
                unity_ObjectToWorld = CalculateTRSMatrix(transform, data.FlipX, data.FlipY);
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

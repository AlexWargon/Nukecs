Shader "Custom/SpriteShaderInstancedWithShadowURP"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _AlphaCutoff ("Alpha Cutoff", Range(0,1)) = 0.1
        _ShadowColor ("Shadow Color", Color) = (0, 0, 0, 0.5)
        _ShadowAngle ("Shadow Angle", Range(0, 360)) = 45
        _ShadowLength ("Shadow Length", Float) = 1
        _ShadowDistortion ("Shadow Distortion", Range(0, 1)) = 0.5
        _ShadowOffset ("Shadow Offset", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags 
        { 
            "Queue" = "Transparent" 
            "RenderType" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct Transform
        {
            float3 Position;
            float4 Rotation;
            float3 Scale;
        };
        
        struct SpriteRenderData
        {
            float4 Color;
            float4 SpriteTiling;
            float FlipX;
            float FlipY;
            float ShadowAngle;
            float ShadowLength;
            float ShadowDistortion;
            int Layer;
            float PixelsPerUnit;
            float2 SpriteSize;
            float2 Pivot;
            bool CanFlip;
        };

#if defined(UNITY_INSTANCING_ENABLED) || defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) || defined(UNITY_STEREO_INSTANCING_ENABLED)
        StructuredBuffer<Transform> _Transforms;
        StructuredBuffer<SpriteRenderData> _Properties;
    #endif

        float4x4 QuaternionToMatrix(float4 quat)
        {
            float4x4 m = float4x4(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

            float x = quat.x, y = quat.y, z = quat.z, w = quat.w;
            float x2 = x + x, y2 = y + y, z2 = z + z;
            float xx = x * x2, xy = x * y2, xz = x * z2;
            float yy = y * y2, yz = y * z2, zz = z * z2;
            float wx = w * x2, wy = w * y2, wz = w * z2;

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
            float3 scale = float3(
                transform.Scale.x * (flipX > 0 ? -1 : 1),
                transform.Scale.y * (flipY > 0 ? -1 : 1),
                transform.Scale.z
            );

            float4x4 S = float4x4(
                scale.x, 0, 0, 0,
                0, scale.y, 0, 0,
                0, 0, scale.z, 0,
                0, 0, 0, 1
            );

            float4x4 R = QuaternionToMatrix(transform.Rotation);

            float4x4 T = float4x4(
                1, 0, 0, transform.Position.x,
                0, 1, 0, transform.Position.y,
                0, 0, 1, transform.Position.z,
                0, 0, 0, 1
            );

            return mul(T, mul(R, S));
        }

        void SetupInstancing()
        {

#if defined(UNITY_INSTANCING_ENABLED) || defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) || defined(UNITY_STEREO_INSTANCING_ENABLED)
            Transform transform = _Transforms[unity_InstanceID];
            SpriteRenderData data = _Properties[unity_InstanceID];
            unity_ObjectToWorld = CalculateTRSMatrix(transform, data.FlipX, data.FlipY);
            unity_WorldToObject = Inverse(unity_ObjectToWorld);
#endif
        }
        ENDHLSL

        // Shadow Pass
//        Pass
//        {
//            Name "ShadowPass"
//            Tags { "LightMode" = "UniversalForward" }
//
//            ZWrite Off
//            Blend SrcAlpha OneMinusSrcAlpha
//
//            HLSLPROGRAM
//            #pragma vertex vert
//            #pragma fragment frag
//            #pragma multi_compile_instancing
//            #pragma instancing_options procedural:SetupInstancing
//            #pragma target 4.5
//
//            struct Attributes
//            {
//                float4 positionOS : POSITION;
//                float2 uv : TEXCOORD0;
//                UNITY_VERTEX_INPUT_INSTANCE_ID
//            };
//
//            struct Varyings
//            {
//                float4 positionCS : SV_POSITION;
//                float2 uv : TEXCOORD0;
//                float4 color : COLOR;
//                UNITY_VERTEX_INPUT_INSTANCE_ID
//            };
//
//            TEXTURE2D(_MainTex);
//            SAMPLER(sampler_MainTex);
//            float4 _ShadowColor;
//            float _ShadowAngle;
//            float _ShadowLength;
//            float _ShadowDistortion;
//            float4 _ShadowOffset;
//
//            Varyings vert(Attributes input)
//            {
//                Varyings output;
//                UNITY_SETUP_INSTANCE_ID(input);
//                UNITY_TRANSFER_INSTANCE_ID(input, output);
//
//                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
//
//#if defined(UNITY_INSTANCING_ENABLED) || defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) || defined(UNITY_STEREO_INSTANCING_ENABLED)
//                SpriteRenderData data = _Properties[unity_InstanceID];
//                float4 localPosition = input.positionOS;
//                localPosition.xy *= data.SpriteSize;
//                localPosition.xy -= data.Pivot * data.SpriteSize;
//                localPosition.xy /= data.PixelsPerUnit;
//                worldPos = mul(unity_ObjectToWorld, localPosition).xyz;
//
//                float shadowAngle = radians(data.ShadowAngle);
//                float shadowLength = data.ShadowLength;
//                float shadowDistortion = data.ShadowDistortion;
//#else
//                float shadowAngle = radians(_ShadowAngle);
//                float shadowLength = _ShadowLength;
//                float shadowDistortion = _ShadowDistortion;
//#endif
//
//                bool isBottomVertex = input.positionOS.y < 0.01;
//
//                if (!isBottomVertex)
//                {
//                    float2 shadowOffset = float2(cos(shadowAngle), sin(shadowAngle)) * shadowLength;
//                    worldPos.xy += shadowOffset;
//                    float verticalOffset = (1 - input.positionOS.y) * shadowDistortion;
//                    worldPos.y -= verticalOffset;
//                }
//                worldPos.xy += _ShadowOffset.xy;
//
//                output.positionCS = TransformWorldToHClip(worldPos);
//                
//#if defined(UNITY_INSTANCING_ENABLED) || defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) || defined(UNITY_STEREO_INSTANCING_ENABLED)
//                float2 uv = input.uv;
//                uv.x = data.FlipX < 0 ? 1 - uv.x : uv.x;
//                uv.y = data.FlipY < 0 ? 1 - uv.y : uv.y;
//                output.uv = uv * abs(data.SpriteTiling.zw) + data.SpriteTiling.xy;
//#else
//                output.uv = input.uv;
//#endif
//
//                output.color = _ShadowColor;
//                return output;
//            }
//
//            float4 frag(Varyings input) : SV_Target
//            {
//                UNITY_SETUP_INSTANCE_ID(input);
//                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
//                float4 finalColor = texColor;
//                finalColor.rgb = _ShadowColor.rgb;
//                finalColor.a *= _ShadowColor.a;
//                return finalColor;
//            }
//            ENDHLSL
//        }

        // Sprite Pass
        Pass
        {
            Name "SpritePass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:SetupInstancing
            #pragma target 4.5

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _AlphaCutoff;

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

#if defined(UNITY_INSTANCING_ENABLED) || defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) || defined(UNITY_STEREO_INSTANCING_ENABLED)
                SpriteRenderData data = _Properties[unity_InstanceID];
                float4 localPosition = input.positionOS;
                localPosition.xy *= data.SpriteSize;
                localPosition.xy -= data.Pivot * data.SpriteSize;
                localPosition.xy /= data.PixelsPerUnit;
                float3 worldPosition = mul(unity_ObjectToWorld, localPosition).xyz;
                worldPosition.z -= data.Layer * 0.0001;
                output.positionCS = TransformWorldToHClip(worldPosition);

                float2 uv = input.uv;
                uv.x = data.FlipX < 0 ? 1 - uv.x : uv.x;
                uv.y = data.FlipY < 0 ? 1 - uv.y : uv.y;
                output.uv = uv * abs(data.SpriteTiling.zw) + data.SpriteTiling.xy;
                output.color = data.Color;
#else
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = float4(1, 1, 1, 1);
#endif

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float4 finalColor = texColor * input.color;
                clip(finalColor.a - _AlphaCutoff);
                return finalColor;
            }
            ENDHLSL
        }
    }
}
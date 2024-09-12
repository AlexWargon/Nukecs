using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs.Tests {
    [StructLayout(LayoutKind.Sequential)]
    public struct SpriteRenderData : IComponent {
        public float4 Color;
        public float4 SpriteTiling;
        public float FlipX; // Changed from bool to float
        public float FlipY; // Changed from bool to float
        public float ShadowAngle;
        public float ShadowLength;
        public float ShadowDistortion;
        public int Layer;
        public float PixelsPerUnit;
        public float2 SpriteSize;
        public float2 Pivot;
    }

    public struct Sprite : IComponent
    {
        public float4 Color;
        public float4 UV;
        public int Layer;
        public int FlipX;
        public int FlipY;
    }

    public struct SpriteShadow : IComponent
    {
        public float ShadowAngle;
        public float ShadowLenght;
        public float ShadowDistortion;
    }
    [BurstCompile]
    public static class GraphicsHelp {
        public static float4 ColorToFloat4(Color c) {
            return new float4(c.a, c.g, c.b, c.a);
        }
    }
}
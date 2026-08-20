using Unity.Burst;
using Unity.Mathematics;

namespace Wargon.Nukecs.Demos.CubeSculpture
{
    [BurstCompile]
    public static class SculptureTemplate
    {
        public static float3 GetPosition(int index, int count, float scale, uint seed = 42)
        {
            var torusCount = (int)(count * 0.35f);
            var helixCWCount = (int)(count * 0.15f);
            var helixCCWCount = (int)(count * 0.15f);
            var crownCount = (int)(count * 0.15f);
            var pedestalTotal = (int)(count * 0.1f);
            var perLayer = pedestalTotal > 0 ? pedestalTotal / 3 : 1;

            var b1 = torusCount;
            var b2 = b1 + helixCWCount;
            var b3 = b2 + helixCCWCount;
            var b4 = b3 + crownCount;
            var b5 = b4 + pedestalTotal;

            if (index < b1) return TorusPos(index, torusCount, scale, seed);
            if (index < b2) return HelixPos(index - b1, helixCWCount, scale, true);
            if (index < b3) return HelixPos(index - b2, helixCCWCount, scale, false);
            if (index < b4) return CrownPos(index - b3, crownCount, scale, seed);
            if (index < b5) return PedestalPos(index - b4, perLayer, scale);
            return RandomPos(index, seed, scale);
        }

        static float3 TorusPos(int i, int count, float scale, uint seed)
        {
            var rng = new Random(seed + (uint)i);
            var majorRadius = 8f * scale;
            var angle = (float)i / count * math.PI * 2f;
            return new float3(
                math.cos(angle) * majorRadius,
                rng.NextFloat(-0.5f, 0.5f) * scale,
                math.sin(angle) * majorRadius
            );
        }

        static float3 HelixPos(int i, int count, float scale, bool clockwise)
        {
            var majorRadius = 8f * scale;
            var helixHeight = 12f * scale;
            var dir = clockwise ? 1f : -1f;
            var t = (float)i / count;
            var angle = t * math.PI * 6f * dir;
            return new float3(
                math.cos(angle) * majorRadius,
                t * helixHeight - helixHeight * 0.3f,
                math.sin(angle) * majorRadius
            );
        }

        static float3 CrownPos(int i, int count, float scale, uint seed)
        {
            var rng = new Random(seed + (uint)i);
            var topY = 10f * scale;
            var angle = (float)i / count * math.PI * 2f;
            var dist = rng.NextFloat(1f, 4f) * scale;
            return new float3(
                math.cos(angle) * dist,
                topY + rng.NextFloat(0f, 3f) * scale,
                math.sin(angle) * dist
            );
        }

        static float3 PedestalPos(int pedestalIndex, int perLayer, float scale)
        {
            var layer = pedestalIndex / perLayer;
            var i = pedestalIndex - layer * perLayer;
            var y = (-4f - layer * 1.5f) * scale;
            var radius = (3f + layer * 2f) * scale;
            var angle = (float)i / perLayer * math.PI * 2f;
            return new float3(math.cos(angle) * radius, y, math.sin(angle) * radius);
        }

        static float3 RandomPos(int index, uint seed, float scale)
        {
            var rng = new Random(seed + (uint)index);
            return new float3(
                rng.NextFloat(-12f, 12f) * scale,
                rng.NextFloat(-6f, 14f) * scale,
                rng.NextFloat(-12f, 12f) * scale
            );
        }


        public static float3 GetPosition(int index, int count, float scale, int shapeIndex, uint seed = 42)
        {
            switch (shapeIndex % 4)
            {
                default: return GetPosition(index, count, scale, seed);
                case 1: return FibonacciSpherePos(index, count, scale, seed);
                case 2: return DoubleHelixPos(index, count, scale);
                case 3: return CubeFramePos(index, count, scale, seed);
            }
        }

        static float3 FibonacciSpherePos(int i, int count, float scale, uint seed)
        {
            var phi = math.acos(1f - 2f * (i + 0.5f) / count);
            var theta = math.PI * (1f + math.sqrt(5f)) * i;
            var radius = 8f * scale;
            return new float3(
                radius * math.sin(phi) * math.cos(theta),
                radius * math.cos(phi),
                radius * math.sin(phi) * math.sin(theta)
            );
        }

        static float3 DoubleHelixPos(int i, int count, float scale)
        {
            var height = 16f * scale;
            var radius = 6f * scale;
            var t = (float)i / count;
            var strand = i % 2;
            var angleOffset = strand * math.PI;
            var angle = t * math.PI * 8f + angleOffset;
            return new float3(
                math.cos(angle) * radius,
                t * height - height * 0.5f,
                math.sin(angle) * radius
            );
        }

        static float3 CubeFramePos(int i, int count, float scale, uint seed)
        {
            var size = 10f * scale;
            var half = size * 0.5f;
            var edgeIndex = i % 12;
            var t = (float)(i / 12) / (count / 12f);
            var edgeT = (i / 12f) - (int)(i / 12f);

            float3 a, b;
            switch (edgeIndex)
            {
                case 0:  a = new float3(-half, -half, -half); b = new float3( half, -half, -half); break;
                case 1:  a = new float3( half, -half, -half); b = new float3( half, -half,  half); break;
                case 2:  a = new float3( half, -half,  half); b = new float3(-half, -half,  half); break;
                case 3:  a = new float3(-half, -half,  half); b = new float3(-half, -half, -half); break;
                case 4:  a = new float3(-half,  half, -half); b = new float3( half,  half, -half); break;
                case 5:  a = new float3( half,  half, -half); b = new float3( half,  half,  half); break;
                case 6:  a = new float3( half,  half,  half); b = new float3(-half,  half,  half); break;
                case 7:  a = new float3(-half,  half,  half); b = new float3(-half,  half, -half); break;
                case 8:  a = new float3(-half, -half, -half); b = new float3(-half,  half, -half); break;
                case 9:  a = new float3( half, -half, -half); b = new float3( half,  half, -half); break;
                case 10: a = new float3( half, -half,  half); b = new float3( half,  half,  half); break;
                default: a = new float3(-half, -half,  half); b = new float3(-half,  half,  half); break;
            }
            return math.lerp(a, b, edgeT);
        }
    }
}

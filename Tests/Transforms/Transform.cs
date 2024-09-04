

namespace Wargon.Nukecs.Transforms {
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Unity.Mathematics;
    [Serializable][StructLayout(LayoutKind.Sequential)]
    public struct Transform : IComponent {
        public float3 Position;
        public quaternion Rotation;
        public float3 Scale;

        public Transform(float3 pos) {
            Position = pos;
            Rotation = quaternion.identity;
            Scale = new float3(1, 1, 1);
        }
        public float4x4 Matrix
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => float4x4.TRS(Position, Rotation, Scale);
        }
    }

    public struct OnAddChildWithTransformEvent : IComponent { }

    public static class TransformsUtility {
        public static void Convert(UnityEngine.Transform transform, ref World world, ref Entity entity) {
            entity.Add(new Transform {
                Position = transform.position,
                Rotation = transform.rotation,
                Scale = transform.lossyScale
            });
            entity.Add(new LocalTransform {
                Position = transform.localPosition,
                Rotation = transform.localRotation,
                Scale = transform.localScale
            });
        }
    }
}
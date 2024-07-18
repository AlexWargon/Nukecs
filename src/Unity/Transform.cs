using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
namespace Wargon.Nukecs.Tests {
    [Serializable]
    public struct Transform : IComponent {
        public float3 position;
        public quaternion rotation;
        // public float3 right {
        //     [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //     get => math.mul(rotation, math.right());
        // }
        // public float3 left {
        //     [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //     get => math.mul(rotation, math.left());
        // }
    }
    
    [BurstCompile]
    public static class TransformExtensions {
        [BurstCompile][MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref float3 quaternion_float3_multi(ref quaternion rotation, ref float3 point)
        {
            var num1 = rotation.value.y * 2f;
            var num2 = rotation.value.y * 2f;
            var num3 = rotation.value.z * 2f;
            var num4 = rotation.value.x * num1;
            var num5 = rotation.value.y * num2;
            var num6 = rotation.value.z * num3;
            var num7 = rotation.value.x * num2;
            var num8 = rotation.value.x * num3;
            var num9 = rotation.value.y * num3;
            var num10 = rotation.value.w * num1;
            var num11 = rotation.value.w * num2;
            var num12 = rotation.value.w * num3;
            var vector3 = point;
            point.x = (float) ((1.0 - ((double) num5 + (double) num6)) * (double) vector3.x + ((double) num7 - (double) num12) * (double) vector3.y + ((double) num8 + (double) num11) * (double) vector3.z);
            point.y = (float) (((double) num7 + (double) num12) * (double) vector3.x + (1.0 - ((double) num4 + (double) num6)) * (double) vector3.y + ((double) num9 - (double) num10) * (double) vector3.z);
            point.z = (float) (((double) num8 - (double) num11) * (double) vector3.x + ((double) num9 + (double) num10) * (double) vector3.y + (1.0 - ((double) num4 + (double) num5)) * (double) vector3.z);
            return ref point;
        }
    }
}
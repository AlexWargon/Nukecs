using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using Transform = Wargon.Nukecs.Transforms.Transform;

namespace Wargon.Nukecs.Collision2D
{
    using static math;

    public struct Rectangle2D : IComponent
    {
        public int index;
        public float w;
        public float h;
        [MarshalAs(UnmanagedType.U1)] public bool trigger;
        public CollisionLayer layer;
        public CollisionLayer collideWith;

        public float3 Position(in Transform transform)
        {
            var centerPosition = transform.Position;
            // Преобразуем float2 в float3 для вращения (z = 0)
            var localCorner = new float3(-w * 0.5f, -h * 0.5f, 0f);
            var rotatedCorner = mul(transform.Rotation, localCorner);
            centerPosition.x += rotatedCorner.x;
            centerPosition.y += rotatedCorner.y;
            return centerPosition;
        }

        public readonly void GetVertices(in Transform transform,
            out float2 v0, out float2 v1, out float2 v2, out float2 v3)
        {
            var center = new float2(transform.Position.x, transform.Position.y);

            var localV0 = new float3(-w * 0.5f, -h * 0.5f, 0f); // left down
            var localV1 = new float3(w * 0.5f, -h * 0.5f, 0f); // right down
            var localV2 = new float3(w * 0.5f, h * 0.5f, 0f); // right up
            var localV3 = new float3(-w * 0.5f, h * 0.5f, 0f); // left up

            v0 = center + mul(transform.Rotation, localV0).xy;
            v1 = center + mul(transform.Rotation, localV1).xy;
            v2 = center + mul(transform.Rotation, localV2).xy;
            v3 = center + mul(transform.Rotation, localV3).xy;
        }

        public void GetVerticesVectors(in Transform transform,
            out Vector3 v0, out Vector3 v1, out Vector3 v2, out Vector3 v3)
        {
            var center = new float3(transform.Position.x, transform.Position.y, 0f);

            var localV0 = new float3(-w * 0.5f, -h * 0.5f, 0f); // left down
            var localV1 = new float3(w * 0.5f, -h * 0.5f, 0f); // right down
            var localV2 = new float3(w * 0.5f, h * 0.5f, 0f); // right up
            var localV3 = new float3(-w * 0.5f, h * 0.5f, 0f); // left up

            v0 = center + mul(transform.Rotation, localV0);
            v1 = center + mul(transform.Rotation, localV1);
            v2 = center + mul(transform.Rotation, localV2);
            v3 = center + mul(transform.Rotation, localV3);
        }
    }
}
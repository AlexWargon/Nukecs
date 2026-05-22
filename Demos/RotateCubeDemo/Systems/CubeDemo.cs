using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using Wargon.Nukecs;
using Transform = Wargon.Nukecs.Transforms.Transform;

namespace Wargon.Nukecs.Demos.RotateCube
{
    public static class CubeDemo
    {
        [System, BurstCompile]
        public static unsafe void RotateCubeSystem(
            ref Query<Transform, RotationSpeed, With<Cube>> query, 
            ref State state)
        {
            var dt = state.Time.DeltaTime;
            foreach (var (transform, speed) in query.iter_unsafe())
            {
                transform->Rotation = math.mul(
                    transform->Rotation,
                    quaternion.AxisAngle(math.up(), 
                        speed->RadiansPerSecond * dt)
                );
            }
        }
    }
}

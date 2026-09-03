using Unity.Burst;
using Unity.Mathematics;
using Wargon.Nukecs.Transforms;
using Transform = Wargon.Nukecs.Transforms.Transform;

namespace Wargon.Nukecs.Demos.HotReload
{
    public class CubeDemo
    {
        [System, BurstCompile]
        public static unsafe void RotateCubeSystem(
            ref Query<Transform, RotationSpeed, With<Cube>> query, 
            ref State state)
        {
            var dt = state.Time.DeltaTime;
            foreach(var (transform, speed) 
                     in query.iter_unsafe())
            {
                transform->Rotation = math.mul(
                    transform->Rotation,
                    quaternion.AxisAngle(math.up(), 
                        -speed->RadiansPerSecond * dt)
                );
            }
        }
    }
}

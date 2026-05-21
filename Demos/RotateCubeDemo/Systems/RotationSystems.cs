using Unity.Burst;
using Unity.Mathematics;
using Wargon.Nukecs;
using Wargon.Nukecs.Transforms;

namespace Wargon.Nukecs.Demos.RotateCube
{
    public static class RotationSystems
    {
        [System, BurstCompile]
        public static void RotateCube(ref Query<Transform, RotationSpeed> query, 
            ref State state)
        {
            var dt = state.Time.DeltaTime;
            foreach (var (transformRef, speedRef) in query)
            {
                ref var t = ref transformRef.Get;
                ref var speed = ref speedRef.Get;
                t.Rotation = math.mul(
                    t.Rotation,
                    quaternion.AxisAngle(math.up(), 
                        speed.RadiansPerSecond * dt)
                );
            }
        }
    }
}

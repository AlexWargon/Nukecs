using System.Runtime.InteropServices;
using Unity.Mathematics;
using Random = UnityEngine.Random;
using Transform = Wargon.Nukecs.Transforms.Transform;

namespace Wargon.Nukecs
{
    public class TestWorldLink : WorldLink
    {
        public override void Bake(ref World world)
        {
            // var e = world.Entity();
            // e.Add(new Transform
            // {
            //     Position= new float3(1488, 1488, 1488),
            //     Rotation = quaternion.RotateY(range(0, 360f)),
            //     Scale = new float3(777, 777, 777)
            // });
            for (int i = 0; i < 1000; i++)
            {
                var scale = range(1f, 2f);
                var e = world.Entity();
                e.Add(new Transform
                {
                    Position= new float3(range(-55f, 55f), 0, range(-55f, 55f)),
                    Rotation = quaternion.RotateY(range(0, 360f)),
                    Scale = new float3(scale, scale, scale)
                });
            }
        }
        private float range(float a, float b) => Random.Range(a, b);
        protected override void OnUpdate()
        {
            
        }
    }
    
}
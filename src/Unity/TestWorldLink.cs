using Unity.Mathematics;
using Wargon.Nukecs.Transforms;
using Random = UnityEngine.Random;

namespace Wargon.Nukecs {
    public class TestWorldLink : WorldLink, IOnCreate, IOnUpdate {
        public void OnCreate(ref World world) { }

        public void OnUpdate(ref State state) { }

        public override void Bake(ref World world) {
            var e = world.Entity();
            e.Add(new Transform {
                Position = new float3(1488, 1488, 1488),
                Rotation = quaternion.RotateY(range(0, 360f)),
                Scale = new float3(777, 777, 777)
            });
            for (var i = 0; i < 1000; i++) {
                var scale = range(1f, 2f);
                e = world.Entity();
                e.Add(new Transform {
                    Position = new float3(range(-55f, 55f), 0, range(-55f, 55f)),
                    Rotation = quaternion.RotateY(range(0, 360f)),
                    Scale = new float3(scale, scale, scale)
                });
            }
        }

        protected override void AddSystems(Systems systems) { }

        private float range(float a, float b) {
            return Random.Range(a, b);
        }
    }
}
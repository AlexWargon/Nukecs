using Unity.Mathematics;
using UnityEngine;
using Wargon.Nukecs.Transforms;
using Transform = Wargon.Nukecs.Transforms.Transform;

namespace Wargon.Nukecs.Demos.HotReload
{
    public class RotateCubeBootstrap : MonoBehaviour
    {
        [SerializeField] private float rotationSpeed = 90f;
        [SerializeField] private HotReloadSystems hotReload;

        private World world;

        private void Awake()
        {
            World.DisposeStatic();
            world = World.Create(WorldConfig.Default256);

            hotReload = new HotReloadSystems(ref world);
            hotReload.Systems.Add(CubeDemo.RotateCubeSystem, Threads.Main); 
            hotReload.Systems.Add(SyncTransformsSystem, Threads.Main);
            hotReload.StartTracking();

            CreateCube(ref world);
        }

        private void CreateCube(ref World world)
        {
            var cubeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeGo.name = "RotatingCube";

            ref var entity = ref world.Entity();
            entity.Add(new Transform
            {
                Position = float3.zero,
                Rotation = quaternion.identity,
                Scale = new float3(1f, 1f, 1f)
            });
            entity.Add(new RotationSpeed
            {
                RadiansPerSecond = math.radians(rotationSpeed)
            });
            entity.Add(new TransformRef { Value = cubeGo.transform });
            entity.Add<Cube>();
        }

        private void Update()
        {
            if (hotReload != null)
                hotReload.OnUpdate(Time.deltaTime, Time.time);
        }

        private void OnDestroy()
        {
            hotReload?.Dispose();
            if (world.IsAlive)
                world.Dispose();
        }
        [System]
        public static void SyncTransformsSystem(ref Query<Transform, TransformRef, None<NoneSyncTransform>> query)
        {
            foreach (var (tRef,tRefRef) in query)
            {
                var transformRef = tRefRef.Get.Value.Value;
                ref var transform = ref tRef.Get;

                transformRef.position = transform.Position;
                transformRef.rotation = transform.Rotation;
                transformRef.localScale = transform.Scale;
            }
        }
    }
    
}

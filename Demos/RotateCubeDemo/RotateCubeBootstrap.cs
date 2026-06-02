using Unity.Mathematics;
using UnityEngine;
using Wargon.Nukecs.HotReload;
using Wargon.Nukecs.Transforms;
using Random = UnityEngine.Random;
using Transform = Wargon.Nukecs.Transforms.Transform;

namespace Wargon.Nukecs.Demos.HotReload
{
    public class RotateCubeBootstrap : MonoBehaviour
    {
        [SerializeField] private float rotationSpeed = 90f;
        [SerializeField] private Systems systems;

        private World world;

        private void Awake()
        {
            World.DisposeStatic();
            world = World.Create(WorldConfig.Default256);

            systems = new Systems(ref world)
            .Add(CubeDemo.RotateCubeSystem, Threads.Main)
            .AddGroup(new TransformsGroup())
            .AddHotReload();
            for (int i = 0; i < 100; i++)
            {
                CreateCube(ref world, new float3(Random.Range(-10,10),Random.Range(-10,10),Random.Range(-10,10)));
            }
        }

        private void CreateCube(ref World world, float3 pos)
        {
            var cubeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            

            ref var entity = ref world.Entity();
            cubeGo.name = $"RotatingCube_{entity.id}";
            entity.Add(new Name(cubeGo.name));
            entity.Add(new Transform
            {
                Position = pos,
                Rotation = quaternion.identity,
                Scale = new float3(1f, 1f, 1f)
            });
            entity.Add(new RotationSpeed
            {
                RadiansPerSecond = math.radians(rotationSpeed)
            });
            entity.Add(new TransformRef { Value = cubeGo.transform });
            entity.Add<Cube>();
            entity.Add(new GameObjectView(){val = cubeGo});
            
        }

        private void Update()
        {
            if (systems != null)
                systems.OnUpdate(Time.deltaTime, Time.time);
        }

        private void OnDestroy()
        {
            if (world.IsAlive)
                world.Dispose();
        }
        // [System]
        // public static void SyncTransformsSystem(ref Query<Transform, TransformRef, None<NoneSyncTransform>> query)
        // {
        //     foreach (var (tRef,tRefRef) in query)
        //     {
        //         var transformRef = tRefRef.Get.Value.Value;
        //         ref var transform = ref tRef.Get;
        //         transformRef.position = transform.Position;
        //         transformRef.rotation = transform.Rotation;
        //         transformRef.localScale = transform.Scale;
        //     }
        // }
    }
    
}

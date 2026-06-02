using UnityEngine;
using Wargon.Nukecs.HotReload;
using static Wargon.Nukecs.Demos.Boids.BoidsDemo;

namespace Wargon.Nukecs.Demos.Boids
{
    public class BoidsBootstrap : MonoBehaviour
    {
        [SerializeField] int boidCount = 500;
        [SerializeField] Mesh boidMesh;
        [SerializeField] Material boidMaterial;

        private Systems systems;
        private World world;
        private BoidRenderData renderData;

        private void Awake()
        {
            World.DisposeStatic();
            world = World.Create(WorldConfig.Default1024);

            renderData = new BoidRenderData();
            renderData.Allocate(boidCount);
            world.AddRes(new BoidCount(){Value = boidCount});
            world.AddRes(renderData);
            world.AddResManaged(new MeshData()
            {
                Mesh = boidMesh,
                Material = boidMaterial
            });
            systems = new Systems(ref world)
                .AddSystems(
                    SystemPath.Update,
                    (SpawnBoids, Threads.MainRun),
                    (BoidsCalculateForces, Threads.MainRun),
                    BoidsApplyMovement,
                    (DrawBoids, Threads.Main))
            .AddHotReload();
        }

        private void Update()
        {
            if (systems != null)
                systems.OnUpdate(Time.deltaTime, Time.time);
        }

        private void OnDestroy()
        {
            renderData.Dispose();
            if (world.IsAlive) world.Dispose();
        }
    }
}

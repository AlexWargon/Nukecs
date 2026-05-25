using UnityEngine;
using Wargon.Nukecs.HotReload;

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
            systems = new Systems(ref world);
            systems.Add(BoidsDemo.SpawnBoids, Threads.MainRun);
            systems.Add(BoidsDemo.BoidsCalculateForces, Threads.MainRun);
            systems.Add(BoidsDemo.BoidsApplyMovement, Threads.Parallel);
            systems.Add(BoidsDemo.DrawBoids, Threads.Main);
            systems.AddHotReload();
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

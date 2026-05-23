using UnityEngine;

namespace Wargon.Nukecs.Demos.Boids
{
    public class BoidsBootstrap : MonoBehaviour
    {
        [SerializeField] int boidCount = 500;
        [SerializeField] Mesh boidMesh;
        [SerializeField] Material boidMaterial;

        private HotReloadSystems hotReload;
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
            hotReload = new HotReloadSystems(ref world);
            hotReload.Systems.Add(BoidsDemo.SpawnBoids, Threads.MainRun);
            hotReload.Systems.Add(BoidsDemo.BoidsUpdate, Threads.MainRun);
            hotReload.Systems.Add(BoidsDemo.DrawBoids, Threads.Main);
            hotReload.StartTracking();
        }

        private void Update()
        {
            if (hotReload != null)
                hotReload.OnUpdate(Time.deltaTime, Time.time);
        }

        private void OnDestroy()
        {
            hotReload?.Dispose();
            renderData.Dispose();
            if (world.IsAlive) world.Dispose();
        }
    }
}

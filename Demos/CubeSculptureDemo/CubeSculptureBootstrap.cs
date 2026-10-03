using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Wargon.Nukecs.Transforms;

namespace Wargon.Nukecs.Demos.CubeSculpture
{
    public class CubeSculptureBootstrap : WorldInstaller
    {
        [Header("Demo Configuration")] 
        [SerializeField] float spawnDelay = 0.1f;
        [SerializeField] int targetCount = 5000;
        [SerializeField] float cubeScale = 0.3f;
        [SerializeField] int spawnBatchSize = 500;
        [Header("Rendering")]
        [SerializeField] Mesh cubeMesh;
        [SerializeField] Material cubeMaterial;

        public int TargetCount => targetCount;
        public float CubeScale => cubeScale;
        public Mesh CubeMesh => cubeMesh;
        public Material CubeMaterial => cubeMaterial;

        public static CubeSculptureBootstrap Instance;

        protected override WorldConfig GetConfig() => WorldConfig.Default163840;

        protected override void OnWorldCreated(ref World world)
        {
            Instance = this;
            world.AddRes(new ConfigData
            {
                TargetCount = targetCount,
                CubeScale = cubeScale,
                SpawnBatchSize = spawnBatchSize,
                spawnTime = spawnDelay
            });
            world.AddRes(new SculptureData());
            world.AddRes(new CycleData { AssembledDuration = 3f });
            Systems.UseDependencyGraph(mode: GroupScheduleMode.FlattenedSchedule2);
            Systems.AddGroup(new CubeSculpture());

        }

        protected override void CreateEntities(ref World world) { }

        void Update()
        {
            Systems.OnUpdate(Time.deltaTime, Time.time);
        }

        protected override void OnDestroy()
        {
            Instance = null;
            base.OnDestroy();
        }
    }
}

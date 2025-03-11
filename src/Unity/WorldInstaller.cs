using TriInspector;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using Wargon.Nukecs.Tests;
using Wargon.Nukecs.Transforms;
using Transform = Wargon.Nukecs.Transforms.Transform;

namespace Wargon.Nukecs
{
    public class WorldInstaller : MonoBehaviour
    {
        [ReadOnly][SerializeField] private int WorldId;
        protected World World;
        protected Systems Systems;
        protected WorldConfig Config = WorldConfig.Default16384;

        private unsafe void Awake()
        {
            World = World.Create(Config);
            Systems = new Systems(ref World);
            Systems.AddDefaults().Add<RotateCubeSystem>().Add<SyncTransformsSystem>();;
            OnWorldCreated(ref World);
            for (var i = 0; i < World.UnsafeWorld->archetypesList.Length; i++)
            {
                ref var archetype = ref World.UnsafeWorld->archetypesList[i];
                archetype.Ptr->Refresh();
            }
            ConvertEntities();
            World.Update();
            WorldId = World.UnsafeWorld->Id;
        }

        protected virtual void OnWorldCreated(ref World world)
        {
            
        }
        
        protected virtual void ConvertEntities()
        {
            var children = transform.GetComponentsInChildren<EntityLink>();
            for (int i = 0; i < transform.childCount; i++)
            {
                var e = World.Entity();
                children[i].Convert(ref World, ref e);
            }
        }
        
        private void Update()
        {
            Systems.OnUpdate(Time.deltaTime, Time.time);
        }
        private void OnDestroy()
        {
            World.Dispose();
        }
    }
    
    [BurstCompile]
    public struct RotateCubeSystem : IEntityJobSystem
    {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world)
        {
            return world.Query().With<Transforms.Transform>().With<TransformRef>().With<Cube>();
        }
        public void OnUpdate(ref Entity entity, ref State state)
        {
            ref var transform = ref entity.Get<Transforms.Transform>();
            float angle = math.radians(30f * state.Time.DeltaTime);
            transform.Rotation = math.mul(transform.Rotation, quaternion.AxisAngle(math.up(), angle));
        }
    }
    public struct SyncTransformsSystem : ISystem, IOnCreate
    {
        private Query query;

        public void OnCreate(ref World world)
        {
            query = world.Query().With<Transforms.Transform>().With<TransformRef>();
        }
        public void OnUpdate(ref State state)
        {
            foreach (ref var entity in query)
            {
                var transformRef = entity.Get<TransformRef>().Value.Value;
                ref var transform = ref entity.Get<Transforms.Transform>();

                transformRef.position = transform.Position;
                transformRef.rotation = transform.Rotation;
                transformRef.localScale = transform.Scale;
            }
        }
    }
    public struct Cube : IComponent { }
}

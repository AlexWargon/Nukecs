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
        [ReadOnly][SerializeField] public int WorldId;
        protected World world;
        public ref World World => ref world;
        protected Systems Systems;
        protected virtual WorldConfig GetConfig() => WorldConfig.Default16384;

        private unsafe void Awake()
        {
            world = World.Create(GetConfig());
            WorldId = world.Id;
            Systems = new Systems(ref world);
            Systems.AddDefaults();
            OnWorldCreated(ref world);
            for (var i = 0; i < world.UnsafeWorld->archetypesList.Length; i++)
            {
                ref var archetype = ref world.UnsafeWorld->archetypesList[i];
                archetype.Ptr->Refresh();
            }
            //ConvertEntities();
            CreateEntities(ref world);
            world.Update();
            WorldId = world.UnsafeWorld->Id;
            
        }

        protected virtual void OnWorldCreated(ref World world)
        {
            
        }

        protected virtual void CreateEntities(ref World world)
        {
            
        }
        // protected virtual void ConvertEntities()
        // {
        //     var children = transform.GetComponentsInChildren<EntityLink>();
        //     for (int i = 0; i < transform.childCount; i++)
        //     {
        //         var e = world.Entity();
        //         children[i].Convert(ref world, ref e);
        //     }
        // }

        protected virtual void OnDestroy()
        {
            world.Dispose();
        }
    }
    
    [BurstCompile]
    public struct RotateCubeSystem : IEntityJobSystem
    {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world)
        {
            return world.Query().With<Transform>().With<TransformRef>().With<Cube>();
        }
        public void OnUpdate(ref Entity entity, ref State state)
        {
            ref var transform = ref entity.Get<Transform>();
            float angle = math.radians(30f * state.Time.DeltaTime);
            transform.Rotation = math.mul(transform.Rotation, quaternion.AxisAngle(math.up(), angle));
        }
    }
    public struct SyncTransformsSystem : ISystem, IOnCreate
    {
        private Query _query;

        public void OnCreate(ref World world)
        {
            _query = world.Query().With<Transform>().With<TransformRef>().None<NoneSyncTransform>();
        }
        public void OnUpdate(ref State state)
        {
            foreach (ref var entity in _query)
            {
                var transformRef = entity.Get<TransformRef>().Value.Value;
                ref var transform = ref entity.Get<Transform>();

                transformRef.position = transform.Position;
                transformRef.rotation = transform.Rotation;
                transformRef.localScale = transform.Scale;
            }
        }
    }
    public struct Cube : IComponent { }
}

using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Transform = Wargon.Nukecs.Transforms.Transform;

namespace Wargon.Nukecs.Collision2D {
    [BurstCompile]
    public struct AddCollision2DDataSystem : IEntityJobSystem
    {
        public SystemMode Mode => SystemMode.Single;
        public Query GetQuery(ref World world)
        {
            return world.Query().None<ComponentArray<Collision2DData>>().With<Circle2D>();
        }

        public void OnUpdate(ref Entity entity, ref State state)
        {
            entity.AddArray<Collision2DData>();
        }
    }
    [BurstCompile]
    public struct AddCollision2DDataRectSystem : IEntityJobSystem
    {
        public SystemMode Mode => SystemMode.Single;
        public Query GetQuery(ref World world)
        {
            return world.Query().None<ComponentArray<Collision2DData>>().With<Rectangle2D>();
        }

        public void OnUpdate(ref Entity entity, ref State state)
        {
            entity.AddArray<Collision2DData>();
        }
    }
    public struct Collision2DData : IArrayComponent {
        public Entity Other;
        public float2 Position;
        public float2 Normal;
        public HitInfo.CollisionType Type;
    }

    public struct CollidedFlag : IComponent {}

    public struct EntityReference : IComponent
    {
        public Entity Value;
    }

    [BurstCompile]
    public struct SynchroniseTransformsSystem : IEntityJobSystem
    {
        public SystemMode Mode => SystemMode.Single;
        public Query GetQuery(ref World world)
        {
            return world.Query().With<Transform>().With<EntityReference>().With<CollidedFlag>();
        }
        public void OnUpdate(ref Entity pEntity, ref State state)
        {
            ref var pTransform = ref pEntity.Get<Transform>();
            ref var entity = ref pEntity.Get<EntityReference>().Value;
            if (entity.IsValid())
            {
                entity.Set(pTransform);
            }
            else
            {
                pEntity.Destroy();
            }
        }
    }

    [BurstCompile]
    public struct SynchroniseBackSystem : IEntityJobSystem
    {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world)
        {
            return world.Query().With<Transform>().With<Body2D>().With<Circle2D>().With<EntityReference>();
        }
        [BurstCompile]
        public void OnUpdate(ref Entity entity, ref State state)
        {
            ref var e = ref entity.Get<EntityReference>().Value;
            if (!e.IsValid())
            {
                entity.Destroy();
                return;
            }
            ref readonly var transform = ref e.Read<Transform>();
            ref readonly var body = ref e.Read<Body2D>();
            ref readonly var circle = ref e.Read<Circle2D>();

            entity.Set(transform);
            entity.Set(body);
            entity.Set(circle);
        }
    }
    public unsafe class Physics2D
    {
        public static int WorldID;
        private World physicsWorld;
        public ref World World => ref physicsWorld;
        private Systems update;
        private Systems fixedUpdate;
        private bool disposed;
        public Physics2D(ref World world)
        {
            this.physicsWorld = World.Create(world.Config);
            WorldID = physicsWorld.UnsafeWorld->Id;
            this.fixedUpdate = new Systems(ref physicsWorld)
                .Add<EntityDestroySystem>()
                .Add(new Collision2DGroup(ref physicsWorld))
                .Add<SynchroniseTransformsSystem>();

            this.update = new Systems(ref physicsWorld)
                    .Add<CreatePhysicsEntitySystem>()
                ;
            //physicsWorld.UnsafeWorld->RefreshArchetypes();
        }
        // ReSharper disable Unity.PerformanceAnalysis
        public void OnFixedUpdate(float deltaTime, float time)
        {
            if(disposed) return;
            fixedUpdate.OnUpdate(deltaTime, time);
        }
        // ReSharper disable Unity.PerformanceAnalysis
        public void OnUpdate(float deltaTime, float time)
        {
            if(disposed) return;
            update.OnUpdate(deltaTime, time);
        }

        public void Dispose()
        {
            if(disposed) return;
            physicsWorld.Dispose();
            disposed = true;
        }
    }
    
    public unsafe struct CreatePhysicsEntitySystem : ISystem, IOnCreate
    {
        private World world;
        private World physicsWorld;
        private Archetype circleArchetype;
        public void OnCreate(ref World world)
        {
            this.world = World.Get(0);
            this.physicsWorld = World.Get(1);
            // circleArchetype = world.UnsafeWorld->CreateArchetype(
            //     ComponentType<Transform>.Index,
            //     ComponentType<Circle2D>.Index,
            //     ComponentType<ComponentArray<Collision2DData>>.Index,
            //     ComponentType<Body2D>.Index,
            //     ComponentType<EntityReference>.Index);
        }
        public void OnUpdate(ref State state)
        {
            state.Dependencies = new Job { world = world.UnsafeWorld, physicsWorld = physicsWorld.UnsafeWorld }.Schedule(state.Dependencies);
        }
        private struct Job : IJob
        {
            [NativeDisableUnsafePtrRestriction] public World.WorldUnsafe* world;
            [NativeDisableUnsafePtrRestriction] public World.WorldUnsafe* physicsWorld;
            public void Execute()
            {
                for (int i = 0; i < world->prefabsToSpawn.Length; i++)
                {
                    ref var entity = ref world->prefabsToSpawn.Ptr[i];
                    if(!entity.Has<Circle2D>()) continue;
                    //var e = circleArchetype.impl->CreateEntity();
                    var e = physicsWorld->CreateEntity();
                    e.Add(new EntityReference{Value = entity});
                    e.Add(entity.Get<Transform>());
                    e.Add(entity.Get<Body2D>());
                    e.Add(entity.Get<Circle2D>());
                    dbug.log($"Physics for {e.id} Created");
                }
            }
        }
    }
}
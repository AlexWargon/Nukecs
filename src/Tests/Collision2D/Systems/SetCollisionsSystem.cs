﻿using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

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
    public struct SetCollisionsSystem : ISystem, IOnCreate
    {
        public void OnUpdate(ref State state)
        {
            ref var hits = ref Grid2D.Instance.Hits;
            if(hits.Count == 0) return;
            var hitsArray = hits.ToArray(Allocator.TempJob);
            
            state.Dependencies = new Fill
                {
                    World = state.World, Hits = hitsArray.AsReadOnly()
                }
                .Schedule(hitsArray.Length,64, state.Dependencies);
            
            state.Dependencies = hitsArray.Dispose(state.Dependencies);
        }

        [BurstCompile]
        private struct Fill : IJobParallelFor
        {
            public World World;
            [ReadOnly]
            public NativeArray<HitInfo>.ReadOnly Hits;
            //public ComponentPool<ComponentArray<Collision2DData>> collisionsData;
            public void Execute(int index)
            {
                var hit = Hits[index];
                ref var from = ref World.GetEntity(hit.From);
                ref var to = ref World.GetEntity(hit.To);
                AddToArray(ref from, ref to, in hit);
                AddToArray(ref to, ref from, in hit);
            }
            private void AddToArray(ref Entity e, ref Entity other, in HitInfo hitInfo)
            {
                if(!e.IsValid()) return;
                if(!other.IsValid()) return;
                
                ref var buffer = ref e.GetArray<Collision2DData>();
                buffer.AddNoResize(new Collision2DData
                {
                    Other = other,
                    Type = hitInfo.Type
                });
                e.Add(new CollidedFlag());
            }
        }

        public unsafe void OnCreate(ref World world)
        {
            PrintSizeInMB<Collision2DData>(64 * WorldConfig.Default163840.StartPoolSize);
            PrintSizeInMB<int>(ComponentAmount.Value.Data * WorldConfig.Default163840.StartPoolSize);
        }

        private unsafe void PrintSize<T>() where T : unmanaged
        {
            Debug.Log($"size of {typeof(T)} =  {sizeof(T).ToString()}");
        }

        private unsafe void PrintSizeInMB<T>(int elements) where T : unmanaged
        {
            Debug.Log($"size of {typeof(T)}[{elements}] =  {(sizeof(T) * elements / 1024/1024).ToString()} mb");
        }
    }
    
    public struct Collision2DData : IArrayComponent {
        public Entity Other;
        public float2 Position;
        public float2 Normal;
        public HitInfo.CollisionType Type;
    }

    public struct CollidedFlag : IComponent {}
    
    [BurstCompile]
    public struct CollisionsClear : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Single;
        public Query GetQuery(ref World world) {
            return world.Query().WithArray<Collision2DData>().With<CollidedFlag>();
        }

        public void OnUpdate(ref Entity entity, ref State state) {
            ref var buffer = ref entity.GetArray<Collision2DData>();
            buffer.Clear();
            entity.Remove<CollidedFlag>();
        }
    }
}
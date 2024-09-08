using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Wargon.Nukecs.Collision2D {
    public struct SetCollisionsSystem : ISystem
    {
        public void OnUpdate(ref State state)
        {
            ref var hits = ref Grid2D.Instance.Hits;
            if(hits.Count == 0) return;
            var hitsArray = hits.ToArray(Allocator.TempJob);
            state.Dependencies = new Fill
                    {World = state.World, Hits = hitsArray.AsReadOnly()}
                .Schedule(hitsArray.Length, state.Dependencies);
            state.Dependencies = hitsArray.Dispose(state.Dependencies);
        }
        [BurstCompile]
        public struct QueueToArray<T> : IJob where T : unmanaged {
            public NativeQueue<T> queue;
            public NativeArray<T> array;
            public void Execute() {
                array = queue.ToArray(Allocator.TempJob);
            }
        }
        [BurstCompile]
        private struct Fill : IJobFor
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
                AddToArray(ref from, ref to);
                AddToArray(ref to, ref from);
            }
            private void AddToArray(ref Entity e, ref Entity other)
            {
                if(!e.IsValid()) return;
                if(!other.IsValid()) return;
                
                ref var buffer = ref e.GetArray<Collision2DData>(256);
                buffer.Add(new Collision2DData
                {
                    Other = other
                });
                e.Add(new CollidedFlag());
            }
        }

        private void Log(int number) {
            
        }
    }
    
    public struct Collision2DData {
        public Entity Other;
        public float2 Position;
        public float2 Normal;
    }

    public struct CollidedFlag : IComponent {}
    
    [BurstCompile]
    public struct CollisionsClear : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Single;
        public Query GetQuery(ref World world) {
            return world.Query().WithArray<Collision2DData>().With<CollidedFlag>().None<DestroyEntity>();
        }

        public void OnUpdate(ref Entity entity, ref State state) {
            ref var buffer = ref entity.GetArray<Collision2DData>();
            buffer.Clear();
            entity.Remove<CollidedFlag>();
        }
    }
}
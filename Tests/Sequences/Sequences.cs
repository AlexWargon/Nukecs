using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;

namespace Wargon.Nukecs {
    namespace Sequences {

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Execute(ref Entity entity);
        public struct SequenceTask : IComponent {
            public float delay;
            public Entity target;
            public Entity next;
            public FunctionPointer<Execute> fn;
            public void Execute() {
                fn.Invoke(ref target);
            }
        }
        
        public struct SequenceBuilder {
            internal Entity last;
            internal Entity root;
            internal World world;
            public TaskBuilder Create(ref World world) {
                
                this.world = world;
                return new TaskBuilder(world.Entity<SequenceActive>(), ref this);
            }
        }

        public struct TaskBuilder {
            private Entity taskEntity;
            private SequenceBuilder _sequenceBuilder;
            public TaskBuilder(Entity t, ref SequenceBuilder sequenceBuilder) {
                taskEntity = t;
                _sequenceBuilder = sequenceBuilder;
            }
            public TaskBuilder WithTarget(Entity entity)
            {
                taskEntity.Get<SequenceTask>().target = entity;
                return this;
            }
            public TaskBuilder WithFunction(Execute action) {
                taskEntity.Get<SequenceTask>().fn = Functions<Execute>.GetPointer(action);
                return this;
            }
            public TaskBuilder WithDelay(float time) {
                taskEntity.Get<SequenceTask>().delay = time;
                //dbug.log($"task e:{taskEntity.id} with delay {time}");
                return this;
            }
            public TaskBuilder Next() {
                var next = _sequenceBuilder.world.Entity();
                taskEntity.Get<SequenceTask>().next = next;
                taskEntity = next;
                return this;
            }
        }

        public struct SequenceActive : IComponent {}
        
        public struct SequenceSystem : IEntityJobSystem {
            public SystemMode Mode => SystemMode.Single;
            public Query GetQuery(ref World world) {
                return world.Query().With<SequenceTask>().With<SequenceActive>();
            }

            public void OnUpdate(ref Entity entity, ref State state) {
                ref var sequence = ref entity.Get<SequenceTask>();
                sequence.delay -= state.Time.DeltaTime;
                if (sequence.delay <= 0) {
                    sequence.Execute();
                    if (sequence.next != Entity.Null)
                    {
                        sequence.next.Add<SequenceActive>();
                        //dbug.log("start next");
                    }
                    entity.Destroy();
                }
            }
        }
        public static class BaseMethods {
           
            public static void DestroyEntity(ref Entity entity) {
                entity.Destroy();
            }
        }

        public struct Functions<T> where T: class {
            private static readonly SharedStatic<NativeParallelHashMap<int, FunctionPointer<T>>> map = 
                SharedStatic<NativeParallelHashMap<int, FunctionPointer<T>>>.GetOrCreate<Functions<T>>();

            static Functions()
            {
                map.Data = new NativeParallelHashMap<int, FunctionPointer<T>>(12, Allocator.Persistent);
                World.OnDisposeStatic(Dispose);
            }

            private static void Dispose()
            {
                map.Data.Dispose();
            }
            public static FunctionPointer<T> GetPointer(T method) {
                if (method is Delegate d) {
                    var id = d.Method.MetadataToken;
                    if (!map.Data.ContainsKey(id)) {
                        map.Data.Add(id, BurstCompiler.CompileFunctionPointer<T>(method));
                    }
                    return map.Data[id];
                }

                throw new Exception($"Type {method} is not delegate");
            }
        }
    }
}
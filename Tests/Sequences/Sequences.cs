using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;

namespace Wargon.Nukecs.Sequences {
    namespace Sequences {

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Execute(ref Entity entity);
        public struct SequenceTask : IComponent {
            public float delay;
            public Entity target;
            public Entity next;
            public FunctionPointer<Execute> fnPtr;
            public void Init(Execute action, float time, ref Entity target, ref Entity next) {
                delay = time;
                this.target = target;
                this.next = next;
                fnPtr = Functions<Execute>.GetFP(action);
            }
            public void Execute() {
                fnPtr.Invoke(ref target);
            }
        }

        public interface IAction<out TAction, TArg> {
            void Invoke(ref TArg arg);
        }

        
        public struct SequenceBuilder {
            internal SequenceTask rootTask;
            internal Entity root;
            internal World world;
            public TaskBuilder Create(ref World world) {

                this.world = world;
                return new TaskBuilder(new SequenceTask {
                    
                }, ref this);
            }

        }

        public struct TaskBuilder {
            private SequenceTask _task;
            private SequenceBuilder _sequenceBuilder;
            public TaskBuilder(SequenceTask t, ref SequenceBuilder sequenceBuilder) {
                _task = t;
                _sequenceBuilder = sequenceBuilder;
            }
            public TaskBuilder WithAction(Execute action) {
                _task.fnPtr = Functions<Execute>.GetFP(action);
                return this;
            }
            public TaskBuilder WithDelay(float time) {
                _task.delay = time;
                return this;
            }
            public SequenceBuilder Build() {
                _task.target = _sequenceBuilder.world.Entity(in _task, new SequenceActive());
                _sequenceBuilder.rootTask = _task;
                return _sequenceBuilder;
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
                sequence.delay -= state.DeltaTime;
                if (sequence.delay <= 0) {
                    sequence.Execute();
                    entity.Remove<SequenceActive>();
                    sequence.next.Add<SequenceActive>();
                }

                var sb = new SequenceBuilder();
                sb.Create(ref state.World).WithAction(BaseMethods.DestroyEntity).WithDelay(2).Build();
            }
        }
        public static class BaseMethods {
           
            public static void DestroyEntity(ref Entity entity) {
                entity.Destroy();
            }
        }

        public static class TST {
            public static void Create(ref Entity target, ref Entity next) {

            }

            public static void T<T>(T method) where T : class {
                
            }
        }

        public struct Functions<T> where T: class{
            private static readonly SharedStatic<NativeParallelHashMap<int, FunctionPointer<T>>> map = 
                SharedStatic<NativeParallelHashMap<int, FunctionPointer<T>>>.GetOrCreate<Functions<T>>();
            public static FunctionPointer<T> GetFP(T method) {
                if (method is Delegate d) {
                    var id = d.Method.MetadataToken;
                    if (!map.Data.ContainsKey(id)) {
                        map.Data.Add(id, BurstCompiler.CompileFunctionPointer<T>(method));
                    }
                    return map.Data[id];
                }
                return default;
            }
        }
    }
}
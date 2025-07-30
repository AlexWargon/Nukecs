using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    public unsafe class Systems
    {
        internal readonly List<ISystemDestroyer> SystemDestroyers;
        internal JobHandle Dependencies;
        internal readonly List<ISystemRunner> FixedRunners;
        internal readonly List<ISystemRunner> Runners;
        internal readonly List<ISystemRunner> MainThreadFixedRunners;
        internal readonly List<ISystemRunner> MainThreadRunners;
        internal SystemsDependencies SystemsDependencies;
        internal World World;
        private State _state;
        private State _stateFixed;
        private const float FIXED_UPDATE_INTERVAL = 0.016f;
        private float _timeSinceLastFixedUpdate;
        public Systems(ref World world)
        {
            Dependencies = default;
            Runners = new List<ISystemRunner>();
            FixedRunners = new List<ISystemRunner>();
            MainThreadRunners = new List<ISystemRunner>();
            MainThreadFixedRunners = new List<ISystemRunner>();
            SystemDestroyers = new List<ISystemDestroyer>();
            SystemsDependencies = SystemsDependencies.Create();
            World = world;
            WorldSystems.Add(world.UnsafeWorld->Id, this);
        }

        public static Systems Default(ref World world)
        {
            return new Systems(ref world).AddDefaults();
        }

        public Systems AddDefaults()
        {
            Add<EntityDestroySystem>();
            Add<OnPrefabSpawnSystem>();
            Add<ClearEntityCreatedEventSystem>();
            return this;
        }

        public Systems RemoveComponent<T>() where T : unmanaged, IComponent
        {
            var system = new RemoveComponentSystem
            {
                Type = ComponentType<T>.Index
            };
            var runner = new EntityJobSystemRunner<RemoveComponentSystem>
            {
                System = system,
                Mode = system.Mode,
                EcbJob = default
            };
            runner.Query = runner.System.GetQuery(ref World).InternalPointer;
            Runners.Add(runner);
            return this;
        }

        public Systems Add<T>() where T : struct, IJobSystem
        {
            T system = default;
            if (system is IOnCreate s)
            {
                s.OnCreate(ref World);
                system = (T)s;
            }

            var runner = new JobSystemRunner<T>
            {
                System = system,
                EcbJob = default,
                isComplete = system is IComplete
            };
            if (system is IFixed)
                FixedRunners.Add(runner);
            else
                Runners.Add(runner);

            return this;
        }

        internal Systems AddRef<T>(ref T system) where T : struct, IEntityJobSystem
        {
            if (system is IOnCreate s)
            {
                s.OnCreate(ref World);
                system = (T)s;
            }

            var runner = new EntityJobSystemRunner<T>
            {
                System = system,
                Mode = system.Mode,
                EcbJob = default
            };
            runner.Query = runner.System.GetQuery(ref World).InternalPointer;
            if (system is IFixed)
                FixedRunners.Add(runner);
            else
                Runners.Add(runner);
            return this;
        }

        public Systems Add<T>(bool dymmy = false) where T : struct, IEntityJobSystem
        {
            T system = default;
            if (system is IOnCreate s)
            {
                s.OnCreate(ref World);
                system = (T)s;
            }

            var runner = new EntityJobSystemRunner<T>
            {
                System = system,
                Mode = system.Mode,
                EcbJob = default
            };
            runner.Query = runner.System.GetQuery(ref World).InternalPointer;
            if (system is IFixed)
                FixedRunners.Add(runner);
            else
                Runners.Add(runner);
            return this;
        }

        public Systems Add<T>(ushort dymmy = 1) where T : unmanaged, IEntityJobSystem, IOnDestroy
        {
            T system = default;
            if (system is IOnCreate s)
            {
                s.OnCreate(ref World);
                system = (T)s;
            }

            var runner = new EntityJobSystemRunner<T>
            {
                System = system,
                Mode = system.Mode,
                EcbJob = default
            };
            SystemDestroyers.Add(new SystemDestroyer<T>(ref runner.System));
            runner.Query = runner.System.GetQuery(ref World).InternalPointer;
            if (system is IFixed)
                FixedRunners.Add(runner);
            else
                Runners.Add(runner);
            return this;
        }

        public Systems Add<T>(short dymmy = 1) where T : struct, IQueryJobSystem
        {
            T system = default;
            if (system is IOnCreate s)
            {
                s.OnCreate(ref World);
                system = (T)s;
            }

            var runner = new QueryJobSystemRunner<T>
            {
                System = system,
                Mode = system.Mode,
                EcbJob = default
            };
            runner.Query = runner.System.GetQuery(ref World);
            if (system is IFixed)
                FixedRunners.Add(runner);
            else
                Runners.Add(runner);
            return this;
        }

        public Systems Add<T>(int dymmy = 1) where T : struct, ISystem
        {
            T system = default;
            if (system is IOnCreate onCreate)
            {
                onCreate.OnCreate(ref World);
                system = (T)onCreate;
            }

            var runner = new SystemMainThreadRunnerStruct<T>
            {
                System = system,
                EcbJob = default
            };

            if (system is IFixed)
                MainThreadFixedRunners.Add(runner);
            else
                MainThreadRunners.Add(runner);
            return this;
        }

        public Systems Add<T>(long dymmy = 1) where T : class, ISystem, new()
        {
            var system = new T();
            if (system is IOnCreate s)
            {
                s.OnCreate(ref World);
                system = (T)s;
            }

            var runner = new SystemMainThreadRunnerClass<T>
            {
                System = system,
                EcbJob = default
            };
            if (system is IFixed)
                MainThreadFixedRunners.Add(runner);
            else
                MainThreadRunners.Add(runner);
            if (system is IOnDestroy onDestroySystem) SystemDestroyers.Add(new SystemClassDestroyer(onDestroySystem));
            return this;
        }

        private Systems AddSystem<T>(T system) where T : class, ISystem, new()
        {
            if (system is IOnCreate s)
            {
                s.OnCreate(ref World);
                system = (T)s;
            }

            var runner = new SystemMainThreadRunnerClass<T>
            {
                System = system,
                EcbJob = default
            };
            if (system is IFixed)
                MainThreadFixedRunners.Add(runner);
            else
                MainThreadRunners.Add(runner);

            if (system is IOnDestroy onDestroySystem) SystemDestroyers.Add(new SystemClassDestroyer(onDestroySystem));
            return this;
        }

        public Systems Add<T>(T group) where T : SystemsGroup
        {
            group.world = World;
            Runners.AddRange(group.runners);
            FixedRunners.AddRange(group.fixedRunners);
            MainThreadRunners.AddRange(group.mainThreadRunners);
            MainThreadFixedRunners.AddRange(group.mainThreadFixedRunners);
            return this;
        } // ReSharper disable Unity.PerformanceAnalysis
        public void OnUpdate(float dt, float time)
        {
            _state.Dependencies.Complete();
            _state.Dependencies = World.DependenciesUpdate;
            _state.World = World;
            _state.Time.DeltaTime = dt;
            _state.Time.Time = time;
            _state.Time.ElapsedTime += dt;
            _state.Time.DeltaTimeFixed = FIXED_UPDATE_INTERVAL;
            for (var i = 0; i < MainThreadRunners.Count; i++)
                _state.Dependencies = MainThreadRunners[i].Schedule(UpdateContext.Update, ref _state);
            for (var i = 0; i < Runners.Count; i++)
                _state.Dependencies = Runners[i].Schedule(UpdateContext.Update, ref _state);

            _timeSinceLastFixedUpdate += dt;
            if (_timeSinceLastFixedUpdate >= FIXED_UPDATE_INTERVAL)
            {
                for (var i = 0; i < MainThreadFixedRunners.Count; i++)
                    _state.Dependencies = MainThreadFixedRunners[i].Schedule(UpdateContext.Update, ref _state);
                for (var i = 0; i < FixedRunners.Count; i++)
                    _state.Dependencies = FixedRunners[i].Schedule(UpdateContext.Update, ref _state);
                _timeSinceLastFixedUpdate = 0;
            }
        }


        internal void Complete()
        {
            _state.Dependencies.Complete();
            _stateFixed.Dependencies.Complete();
        }

        internal void OnWorldDispose()
        {
            Complete();
            foreach (var systemDestroyer in SystemDestroyers) systemDestroyer.Destroy(ref World);
            SystemsDependencies.Dispose();
        }
        // public void Run(float dt) {
        //     for (var i = 0; i < runners.Count; i++) {
        //         runners[i].Run(ref world, dt);
        //     }
        // }
    }

    public static class SystemsExtensions
    {
        public static unsafe Systems Add<T>(this Systems systems, SystemMode systemMode)
            where T : struct, IEntityJobSystem
        {
            T system = default;
            if (system is IOnCreate s)
            {
                s.OnCreate(ref systems.World);
                system = (T)s;
            }

            var runner = new EntityJobSystemRunner<T>
            {
                System = system,
                Mode = systemMode,
                EcbJob = default
            };
            runner.Query = runner.System.GetQuery(ref systems.World).InternalPointer;
            systems.Runners.Add(runner);
            return systems;
        }
    }

    public enum UpdateContext
    {
        Update,
        FixedUpdate
    }

    internal interface ISystemDestroyer
    {
        void Destroy(ref World world);
    }

    internal interface ISystemRunner
    {
        JobHandle Schedule(UpdateContext updateContext, ref State state);
        void Run(ref State state);
    }


    internal class GenericSystemMainThreadRunner<TSystem> : ISystemRunner where TSystem : struct, ISystem
    {
        internal ECBJob EcbJob;
        internal TSystem System;

        public JobHandle Schedule(UpdateContext updateContext, ref State state)
        {
            System.OnUpdate(ref state);
            EcbJob.ECB = state.World.ECB;
            EcbJob.world = state.World;
            return EcbJob.Schedule(state.Dependencies);
        }

        public void Run(ref State state)
        {
            System.OnUpdate(ref state);
            state.World.ECB.Playback(ref state.World);
        }
    }

    /// <summary>
    ///     Single : execute in one of threads, but not in main
    ///     Parallel : execute parallel
    ///     Main : execute in main thread, need to axes to unity api
    /// </summary>
    public enum SystemMode
    {
        Main,
        Parallel,
        Single
    }

    public interface IOnCreate
    {
        void OnCreate(ref World world);
    }

    public interface IFixed
    {
    }

    public interface IOnDestroy
    {
        void OnDestroy(ref World world);
    }

    public interface IComplete
    {
    }

    public interface ISystem
    {
        void OnUpdate(ref State state);
    }

    public abstract class System<T> : ISystem, IOnCreate where T : unmanaged, IComponent
    {
        private Query Query;

        public void OnCreate(ref World world)
        {
            Query = world.Query().With<T>();
        }

        public void OnUpdate(ref State state)
        {
            foreach (ref var entity in Query) OnUpdate(ref entity, ref entity.Get<T>(), ref state);
        }

        public abstract void OnUpdate(ref Entity entity, ref T component, ref State state);
    }

    // public unsafe class DisposeSystem<T> : ISystem, IOnCreate where T : unmanaged, IComponent, IDisposable
    // {
    //     private Query query;
    //     private GenericPool pool;
    //     public void OnCreate(ref World world)
    //     {
    //         query = world.Query().With<T>().With<Dispose<T>>();
    //         pool = world.GetPool<T>();
    //     }
    //     [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //     public void OnUpdate(ref State state)
    //     {
    //         if(query.Count == 0) return;
    //         foreach (ref var entity in query)
    //         {
    //             state.World.UnsafeWorld->Dispose<T>(ref pool, ref entity);
    //             Debug.Log($"{entity.id} {typeof(T).Name} Disposed");
    //         }
    //     }
    //
    // }

    [JobProducerType(typeof(EntityIndexJobSystemExtensions<>.EntityJobStruct<>))]
    public interface IEntityIndexJobSystem
    {
        SystemMode Mode { get; }
        void OnUpdate<T1>(ref T1 c1, ref State state) where T1 : unmanaged, IComponent;
    }

    public static class EntityIndexJobSystemExtensions<T1> where T1 : unmanaged, IComponent
    {
        public static void EarlyJobInit<T>() where T : struct, IEntityIndexJobSystem
        {
            EntityJobStruct<T>.Initialize();
        }

        public static IntPtr GetReflectionData<T>() where T : struct, IEntityIndexJobSystem
        {
            EntityJobStruct<T>.Initialize();
            return EntityJobStruct<T>.JobReflectionData.Data;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct EntityJobStruct<TJob> where TJob : struct, IEntityIndexJobSystem
        {
            public TJob JobData;
            public Query query;
            public State State;

            internal static readonly SharedStatic<IntPtr> JobReflectionData =
                SharedStatic<IntPtr>.GetOrCreate<EntityJobStruct<TJob>>();

            [BurstDiscard]
            internal static void Initialize()
            {
                if (JobReflectionData.Data == IntPtr.Zero)
                    JobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(EntityJobStruct<TJob>),
                        typeof(TJob), new ExecuteJobFunction(Execute));
            }

            private delegate void ExecuteJobFunction(ref EntityJobStruct<TJob> fullData, IntPtr additionalPtr,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            public static void Execute(ref EntityJobStruct<TJob> fullData, IntPtr additionalPtr,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex)
            {
                if (fullData.query.Count == 0) return;
                while (true)
                {
                    if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end))
                        break;
                    //JobsUtility.PatchBufferMinMaxRanges(bufferRangePatchData, UnsafeUtility.AddressOf<TJob>(ref fullData.JobData), begin, end - begin);
                    ref var c1pool = ref fullData.State.World.GetPool<T1>();
                    for (var i = begin; i < end; i++)
                        unsafe
                        {
                            var e = fullData.query.InternalPointer->GetEntityID(i);
                            fullData.JobData.OnUpdate(ref c1pool.GetRef<T1>(e), ref fullData.State);
                        }
                }
            }
        }
    }

    public static class EXT
    {
        public static unsafe JobHandle Schedule<TJob, T>(this TJob jobData, ref Query query, ref State state,
            SystemMode mode, JobHandle dependsOn = default)
            where TJob : struct, IEntityIndexJobSystem
            where T : unmanaged, IComponent
        {
            var fullData = new EntityIndexJobSystemExtensions<T>.EntityJobStruct<TJob>
            {
                JobData = jobData,
                query = query,
                State = state
            };

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData),
                EntityIndexJobSystemExtensions<T>.GetReflectionData<TJob>(), dependsOn,
                mode == SystemMode.Parallel ? ScheduleMode.Parallel : ScheduleMode.Single);
            switch (mode)
            {
                case SystemMode.Single:
                    return JobsUtility.Schedule(ref scheduleParams);
                case SystemMode.Parallel:
                    return JobsUtility.ScheduleParallelFor(ref scheduleParams, query.Count, 1);
            }

            //var workers = JobsUtility.JobWorkerCount;
            //var batchCount = query.Count > workers ? query.Count / workers : 1;
            return dependsOn;
        }
    }

    [BurstCompile]
    public struct ClearEntityCreatedEventSystem : IEntityJobSystem
    {
        public SystemMode Mode => SystemMode.Single;

        public Query GetQuery(ref World world)
        {
            return world.Query().With<EntityCreated>();
        }

        public void OnUpdate(ref Entity entity, ref State state)
        {
            entity.Remove<EntityCreated>();
        }
    }

    [BurstCompile]
    internal struct RemoveComponentSystem : IEntityJobSystem
    {
        internal int Type;
        public SystemMode Mode => SystemMode.Single;

        public Query GetQuery(ref World world)
        {
            return world.Query().With(Type);
        }

        [BurstCompile]
        public void OnUpdate(ref Entity entity, ref State state)
        {
            //dbug.log($"remove entity {entity}");
            state.World.ECB.Remove(entity.id, Type);
        }
    }

    public interface IComponentSystem<TAspect> where TAspect : unmanaged, IAspect
    {
        public void OnUpdate(ref TAspect aspect)
        {
        }
    }

    public struct ActionJob : IJob
    {
        public Action Action;

        public void Execute()
        {
            Action?.Invoke();
        }
    }

    public struct JobCallback : IJob
    {
        public FunctionPointer<Action> callback;

        public void Execute()
        {
            callback.Invoke();
        }
    }

    public static class JobParallelForExtensions
    {
        public static JobHandle ScheduleWithCallback<T>(this T job, Action callback, int len, int batchCount,
            JobHandle dependencies = default)
            where T : struct, IJobParallelFor
        {
            return new JobCallback
            {
                callback = new FunctionPointer<Action>(Marshal.GetFunctionPointerForDelegate(callback))
            }.Schedule(job.Schedule(len, batchCount, dependencies));
        }
    }

    public static class JobForExtensions
    {
        public static JobHandle ScheduleWithCallback<T>(this T job, Action callback, int len,
            JobHandle dependencies = default)
            where T : struct, IJobFor
        {
            return new JobCallback
            {
                callback = new FunctionPointer<Action>(Marshal.GetFunctionPointerForDelegate(callback))
            }.Schedule(job.Schedule(len, dependencies));
        }
    }

    public static class JobExtensions
    {
        public static JobHandle ScheduleWithCallback<T>(this T job, Action callback, JobHandle dependencies = default)
            where T : struct, IJob
        {
            return new JobCallback
            {
                callback = new FunctionPointer<Action>(Marshal.GetFunctionPointerForDelegate(callback))
            }.Schedule(job.Schedule(dependencies));
        }

        public static JobHandle ScheduleWithCallback<T>(this T jobData, int arrayLength, int indicesPerJobCount,
            Action callback,
            JobHandle dependsOn = default)
            where T : struct, IJobParallelForBatch
        {
            var handle = jobData.ScheduleBatch(arrayLength, indicesPerJobCount, dependsOn);
            return new JobCallback
            {
                callback = new FunctionPointer<Action>(Marshal.GetFunctionPointerForDelegate(callback))
            }.Schedule(handle);
        }
    }

    internal struct SystemInfo<T>
    {
        internal static SharedStatic<int> data = SharedStatic<int>.GetOrCreate<SystemInfo<T>>();
        internal static int Index => data.Data;
    }

    public struct SystemsDependencies
    {
        private NativeList<JobHandle> list;
        private NativeArray<JobHandle> array;
        private int lastDefault;

        public static SystemsDependencies Create()
        {
            var systemsDependencies = new SystemsDependencies
            {
                list = new NativeList<JobHandle>(16, Allocator.Persistent),
                lastDefault = 0
            };
            systemsDependencies.list.Add(new JobHandle());
            return systemsDependencies;
        }

        public void Complete()
        {
            if (!array.IsCreated) array = list.AsArray();
            JobHandle.CompleteAll(array);
        }

        public void Dispose()
        {
            list.Dispose();
            array.Dispose();
        }

        public int GetIndex<T>()
        {
            return SystemInfo<T>.Index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JobHandle GetDependencies<T>()
        {
            return list[SystemInfo<T>.Index];
        }

        public void SetDependenciesNew<TTo>(JobHandle handle = default)
        {
            SystemInfo<TTo>.data.Data = list.Length;
            list.Add(handle);
        }

        public void SetDependencies<TFrom, TTo>()
        {
            SystemInfo<TTo>.data.Data = SystemInfo<TFrom>.Index;
        }

        public void SetDependenciesDefault<TTo>()
        {
            SystemInfo<TTo>.data.Data = 0;
        }
    }

    public static class SystemsEx
    {
        // public static unsafe Systems Add<TSystem, TDependsOn>(this Systems systems, SystemMode mode = SystemMode.Parallel) where TSystem : struct, IEntityJobSystem where TDependsOn : struct, IEntityJobSystem
        // {
        //     systems.systemsDependencies.SetDependencies<TDependsOn, TSystem>();
        //     TSystem system = default;
        //     if (system is IOnCreate s) {
        //         s.OnCreate(ref systems.world);
        //         system = (TSystem) s;
        //     }
        //
        //     var runner = new EntityJobSystemRunner<TSystem> {
        //         System = system,
        //         Mode = system.Mode,
        //         EcbJob = default,
        //         jobHandle = systems.systemsDependencies.GetDependenciesPtr<TSystem>()
        //     };
        //     
        //     runner.Query = runner.System.GetQuery(ref systems.world);
        //     systems.runners.Add(runner);
        //     
        //     return systems;
        // }
        public static Systems Add(this Systems systems, Delegate @delegate)
        {
            var functionPointer = BurstCompiler.CompileFunctionPointer(@delegate);
            var gcHandle = GCHandle.Alloc(@delegate);

            return systems;
        }
    }

    public delegate void Fn<T1, T2, T3>(UnsafeTuple<T1, T2, T3> query)
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged;
}
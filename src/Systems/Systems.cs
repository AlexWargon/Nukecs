using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Wargon.Nukecs.Tests;

namespace Wargon.Nukecs
{
    public unsafe class Systems {
        internal JobHandle dependencies;
        internal List<ISystemRunner> runners;
        internal List<ISystemRunner> disposeSystems;
        internal List<ISystemDestroyer> _systemDestroyers;
        internal World world;
        internal SystemsDependencies systemsDependencies;
        private NativeList<JobHandle> dependenciesList;
        //private ECBSystem _ecbSystem;
        public Systems(ref World world) {
            this.dependencies = default;
            this.runners = new List<ISystemRunner>();
            this.disposeSystems = new List<ISystemRunner>();
            this._systemDestroyers = new List<ISystemDestroyer>();
            this.dependenciesList = new NativeList<JobHandle>(12, AllocatorManager.Persistent);
            this.systemsDependencies = SystemsDependencies.Create();
            this.world = world;
            //_ecbSystem = default;
            //_ecbSystem.OnCreate(ref world);

            //this.InitDisposeSystems();
            WorldSystems.Add(world.UnsafeWorld->Id, this);
        }

        public static Systems Default(ref World world) {
            return new Systems(ref world).AddDefaults();
        }
        public Systems AddDefaults()
        {
            this.Add<EntityDestroySystem>();
            this.Add<OnPrefabSpawnSystem>();
            this.Add<ClearEntityCreatedEventSystem>();
            return this;
        }

        public Systems Add<T>() where T : struct, IJobSystem {
            T system = default;
            if (system is IOnCreate s) {
                s.OnCreate(ref world);
                system = (T) s;
            }

            var runner = new JobSystemRunner<T>
            {
                System = system,
                EcbJob = default,
                isComplete = system is IComplete
            };
            runners.Add(runner);
            return this;
        }
        
        // public Systems Add<T, T2>() where T : struct, IQueryJobSystem<T2> where T2 : struct, ITuple{
        //     T system = default;
        //     if (system is IOnCreate s) {
        //         s.OnCreate(ref world);
        //         system = (T) s;
        //     }
        //     system.GetQuery(new Query<T2>(new T2(), world.Unsafe));
        //     runners.Add(new QueryJobSystemRunner<T,T2> {
        //         System = system,
        //         EcbJob = default
        //     });
        //     return this;
        // }
        public Systems Add<T>(bool dymmy = false) where T : struct, IEntityJobSystem {
            T system = default;
            if (system is IOnCreate s) {
                s.OnCreate(ref world);
                system = (T) s;
            }

            var runner = new EntityJobSystemRunner<T> {
                System = system,
                Mode = system.Mode,
                EcbJob = default
            };
            runner.Query = runner.System.GetQuery(ref world);
            runners.Add(runner);
            return this;
        }
        public Systems Add<T>(ushort dymmy = 1) where T : unmanaged, IEntityJobSystem, IOnDestroy {
            T system = default;
            if (system is IOnCreate s) {
                s.OnCreate(ref world);
                system = (T) s;
            }

            var runner = new EntityJobSystemRunner<T> {
                System = system,
                Mode = system.Mode,
                EcbJob = default
            };
            _systemDestroyers.Add(new SystemDestroyer<T>(ref runner.System));
            runner.Query = runner.System.GetQuery(ref world);
            runners.Add(runner);
            return this;
        }
        public Systems Add<T>(short dymmy = 1) where T : struct, IQueryJobSystem {
            T system = default;
            if (system is IOnCreate s) {
                s.OnCreate(ref world);
                system = (T) s;
            }
            var runner = new QueryJobSystemRunner<T> {
                System = system,
                Mode = system.Mode,
                EcbJob = default
            };
            runner.Query = runner.System.GetQuery(ref world);
            runners.Add(runner);
            return this;
        }

        public Systems Add<T>(int dymmy = 1) where T : struct, ISystem {
            T system = default;
            if (system is IOnCreate s) {
                s.OnCreate(ref world);
                system = (T) s;
            }
            
            var runner = new SystemMainThreadRunnerStruct<T> {
                System = system,
                EcbJob = default
            };
            runners.Add(runner);
            return this;
        }
        public Systems Add<T>(long dymmy = 1) where T : class, ISystem, new() {
            T system = new T();
            if (system is IOnCreate s) {
                s.OnCreate(ref world);
                system = (T) s;
            }
            
            var runner = new SystemMainThreadRunnerClass<T> {
                System = system,
                EcbJob = default
            };
            runners.Add(runner);
            return this;
        }
        private Systems AddSystem<T>(T system) where T : class, ISystem, new() {
            if (system is IOnCreate s) {
                s.OnCreate(ref world);
                system = (T) s;
            }
            
            var runner = new SystemMainThreadRunnerClass<T> {
                System = system,
                EcbJob = default
            };
            runners.Add(runner);
            return this;
        }
        public Systems Add<T>(T group) where T : SystemsGroup {
            group.world = world;
            for (int i = 0; i < group.runners.Count; i++)
            {
                runners.Add(group.runners[i]);
            }

            return this;
        }

        private State state;
        private State stateFixed;
        public void OnUpdate(float dt, float time)
        {
            //systemsDependencies.Complete();
            state.Time.ElapsedTime += dt;
            state.Dependencies.Complete();
            //stateFixed.Dependencies.Complete();
            state.Dependencies = world.DependenciesUpdate;
            state.World = world;
            state.Time.DeltaTime = dt;
            state.Time.Time = time;
            for (var i = 0; i < runners.Count; i++) {
                state.Dependencies = runners[i].Schedule(UpdateContext.Update, ref state);
            }
        }
        public void OnFixedUpdate(float dt, float time)
        {
            //state.Dependencies.Complete();
            stateFixed.Dependencies.Complete();
            stateFixed.World = world;
            stateFixed.Time.DeltaTime = dt;
            stateFixed.Time.Time = time;
            for (var i = 0; i < runners.Count; i++) {
                stateFixed.Dependencies = runners[i].Schedule(UpdateContext.FixedUpdate, ref stateFixed);
            }
            
        }

        internal void Complete()
        {
            state.Dependencies.Complete();
            stateFixed.Dependencies.Complete();
        }

        internal void OnWorldDispose()
        {
            Complete();
            dependenciesList.Dispose();
            foreach (var systemDestroyer in _systemDestroyers)
            {
                systemDestroyer.Destroy(ref world);
            }
            systemsDependencies.Dispose();
        }
        // public void Run(float dt) {
        //     for (var i = 0; i < runners.Count; i++) {
        //         runners[i].Run(ref world, dt);
        //     }
        // }
    }

    public static class SystemsExtensions {
        public static Systems Add<T>(this Systems systems, SystemMode systemMode) where T : struct, IEntityJobSystem{
            T system = default;
            if (system is IOnCreate s) {
                s.OnCreate(ref systems.world);
                system = (T) s;
            }

            var runner = new EntityJobSystemRunner<T> {
                System = system,
                Mode = systemMode,
                EcbJob = default
            };
            runner.Query = runner.System.GetQuery(ref systems.world);
            systems.runners.Add(runner);
            return systems;
        }
    }
    /// <summary>
    /// <code>
    /// Dependencies
    /// World
    /// Time
    /// </code>
    /// </summary>
    public struct State
    {
        public JobHandle Dependencies;
        public World World;
        public TimeData Time;
    }

    public struct TimeData
    {
        public float DeltaTime;
        public float Time;
        public double ElapsedTime;
    }
    public enum UpdateContext {
        Update,
        FixedUpdate
    }
    
    [BurstCompile]
    public struct ECBJob : IJob {
        public EntityCommandBuffer ECB;
        public World world;
        public UpdateContext updateContext;
        public void Execute() {
            ECB.Playback(ref world);
        }
    }
    internal interface ISystemDestroyer
    {
        void Destroy(ref World world);
    }

    internal unsafe class SystemDestroyer<T> : ISystemDestroyer where T : unmanaged, IOnDestroy
    {
        private T* system;
        private GCHandle gcHandle;
        public SystemDestroyer(ref T system)
        {
            fixed (T* ptr = &system)
            {
                this.system = ptr;
                gcHandle = GCHandle.Alloc(system);
            }
        }
        public void Destroy(ref World world)
        {
            system->OnDestroy(ref world);
            gcHandle.Free();
        }
    }
    internal interface ISystemRunner {
        JobHandle Schedule(UpdateContext updateContext, ref State state);
        void Run(ref State state);
    }
    
    internal class QueryJobSystemRunner<TSystem,T> : ISystemRunner where TSystem : struct, IQueryJobSystem<T> 
        where T : struct, ITuple
    {
        public TSystem System;
        public Query<T> Query;
        public SystemMode Mode;
        public ECBJob EcbJob;
        private GenericJobWrapper JobWrapper;
        public JobHandle Schedule(UpdateContext updateContext, ref State state) {
            if (Mode == SystemMode.Main) {
                System.OnUpdate(Query);
            }
            else {

                JobWrapper.query = Query;
                JobWrapper.dt = state.Time.DeltaTime;
                JobWrapper.system = System;
                state.Dependencies = JobWrapper.Schedule(Query.Count, 1, state.Dependencies);
            }
            
            EcbJob.ECB = state.World.ECB;
            EcbJob.world = state.World;
            return EcbJob.Schedule(state.Dependencies);
        }
    
        public void Run(ref State state) {

        }
        [BurstCompile]
        internal struct GenericJobWrapper : IJobParallelFor {

            public TSystem system;
            public Query<T> query;
            public float dt;
            public void Execute(int index) {

            }
        }
    }

    internal class GenericSystemMainThreadRunner<TSystem> : ISystemRunner where TSystem : struct, ISystem {
        internal TSystem System;
        internal ECBJob EcbJob;

        public JobHandle Schedule(UpdateContext updateContext, ref State state) {
            System.OnUpdate(ref state);
            EcbJob.ECB = state.World.ECB;
            EcbJob.world = state.World;
            return EcbJob.Schedule(state.Dependencies);
        }

        public void Run(ref State state) {
            System.OnUpdate(ref state);
            state.World.ECB.Playback(ref state.World);
        }
    }
    /// <summary>
    /// Single : execute in one of threads, but not in main
    /// Parallel : execute parallel
    /// Main : execute in main thread, need to axes to unity api
    /// </summary>
    public enum SystemMode {
        Main,
        Parallel,
        Single
    }


    
    [JobProducerType(typeof(QueryJobSystemExtensions.QueryJobStruct<>))]
    public interface IQueryJobSystem {
        SystemMode Mode { get; }
        Query GetQuery(ref World world);
        void OnUpdate(ref Query query, float deltaTime);
    }
    public static class QueryJobSystemExtensions {
        [StructLayout(LayoutKind.Sequential)]
        internal struct QueryJobStruct<TJob> where TJob : struct, IQueryJobSystem {
            public TJob JobData;
            public Query query;
            public float deltaTime;

            internal static readonly SharedStatic<IntPtr> JobReflectionData = SharedStatic<IntPtr>.GetOrCreate<QueryJobStruct<TJob>>();

            [BurstDiscard]
            internal static void Initialize() {
                if (JobReflectionData.Data == IntPtr.Zero) {
                    JobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(QueryJobStruct<TJob>), typeof(TJob), (object) new ExecuteJobFunction(Execute));
                }
            }

            private delegate void ExecuteJobFunction(ref QueryJobStruct<TJob> fullData, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);
            
            public static void Execute(ref QueryJobStruct<TJob> fullData, IntPtr additionalPtr, IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) 
            {
                while (true) {
                    if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end))
                        break;
                    //JobsUtility.PatchBufferMinMaxRanges(bufferRangePatchData, UnsafeUtility.AddressOf<TJob>(ref fullData.JobData), begin, end - begin);
                   
                    for (var i = begin; i < end; i++) {
                        fullData.JobData.OnUpdate(ref fullData.query, fullData.deltaTime);
                    }
                }
            }
        }


        public static void EarlyJobInit<T>() where T : struct, IQueryJobSystem {
            QueryJobStruct<T>.Initialize();
        }

        private static IntPtr GetReflectionData<T>() where T : struct, IQueryJobSystem {
            QueryJobStruct<T>.Initialize();
            return QueryJobStruct<T>.JobReflectionData.Data;
        }

        public static unsafe JobHandle Schedule<TJob>(this TJob jobData, ref Query query, float deltaTime, SystemMode mode, JobHandle dependsOn = default)
            where TJob : struct, IQueryJobSystem 
        {
            var fullData = new QueryJobStruct<TJob> {
                JobData = jobData,
                query = query,
                deltaTime = deltaTime
            };
            
            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData),
                GetReflectionData<TJob>(), dependsOn,
                mode == SystemMode.Parallel ? ScheduleMode.Parallel : ScheduleMode.Single);
            switch (mode) {
                case SystemMode.Single:
                    return JobsUtility.Schedule(ref scheduleParams);
                case SystemMode.Parallel:
                    return JobsUtility.ScheduleParallelFor(ref scheduleParams, 1, 1);
            }
            //var workers = JobsUtility.JobWorkerCount;
            //var batchCount = query.Count > workers ? query.Count / workers : 1;
            return dependsOn;
        }
    }

    public interface IQueryJobSystem<T> where T : struct, ITuple {
        public Query<T> GetQuery(Query<T> query) {
            return query;
        }
        void OnUpdate(Query<T> query);
    }

    public interface IOnCreate {
        void OnCreate(ref World world);
    }

    public interface IOnDestroy
    {
        void OnDestroy(ref World world);
    }
    public interface IComplete{}
    public interface ISystem {
        void OnUpdate(ref State state);
    }

    public abstract class System<T> : ISystem, IOnCreate  where T : unmanaged, IComponent
    {
        private Query Query;
        public abstract void OnUpdate(ref Entity entity, ref T component, ref State state);
        public void OnUpdate(ref State state)
        {
            foreach (ref var entity in Query)
            {
                OnUpdate(ref entity, ref entity.Get<T>(), ref state);
            }
        }

        public void OnCreate(ref World world)
        {
            Query = world.Query().With<T>();
        }
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
    
    public static class EntityIndexJobSystemExtensions<T1> where T1 : unmanaged, IComponent {
        [StructLayout(LayoutKind.Sequential)]
        internal struct EntityJobStruct<TJob> where TJob : struct, IEntityIndexJobSystem {
            public TJob JobData;
            public Query query;
            public State State;

            internal static readonly SharedStatic<IntPtr> JobReflectionData =
                SharedStatic<IntPtr>.GetOrCreate<EntityJobStruct<TJob>>();

            [BurstDiscard]
            internal static void Initialize() {
                if (JobReflectionData.Data == IntPtr.Zero) {
                    JobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(EntityJobStruct<TJob>),
                        typeof(TJob),  new ExecuteJobFunction(Execute));
                }
            }

            private delegate void ExecuteJobFunction(ref EntityJobStruct<TJob> fullData, IntPtr additionalPtr,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);
            
            public static void Execute(ref EntityJobStruct<TJob> fullData, IntPtr additionalPtr,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {
                if(fullData.query.Count == 0) return;
                while (true) {
                    if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end))
                        break;
                    //JobsUtility.PatchBufferMinMaxRanges(bufferRangePatchData, UnsafeUtility.AddressOf<TJob>(ref fullData.JobData), begin, end - begin);
                    ref var c1pool = ref fullData.State.World.GetPool<T1>();
                    for (var i = begin; i < end; i++) {
                        unsafe
                        {
                            var e = fullData.query.impl->GetEntityID(i);
                            fullData.JobData.OnUpdate(ref c1pool.GetRef<T1>(e), ref fullData.State);
                        }
                    }
                }
            }
        }


        public static void EarlyJobInit<T>() where T : struct, IEntityIndexJobSystem {
            EntityJobStruct<T>.Initialize();
        }

        public static IntPtr GetReflectionData<T>() where T : struct, IEntityIndexJobSystem {
            EntityJobStruct<T>.Initialize();
            return EntityJobStruct<T>.JobReflectionData.Data;
        }
    }
   

    public static class EXT
    {
        public static unsafe JobHandle Schedule<TJob,T>(this TJob jobData, ref Query query, ref State state,
            SystemMode mode, JobHandle dependsOn = default)
            where TJob : struct, IEntityIndexJobSystem 
            where T : unmanaged, IComponent {
            var fullData = new EntityIndexJobSystemExtensions<T>.EntityJobStruct<TJob> {
                JobData = jobData,
                query = query,
                State = state
            };
            
            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData),
                EntityIndexJobSystemExtensions<T>.GetReflectionData<TJob>(), dependsOn,
                mode == SystemMode.Parallel ? ScheduleMode.Parallel : ScheduleMode.Single);
            switch (mode) {
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
    public struct ClearEntityCreatedEventSystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Single;
        public Query GetQuery(ref World world) {
            return world.Query().With<EntityCreated>();
        }
        public void OnUpdate(ref Entity entity, ref State state) {
            entity.Remove<EntityCreated>();
        }
    }

    public interface IComponentSystem<TAspect>  where TAspect : unmanaged, IAspect
    {
        public void OnUpdate(ref TAspect aspect)
        {
        }
    }

    public struct JobCallback : IJob {
        public FunctionPointer<Action> callback;
        public void Execute() {
            callback.Invoke();
        }
    }

    public static class JobParallelForExtensions {
        public static JobHandle ScheduleWithCallback<T>(this T job, Action callback, int len, int batchCount, JobHandle dependencies = default)
            where T : struct, IJobParallelFor {
            return new JobCallback {
                callback = new FunctionPointer<Action>(Marshal.GetFunctionPointerForDelegate(callback))
            }.Schedule(job.Schedule(len, batchCount, dependencies));
        }
    }
    public static class JobForExtensions {
        public static JobHandle ScheduleWithCallback<T>(this T job, Action callback, int len, JobHandle dependencies = default)
            where T : struct, IJobFor {
            return new JobCallback {
                callback = new FunctionPointer<Action>(Marshal.GetFunctionPointerForDelegate(callback))
            }.Schedule(job.Schedule(len, dependencies));
        }
    }
    public static class JobExtensions {
        public static JobHandle ScheduleWithCallback<T>(this T job, Action callback, JobHandle dependencies = default) 
            where T : struct, IJob {
            return new JobCallback {
                callback = new FunctionPointer<Action>(Marshal.GetFunctionPointerForDelegate(callback))
            }.Schedule(job.Schedule(dependencies));
        }
    }

    internal struct SystemInfo<T> {
        internal static SharedStatic<int> data = SharedStatic<int>.GetOrCreate<SystemInfo<T>>();
        internal static int Index => data.Data;
    }
    public unsafe struct SystemsDependencies {
        private UnsafeList<JobHandle> list;
        private NativeArray<JobHandle> array;
        private int lastDefault;

        public static SystemsDependencies Create() {
            var systemsDependencies = new SystemsDependencies() {
                list = new UnsafeList<JobHandle>(16, Allocator.Persistent),
                lastDefault = 0
            };
            systemsDependencies.list.Add(new JobHandle());
            return systemsDependencies;
        }

        public void Complete() {
            if (!array.IsCreated) {
                array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<JobHandle>(list.Ptr, list.Length,
                    Allocator.Persistent);
            }

            JobHandle.CompleteAll(array);

        }
        public void Dispose()
        {
            list.Dispose();
            array.Dispose();
        }
        public int GetIndex<T>() {
            return SystemInfo<T>.Index;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref JobHandle GetDependencies<T>() {
            return ref list.Ptr[SystemInfo<T>.Index];
        }

        public JobHandle* GetDependenciesPtr<T>() {
            return list.Ptr + SystemInfo<T>.Index;
        }
        public void SetDependenciesNew<TTo>(JobHandle handle = default) {
            SystemInfo<TTo>.data.Data = list.Length;
            list.Add(handle);
        }
        public void SetDependencies<TFrom, TTo>() {
            SystemInfo<TTo>.data.Data = SystemInfo<TFrom>.Index;
        }
        public void SetDependenciesDefault<TTo>() {
            SystemInfo<TTo>.data.Data = 0;
        }
    }

    public static class SystemsEx {
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
        public static unsafe Systems Add(this Systems systems, Delegate @delegate) {
            var functionPointer = BurstCompiler.CompileFunctionPointer(@delegate);
            var gcHandle = GCHandle.Alloc(@delegate);
            
            return systems;
        }
    }

    public unsafe delegate void QueryFn<T1, T2, T3>(UnsafeTuple<T1, T2, T3> query)
        where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged;
}
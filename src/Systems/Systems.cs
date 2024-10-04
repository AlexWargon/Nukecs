﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using Wargon.Nukecs.Tests;

namespace Wargon.Nukecs
{
    public unsafe class Systems {
        internal JobHandle dependencies;
        private List<ISystemRunner> runners;
        private List<ISystemRunner> disposeSystems;
        private World world;

        private NativeList<JobHandle> dependenciesList;
        //private ECBSystem _ecbSystem;
        public Systems(ref World world) {
            this.dependencies = default;
            this.runners = new List<ISystemRunner>();
            this.disposeSystems = new List<ISystemRunner>();
            this.dependenciesList = new NativeList<JobHandle>(12, AllocatorManager.Persistent);
            this.world = world;
            //_ecbSystem = default;
            //_ecbSystem.OnCreate(ref world);

            //this.InitDisposeSystems();
            WorldSystems.Add(world.UnsafeWorld->Id, this);
        }
        
        public Systems AddDefaults()
        {
            this.Add<EntityDestroySystem>();
            this.Add<OnPrefabSpawnSystem>();
            this.Add<ClearEntityCreatedEventSystem>();
            return this;
        }
        public Systems AddEndSystems() {
            Add<ClearEntityCreatedEventSystem>();
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
        // private void AddDisposeSystem<T>() where T: unmanaged, IComponent, IDisposable
        // {
        //     var system = new DisposeSystem<T>();
        //     if (system is IOnCreate s) {
        //         s.OnCreate(ref world);
        //         system = (DisposeSystem<T>) s;
        //     }
        //     
        //     var runner = new SystemMainThreadRunnerClass<DisposeSystem<T>> {
        //         System = system,
        //         EcbJob = default
        //     };
        //     disposeSystems.Add(runner);
        // }
        // private void InitDisposeSystems()
        // {
        //     var addMethod = typeof(Systems).GetMethod(nameof(AddDisposeSystem));
        //
        //     var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        //     foreach (var assembly in assemblies) {
        //         var types = assembly.GetTypes();
        //         foreach (var type in types) {
        //             if (typeof(IComponent).IsAssignableFrom(type) && typeof(IDisposable).IsAssignableFrom(type))
        //             {
        //                 addMethod?.MakeGenericMethod(type).Invoke(this, null);
        //             }
        //         }
        //     }
        // }
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
            state.Dependencies.Complete();
            //stateFixed.Dependencies.Complete();
            state.Dependencies = world.DependenciesUpdate;
            state.World = world;
            state.Time.DeltaTime = dt;
            state.Time.Time = time;
            for (var i = 0; i < runners.Count; i++) {
                state.Dependencies = runners[i].Schedule(UpdateContext.Update, ref state);
            }
            
            //for (var i = 0; i < disposeSystems.Count; i++)
            //{
            //    world.DependenciesUpdate = disposeSystems[i].Schedule(ref world, dt, ref world.DependenciesUpdate, UpdateContext.Update);
            //}
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
        }
        // public void Run(float dt) {
        //     for (var i = 0; i < runners.Count; i++) {
        //         runners[i].Run(ref world, dt);
        //     }
        // }
    }

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
    }
    public enum UpdateContext {
        Update,
        FixedUpdate
    }

    public unsafe struct ECBSystem : ISystem, IOnCreate, IInit {
        public static ref ECBSystem Singleton => ref Singleton<ECBSystem>.Instance;
        public void OnCreate(ref World world) {
            ecbList = new UnsafeList<EntityCommandBuffer>(3, world.UnsafeWorld->allocator);
            Singleton<ECBSystem>.Set(ref this);
        }
        private int count;
        private UnsafeList<EntityCommandBuffer> ecbList;
        public ref EntityCommandBuffer CreateEcb() {
            if (ecbList.Length >= count) {
                var ecb = new EntityCommandBuffer(1024);
                ecbList.Add(ecb);
            }
            var index = count;
            count++;
            return ref ecbList.ElementAt(index);
        }

        public void Init()
        {
            
        }
        public void OnUpdate(ref State state) {
            for (int i = 0; i < count; i++) {
                ecbList.ElementAt(i).Playback(ref state.World);
            }
            count = 0;
        }

        internal void OnDestroy() {
            foreach (var entityCommandBuffer in ecbList) {
                entityCommandBuffer.Clear();
            }
            ecbList.Dispose();
        }
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
}
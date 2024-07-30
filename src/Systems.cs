using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Wargon.Nukecs.Tests;

namespace Wargon.Nukecs {
    public unsafe struct Systems {
        internal JobHandle dependencies;
        private List<ISystemRunner> runners;
        private World world;
        
        //private ECBSystem _ecbSystem;
        public Systems(ref World world) {
            this.dependencies = default;
            this.runners = new List<ISystemRunner>();
            this.world = world;
            //_ecbSystem = default;
            //_ecbSystem.OnCreate(ref world);
            Add<EntityDestroySystem>();
        }

        public Systems Add<T>() where T : struct, IJobSystem {
            T system = default;
            if (system is IOnCreate s) {
                s.OnCreate(ref world);
                system = (T) s;
            }
            runners.Add(new SystemJobRunner<T> {
                System = system,
                EcbJob = default
            });
            return this;
        }
        public Systems Add<T, T2>() where T : struct, IQueryJobSystem<T2> where T2 : struct, ITuple{
            T system = default;
            if (system is IOnCreate s) {
                s.OnCreate(ref world);
                system = (T) s;
            }
            system.GetQuery(new Query<T2>(new T2(), world.Unsafe));
            runners.Add(new QueryJobSystemRunner<T,T2> {
                System = system,
                EcbJob = default
            });
            return this;
        }
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

        // public Systems Add<TSystem,T1,T2>(byte dymmy = 1) where TSystem : struct, IEntityJobSystem<T1,T2> 
        //     where T1 : unmanaged, IComponent 
        //     where T2 : unmanaged, IComponent
        // {
        //     TSystem system = default;
        //     if (system is IOnCreate s) {
        //         s.OnCreate(ref world);
        //         system = (TSystem) s;
        //     }
        //     
        //     var runner = new EntityJobSystemRunner<TSystem,T1,T2> {
        //         System = system,
        //         Mode = system.Mode,
        //         EcbJob = default
        //     };
        //     runner.Query = runner.System.GetQuery(ref world);
        //     runners.Add(runner);
        //     return this;
        // }
        public Systems Add<T>(int dymmy = 1) where T : struct, ISystem {
            T system = default;
            if (system is IOnCreate s) {
                s.OnCreate(ref world);
                system = (T) s;
            }
            
            var runner = new SystemMainThreadRunner<T> {
                System = system,
                EcbJob = default
            };
            runners.Add(runner);
            return this;
        }
        public void OnUpdate(float dt) {
            world.Dependencies.Complete();
            //_ecbSystem.OnUpdate(ref world, dt);
            for (var i = 0; i < runners.Count; i++) {
                world.Dependencies = runners[i].Schedule(ref world, dt, ref world.Dependencies);
            }
        }

        public void Run(float dt) {
            for (var i = 0; i < runners.Count; i++) {
                runners[i].Run(ref world, dt);
            }
        }
    }
    [BurstCompile]
    public struct EFBJob : IJob {
        public EntityFilterBuffer EFB;
        public void Execute() {
            EFB.Playback();
        }
    }
    public unsafe struct ECBSystem : ISystem, IOnCreate {
        public static ref ECBSystem Singleton => ref Singleton<ECBSystem>.Instance;
        public void OnCreate(ref World world) {
            ecbList = new UnsafeList<EntityCommandBuffer>(3, world.Unsafe->allocator);
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
        public void OnUpdate(ref World world, float deltaTime) {
            for (int i = 0; i < count; i++) {
                ecbList.ElementAt(i).Playback(ref world);
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
        public void Execute() {
            ECB.Playback(ref world);
        }
    }
    [BurstCompile]
    internal struct ECBParallelJob : IJobParallelFor {
        internal EntityCommandBuffer ECB;
        internal World.WorldUnsafe worldUnsafe;
        internal World world;
        [NativeSetThreadIndex] private int threadIndex;
        public void Execute(int index) {
            ECB.PlaybackParallel(ref world, threadIndex);
            worldUnsafe.GetEntity(0);
        }
    }
    [BurstCompile]
    public struct EntityDestroySystem : IEntityJobSystem {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world) {
            return world.CreateQuery().With<DestroyEntity>();
        }
        public void OnUpdate(ref Entity entity, float deltaTime) {
            entity.Destroy();
        }
    }
    internal interface ISystemRunner {
        JobHandle Schedule(ref World world, float dt, ref JobHandle jobHandle);
        void Run(ref World world, float dt);
    }

    internal class EntityJobSystemRunner<TSystem> : ISystemRunner where TSystem : struct, IEntityJobSystem {
        public TSystem System;
        public Query Query;
        public SystemMode Mode;
        public ECBJob EcbJob;

        public JobHandle Schedule(ref World world, float dt, ref JobHandle jobHandle) {
            if (Mode == SystemMode.Main) {
                for (var i = 0; i < Query.Count; i++) {
                    System.OnUpdate(ref Query.GetEntity(i), dt);    
                }
                EcbJob.ECB = world.ECB;
                EcbJob.world = world;
                EcbJob.Run();
            }
            else {
                jobHandle = System.Schedule(ref Query, dt, Mode, jobHandle);
                EcbJob.ECB = world.ECB;
                EcbJob.world = world;
                jobHandle = EcbJob.Schedule(jobHandle);
            }
            return EcbJob.Schedule(jobHandle);
        }

        public void Run(ref World world, float dt) {
            for (int i = 0; i < Query.Count; i++) {
                System.OnUpdate(ref this.Query.GetEntity(i), dt);
            }
            world.ECB.Playback(ref world);
        }
    }
    
    internal class QueryJobSystemRunner<TSystem> : ISystemRunner where TSystem : struct, IQueryJobSystem {
        public TSystem System;
        public Query Query;
        public SystemMode Mode;
        public ECBJob EcbJob;

        public JobHandle Schedule(ref World world, float dt, ref JobHandle jobHandle) {

            if (Mode == SystemMode.Main) {
                System.OnUpdate(ref Query, dt);
                EcbJob.ECB = world.ECB;
                EcbJob.world = world;
                EcbJob.Run();
            }
            else {
                jobHandle = System.Schedule(ref Query, dt, Mode, jobHandle);
                EcbJob.ECB = world.ECB;
                EcbJob.world = world;
                jobHandle = EcbJob.Schedule(jobHandle);
            }
            return jobHandle;
        }

        public void Run(ref World world, float dt) {
            for (int i = 0; i < Query.Count; i++) {
                System.OnUpdate(ref Query, dt);
            }
            world.ECB.Playback(ref world);
        }
    }
    internal class EntityJobSystemRunner<TSystem,T1,T2> : ISystemRunner where TSystem : struct, IEntityJobSystem<T1,T2> 
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
    {
        public TSystem System;
        public Query Query;
        public SystemMode Mode;
        public ECBJob EcbJob;
        private GenericJobWrapper JobWrapper;
        public JobHandle Schedule(ref World world, float dt, ref JobHandle jobHandle) {
            if (Mode == SystemMode.Main) {
                for (var i = 0; i < Query.Count; i++) {
                    ref var e = ref Query.GetEntity(i);
                    System.OnUpdate(ref e, ref e.Get<T1>(), ref e.Get<T2>(), dt);    
                }
            }
            else {
                JobWrapper.c1pool = world.GetPool<T1>().AsComponentPool<T1>();
                JobWrapper.c2pool = world.GetPool<T2>().AsComponentPool<T2>();
                JobWrapper.query = Query;
                JobWrapper.dt = dt;
                JobWrapper.system = System;
                jobHandle = JobWrapper.Schedule(Query.Count, 1, jobHandle);
            }
            
            EcbJob.ECB = world.ECB;
            EcbJob.world = world;
            return EcbJob.Schedule(jobHandle);
        }
    
        public void Run(ref World world, float dt) {
            for (int i = 0; i < Query.Count; i++) {
                ref var e = ref Query.GetEntity(i);
                System.OnUpdate(ref e, ref e.Get<T1>(), ref e.Get<T2>(), dt);
            }
            world.ECB.Playback(ref world);
        }
        [BurstCompile]
        internal struct GenericJobWrapper : IJobParallelFor {
            public ComponentPool<T1> c1pool;
            public ComponentPool<T2> c2pool;
            public TSystem system;
            public Query query;
            public float dt;
            public void Execute(int index) {
                ref var e = ref query.GetEntity(index);
                system.OnUpdate(ref e, ref c1pool.Get(e.id), ref c2pool.Get(e.id), dt);
            }
        }
    }
    internal class QueryJobSystemRunner<TSystem,T> : ISystemRunner where TSystem : struct, IQueryJobSystem<T> 
        where T : struct, ITuple
    {
        public TSystem System;
        public Query<T> Query;
        public SystemMode Mode;
        public ECBJob EcbJob;
        private GenericJobWrapper JobWrapper;
        public JobHandle Schedule(ref World world, float dt, ref JobHandle jobHandle) {
            if (Mode == SystemMode.Main) {
                System.OnUpdate(Query);
            }
            else {

                JobWrapper.query = Query;
                JobWrapper.dt = dt;
                JobWrapper.system = System;
                jobHandle = JobWrapper.Schedule(Query.Count, 1, jobHandle);
            }
            
            EcbJob.ECB = world.ECB;
            EcbJob.world = world;
            return EcbJob.Schedule(jobHandle);
        }
    
        public void Run(ref World world, float dt) {

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
    internal class SystemJobRunner<TSystem> : ISystemRunner where TSystem : struct, IJobSystem {
        public TSystem System;
        public ECBJob EcbJob;

        public JobHandle Schedule(ref World world, float dt, ref JobHandle jobHandle) {
            System.Schedule(ref world, dt, jobHandle);
            EcbJob.ECB = world.ECB;
            EcbJob.world = world;
            return EcbJob.Schedule(jobHandle);
        }

        public void Run(ref World world, float dt) {
            System.OnUpdate(ref world, dt);
            world.ECB.Playback(ref world);
        }
    }
    internal class SystemMainThreadRunner<TSystem> : ISystemRunner where TSystem : struct, ISystem {
        internal TSystem System;
        internal ECBJob EcbJob;

        public JobHandle Schedule(ref World world, float dt, ref JobHandle jobHandle) {
            System.OnUpdate(ref world, dt);
            EcbJob.ECB = world.ECB;
            EcbJob.world = world;
            return EcbJob.Schedule(jobHandle);
        }

        public void Run(ref World world, float dt) {
            System.OnUpdate(ref world, dt);
            world.ECB.Playback(ref world);
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

    [JobProducerType(typeof(EntityJobSystemExtensions.EntityJobStruct<>))]
    public interface IEntityJobSystem {
        SystemMode Mode { get; }
        Query GetQuery(ref World world);
        void OnUpdate(ref Entity entity, float deltaTime);
    }
    public static class EntityJobSystemExtensions {
        [StructLayout(LayoutKind.Sequential)]
        internal struct EntityJobStruct<TJob> where TJob : struct, IEntityJobSystem {
            public TJob JobData;
            public Query query;
            public float deltaTime;

            internal static readonly SharedStatic<IntPtr> JobReflectionData =
                SharedStatic<IntPtr>.GetOrCreate<EntityJobStruct<TJob>>();

            [BurstDiscard]
            internal static void Initialize() {
                if (JobReflectionData.Data == IntPtr.Zero) {
                    JobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(EntityJobStruct<TJob>),
                        typeof(TJob), (object) new EntityJobSystemExtensions.EntityJobStruct<TJob>.ExecuteJobFunction(EntityJobSystemExtensions.EntityJobStruct<TJob>.Execute));
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
                   
                    for (var i = begin; i < end; i++) {
                        unsafe {
                            fullData.JobData.OnUpdate(ref fullData.query.impl->GetEntity(i), fullData.deltaTime);
                        }
                    }
                }
            }
        }


        public static void EarlyJobInit<T>() where T : struct, IEntityJobSystem {
            EntityJobStruct<T>.Initialize();
        }

        private static IntPtr GetReflectionData<T>() where T : struct, IEntityJobSystem {
            EntityJobStruct<T>.Initialize();
            return EntityJobStruct<T>.JobReflectionData.Data;
        }

        public static unsafe JobHandle Schedule<TJob>(this TJob jobData, ref Query query, float deltaTime,
            SystemMode mode, JobHandle dependsOn = default)
            where TJob : struct, IEntityJobSystem {
            var fullData = new EntityJobStruct<TJob> {
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
                    return JobsUtility.ScheduleParallelFor(ref scheduleParams, query.Count, 1);
            }
            //var workers = JobsUtility.JobWorkerCount;
            //var batchCount = query.Count > workers ? query.Count / workers : 1;
            return dependsOn;
        }
        
        public static unsafe void Run<TJob>(this TJob jobData, ref Query query, float deltaTime) where TJob : struct, IEntityJobSystem
        {
            var fullData = new EntityJobStruct<TJob> {
                JobData = jobData,
                query = query,
                deltaTime = deltaTime
            };
            JobsUtility.JobScheduleParameters parameters = new JobsUtility.JobScheduleParameters(
                UnsafeUtility.AddressOf(ref fullData),
                GetReflectionData<TJob>(),
            new JobHandle(), 
                ScheduleMode.Run);
            JobsUtility.Schedule(ref parameters);
        }
    }

    [JobProducerType(typeof(JobSystemExtensions.JobSystemWrapper<>))]
    public interface IJobSystem {
        void OnUpdate(ref World world, float deltaTime);
    }
    
    public static class JobSystemExtensions {
        [StructLayout(LayoutKind.Sequential)]
        public struct JobSystemWrapper<TJob> where TJob : struct, IJobSystem {
            public TJob JobData;
            public World world;
            public float deltaTime;

            internal static readonly SharedStatic<IntPtr> JobReflectionData =
                SharedStatic<IntPtr>.GetOrCreate<JobSystemWrapper<TJob>>();

            [BurstDiscard]
            internal static void Initialize() {
                if (JobReflectionData.Data == IntPtr.Zero) {
                    JobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(JobSystemWrapper<TJob>),
                        typeof(TJob), (ExecuteJobFunction) Execute);
                }
            }

            private delegate void ExecuteJobFunction(ref JobSystemWrapper<TJob> fullData, IntPtr additionalPtr,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);

            public static void Execute(ref JobSystemWrapper<TJob> fullData, IntPtr additionalPtr,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {
                while (true) {
                    if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end))
                        return;

                    for (var i = begin; i < end; i++) {
                        fullData.JobData.OnUpdate(ref fullData.world, fullData.deltaTime);
                    }
                }
            }
        }


        public static void EarlyJobInit<T>() where T : struct, IJobSystem {
            JobSystemWrapper<T>.Initialize();
        }

        private static IntPtr GetReflectionData<T>() where T : struct, IJobSystem {
            JobSystemWrapper<T>.Initialize();
            return JobSystemWrapper<T>.JobReflectionData.Data;
        }

        public static unsafe JobHandle Schedule<TJob>(this TJob jobData, ref World world, float deltaTime,
            JobHandle dependsOn = default)
            where TJob : struct, IJobSystem {
            var fullData = new JobSystemWrapper<TJob> {
                JobData = jobData,
                world = world,
                deltaTime = deltaTime
            };

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData),
                GetReflectionData<TJob>(), dependsOn, ScheduleMode.Parallel);

            return JobsUtility.ScheduleParallelFor(ref scheduleParams, 100, 1);
        }
        public static unsafe void Run<TJob>(this TJob jobData, ref World world, float deltaTime) where TJob : struct, IJobSystem
        {
            var fullData = new JobSystemWrapper<TJob> {
                JobData = jobData,
                world = world,
                deltaTime = deltaTime
            };
            JobsUtility.JobScheduleParameters parameters = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData),
                JobSystemExtensions.GetReflectionData<TJob>(),
                new JobHandle(), 
                ScheduleMode.Run);
            JobsUtility.Schedule(ref parameters);
        }
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
    // [JobProducerType(typeof(EntityJobSystemT2Extensions.EntityJobStruct<,,>))]
    public interface IEntityJobSystem<T1,T2> where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent{
        SystemMode Mode { get; }
        Query GetQuery(ref World world);
        void OnUpdate(ref Entity entity, ref T1 c1, ref T2 c2, float dt);
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
    public interface ISystem {
        void OnUpdate(ref World world, float deltaTime);
    }
    [JobProducerType(typeof(EntityIndexJobSystemExtensions<>.EntityJobStruct<>))]
    public interface IEntityIndexJobSystem
    {
        SystemMode Mode { get; }
        void OnUpdate<T1>(ref T1 c1, float dt) where T1 : unmanaged, IComponent;
    }
    public static class EntityIndexJobSystemExtensions<T1> where T1 : unmanaged, IComponent {
        [StructLayout(LayoutKind.Sequential)]
        internal struct EntityJobStruct<TJob> where TJob : struct, IEntityIndexJobSystem {
            public TJob JobData;
            public Query query;
            public World world;
            public float deltaTime;

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
                    ref var c1pool = ref fullData.world.GetPool<T1>();
                    for (var i = begin; i < end; i++) {
                        unsafe
                        {
                            var e = fullData.query.impl->GetEntityID(i);
                            fullData.JobData.OnUpdate(ref c1pool.GetRef<T1>(e), fullData.deltaTime);
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
        public static unsafe JobHandle Schedule<TJob,T>(this TJob jobData, ref Query query, float deltaTime,
            SystemMode mode, JobHandle dependsOn = default)
            where TJob : struct, IEntityIndexJobSystem 
            where T : unmanaged, IComponent {
            var fullData = new EntityIndexJobSystemExtensions<T>.EntityJobStruct<TJob> {
                JobData = jobData,
                query = query,
                deltaTime = deltaTime
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
}
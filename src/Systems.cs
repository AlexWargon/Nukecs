using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Wargon.Nukecs {
    public unsafe struct Systems {
        internal JobHandle dependencies;
        private List<ISystemRunner> runners;
        private World world;

        public Systems(ref World world) {
            this.dependencies = default;
            this.runners = new List<ISystemRunner>();
            this.world = world;
        }

        public Systems Add<T>() where T : struct, IJobSystem {
            T system = default;
            if (system is ICreate s) {
                s.OnCreate(ref world);
                system = (T) s;
            }
            runners.Add(new SystemJobRunner<T> {
                System = system,
                EcbJob = default
            });
            return this;
        }

        public Systems Add<T>(bool dymmy = false) where T : struct, IEntityJobSystem {
            T system = default;
            if (system is ICreate s) {
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
        
        public Systems Add<T>(float dymmy = 1f) where T : struct, IQueryJobSystem {
            T system = default;
            if (system is ICreate s) {
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
        
        [BurstCompile(CompileSynchronously = true)]
        internal struct WarmupJob : IJob
        {
            public void Execute() {}
        }
        // public Systems Add<TSystem,T1,T2>(byte dymmy = 1) where TSystem : struct, IEntityJobSystem<T1,T2> where T1 : unmanaged where T2 : unmanaged{
        //     TSystem system = default;
        //     if (system is ICreate s) {
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
            if (system is ICreate s) {
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
            dependencies.Complete();
            for (var i = 0; i < runners.Count; i++) {
                dependencies = runners[i].Schedule(ref world, dt, ref dependencies);
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
    // internal class EntityJobSystemRunner<TSystem,T1,T2> : ISystemRunner where TSystem : struct, IEntityJobSystem<T1,T2> where T1 : unmanaged where T2 : unmanaged{
    //     public TSystem System;
    //     public Query Query;
    //     public SystemMode Mode;
    //     public ECBJob EcbJob;
    //
    //     public JobHandle Schedule(ref World world, float dt, ref JobHandle jobHandle) {
    //         if (Mode == SystemMode.Main) {
    //             for (var i = 0; i < Query.Count; i++) {
    //                 ref var e = ref Query.GetEntity(i);
    //                 System.OnUpdate(ref e, ref e.Get<T1>(), ref e.Get<T2>(), dt);    
    //             }
    //
    //             
    //         }
    //         else {
    //             jobHandle = System.Schedule<TSystem,T1,T2>(ref Query, dt, Mode, jobHandle);    
    //         }
    //         
    //         EcbJob.ECB = world.ECB;
    //         EcbJob.world = world;
    //         return EcbJob.Schedule(jobHandle);
    //     }
    //
    //     public void Run(ref World world, float dt) {
    //         for (int i = 0; i < Query.Count; i++) {
    //             ref var e = ref this.Query.GetEntity(i);
    //             System.OnUpdate(ref e, ref e.Get<T1>(), ref e.Get<T2>(), dt);
    //         }
    //         world.ECB.Playback(ref world);
    //     }
    // }
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
    // public interface IEntityJobSystem<T1,T2> where T1 : unmanaged where T2 : unmanaged{
    //     SystemMode Mode { get; }
    //     Query GetQuery(ref World world);
    //     void OnUpdate(ref Entity entity, ref T1 c1, ref T2 c2, float dt);
    // }
    // public static class EntityJobSystemT2Extensions {
    //     [StructLayout(LayoutKind.Sequential)]
    //     internal struct EntityJobStruct<TJob,T1,T2> where TJob : struct, IEntityJobSystem<T1,T2>  
    //         where T1 : unmanaged 
    //         where T2 : unmanaged
    //     {
    //         public TJob JobData;
    //         public Query query;
    //         public float deltaTime;
    //
    //         internal static readonly SharedStatic<IntPtr> JobReflectionData =
    //             SharedStatic<IntPtr>.GetOrCreate<EntityJobStruct<TJob,T1,T2>>();
    //
    //         [BurstDiscard]
    //         internal static void Initialize() {
    //             if (JobReflectionData.Data == IntPtr.Zero) {
    //                 JobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(EntityJobStruct<TJob,T1,T2>),
    //                     typeof(TJob), (object) new ExecuteJobFunction(Execute));
    //             }
    //         }
    //
    //         private delegate void ExecuteJobFunction(ref EntityJobStruct<TJob,T1,T2> fullData, IntPtr additionalPtr,
    //             IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);
    //         
    //         public static void Execute(ref EntityJobStruct<TJob, T1,T2> fullData, IntPtr additionalPtr,
    //             IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {
    //             if(fullData.query.Count == 0) return;
    //             while (true) {
    //                 if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end))
    //                     break;
    //                 //JobsUtility.PatchBufferMinMaxRanges(bufferRangePatchData, UnsafeUtility.AddressOf<TJob>(ref fullData.JobData), begin, end - begin);
    //                
    //                 for (var i = begin; i < end; i++) {
    //                     ref var e = ref fullData.query.GetEntity(i);
    //                     fullData.JobData.OnUpdate(ref e, ref e.Get<T1>(), ref e.Get<T2>(), fullData.deltaTime);
    //                 }
    //             }
    //         }
    //     }
    //
    //
    //     public static void EarlyJobInit<TJob,T1,T2>() where TJob : struct, IEntityJobSystem<T1,T2> where T1 : unmanaged where T2 : unmanaged {
    //         EntityJobStruct<TJob,T1,T2>.Initialize();
    //     }
    //
    //     private static IntPtr GetReflectionData<TJob, T1,T2>() where TJob : struct, IEntityJobSystem<T1,T2> where T1 : unmanaged where T2 : unmanaged {
    //         EntityJobStruct<TJob,T1,T2>.Initialize();
    //         return EntityJobStruct<TJob, T1,T2>.JobReflectionData.Data;
    //     }
    //
    //     public static unsafe JobHandle Schedule<TJob,T1,T2>(this TJob jobData, ref Query query, float deltaTime,
    //         SystemMode mode, JobHandle dependsOn = default)
    //         where TJob : struct, IEntityJobSystem<T1,T2> where T1 : unmanaged where T2 : unmanaged {
    //         var fullData = new EntityJobStruct<TJob,T1,T2> {
    //             JobData = jobData,
    //             query = query,
    //             deltaTime = deltaTime
    //         };
    //
    //         var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData),
    //             GetReflectionData<TJob,T1,T2>(), dependsOn,
    //             mode == SystemMode.Parallel ? ScheduleMode.Parallel : ScheduleMode.Single);
    //         //var workers = JobsUtility.JobWorkerCount;
    //         //var batchCount = query.Count > workers ? query.Count / workers : 1;
    //         return JobsUtility.ScheduleParallelFor(ref scheduleParams, query.Count, 1);
    //     }
    //     
    //     public static unsafe void Run<TJob,T1,T2>(this TJob jobData, ref Query query, float deltaTime) 
    //         where TJob : struct, IEntityJobSystem<T1,T2> where T1 : unmanaged where T2 : unmanaged
    //     {
    //         var fullData = new EntityJobStruct<TJob,T1,T2> {
    //             JobData = jobData,
    //             query = query,
    //             deltaTime = deltaTime
    //         };
    //         JobsUtility.JobScheduleParameters parameters = new JobsUtility.JobScheduleParameters(
    //             UnsafeUtility.AddressOf(ref fullData),
    //             GetReflectionData<TJob,T1,T2>(),
    //         new JobHandle(), 
    //             ScheduleMode.Run);
    //         JobsUtility.Schedule(ref parameters);
    //     }
    // }
    
    public interface ICreate {
        void OnCreate(ref World world);
    }
    public interface ISystem {
        void OnUpdate(ref World world, float deltaTime);
    }
}
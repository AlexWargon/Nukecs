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
    public struct ECBParallelJob : IJobParallelFor {
        public EntityCommandBuffer ECB;
        public World world;
        [NativeSetThreadIndex] private int threadIndex;
        public void Execute(int index) {
            ECB.PlaybackParallel(ref world, threadIndex);
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
            jobHandle = System.Schedule(ref Query, dt, Mode, jobHandle);
            EcbJob.ECB = world.ECB;
            EcbJob.world = world;
            return EcbJob.Schedule(jobHandle);
        }

        public void Run(ref World world, float dt) {
            for (int i = 0; i < Query.Count; i++) {
                System.OnUpdate(ref this.Query.GetEntity(i), dt);
            }
            world.ECB.Playback(ref world);
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
    public enum SystemMode {
        Single,
        Parallel,
        Main,
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

            public unsafe static void Execute(ref EntityJobStruct<TJob> fullData, IntPtr additionalPtr,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {
                if(fullData.query.Count == 0) return;
                while (true) {
                    if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end))
                        break;
                    JobsUtility.PatchBufferMinMaxRanges(bufferRangePatchData, UnsafeUtility.AddressOf<TJob>(ref fullData.JobData), begin, end - begin);
                   
                    for (var i = begin; i < end; i++) {
                        fullData.JobData.OnUpdate(ref fullData.query.GetEntity(i), fullData.deltaTime);
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

            return JobsUtility.ScheduleParallelFor(ref scheduleParams, query.Count, query.Count);
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
    public interface ICreate {
        void OnCreate(ref World world);
    }
    public interface ISystem : ISystemBase {
        void OnUpdate(ref World world, float deltaTime);
    }
    
    public interface ISystemBase {}
}
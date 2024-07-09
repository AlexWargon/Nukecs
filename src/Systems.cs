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

            runners.Add(new SystemRunner<T> {
                system = system,
                ecbJob = default
            });
            return this;
        }

        public Systems Add<T>(bool dymmy = false) where T : struct, IEntityJobSystem {
            T system = default;
            if (system is ICreate s) {
                s.OnCreate(ref world);
                system = (T) s;
            }
            
            runners.Add(new EntitySystemRunner<T> {
                System = system,
                Query = system.GetQuery(ref world),
                Mode = system.Mode,
                EcbJob = default
            });
            return this;
        }

        public void OnUpdate(float dt) {
            
            dependencies.Complete();
            for (var i = 0; i < runners.Count; i++) {
                dependencies = runners[i].OnUpdate(ref world, dt, ref dependencies);
            }
        }
    }

    [BurstCompile]
    public struct ECBJob : IJob {
        public EntityCommandBuffer ECB;
        public World world;

        public void Execute() {
            ECB.PerformCommand(ref world);
        }
    }

    internal interface ISystemRunner {
        JobHandle OnUpdate(ref World world, float dt, ref JobHandle jobHandle);
    }

    internal class EntitySystemRunner<TSystem> : ISystemRunner where TSystem : struct, IEntityJobSystem {
        public TSystem System;
        public Query Query;
        public SystemMode Mode;
        public ECBJob EcbJob;

        public JobHandle OnUpdate(ref World world, float dt, ref JobHandle jobHandle) {
            jobHandle = System.Schedule(ref Query, dt, Mode, jobHandle);
            EcbJob.world = world;
            EcbJob.ECB = world.ECB;
            return EcbJob.Schedule(jobHandle);
        }
    }

    internal class SystemRunner<TSystem> : ISystemRunner where TSystem : struct, IJobSystem {
        public TSystem system;
        public ECBJob ecbJob;

        public JobHandle OnUpdate(ref World world, float dt, ref JobHandle jobHandle) {
            jobHandle = system.Schedule(ref world, dt, jobHandle);
            ecbJob.world = world;
            ecbJob.ECB = world.ECB;
            return ecbJob.Schedule(jobHandle);
        }
    }

    public enum SystemMode {
        Single,
        Parallel
    }

    [JobProducerType(typeof(EntityJobSystemExt.JobSystemWrapper<>))]
    public interface IEntityJobSystem {
        SystemMode Mode { get; }
        Query GetQuery(ref World world);
        void OnUpdate(ref Entity entity, float deltaTime);
    }
    public static class EntityJobSystemExt {
        [StructLayout(LayoutKind.Sequential)]
        public struct JobSystemWrapper<TJob> where TJob : struct, IEntityJobSystem {
            public TJob JobData;
            public Query query;
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
                    
                    if(fullData.query.Count == 0) return;
                    for (var i = begin; i < end; i++) {
                        ref var query = ref fullData.query;
                        fullData.JobData.OnUpdate(ref query.GetEntity(i), fullData.deltaTime);
                    }
                }
            }
        }


        public static void EarlyJobInit<T>() where T : struct, IEntityJobSystem {
            JobSystemWrapper<T>.Initialize();
        }

        private static IntPtr GetReflectionData<T>() where T : struct, IEntityJobSystem {
            JobSystemWrapper<T>.Initialize();
            return JobSystemWrapper<T>.JobReflectionData.Data;
        }

        public static unsafe JobHandle Schedule<TJob>(this TJob jobData, ref Query query, float deltaTime,
            SystemMode mode, JobHandle dependsOn = default)
            where TJob : struct, IEntityJobSystem {
            var fullData = new JobSystemWrapper<TJob> {
                JobData = jobData,
                query = query,
                deltaTime = deltaTime
            };

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData),
                GetReflectionData<TJob>(), dependsOn,
                mode == SystemMode.Parallel ? ScheduleMode.Parallel : ScheduleMode.Single);

            return JobsUtility.ScheduleParallelFor(ref scheduleParams, query.Count, 1);
        }
    }

    [JobProducerType(typeof(JobSystemExt.JobSystemWrapper<>))]
    public interface IJobSystem {
        void OnUpdate(ref World world, float deltaTime);
    }

    public static class JobSystemExt {
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
    }
}
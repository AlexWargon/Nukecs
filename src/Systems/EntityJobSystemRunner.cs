using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    internal class EntityJobSystemRunner<TSystem> : ISystemRunner where TSystem : struct, IEntityJobSystem {
        public TSystem System;
        public Query Query;
        public SystemMode Mode;
        public ECBJob EcbJob;

        public JobHandle Schedule(ref World world, float dt, ref JobHandle jobHandle, UpdateContext updateContext) {
            if (Mode == SystemMode.Main) {
                world.CurrentContext = updateContext;
                for (var i = 0; i < Query.Count; i++) {
                    System.OnUpdate(ref Query.GetEntity(i), dt);    
                }
                EcbJob.ECB = world.GetEcbVieContext(updateContext);
                EcbJob.world = world;
                EcbJob.Run();
            }
            else {
                jobHandle = System.Schedule(ref Query, dt, Mode, updateContext, ref world, jobHandle);
                EcbJob.ECB = world.GetEcbVieContext(updateContext);
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
    [JobProducerType(typeof(EntityJobSystemExtensions.EntityJobWrapper<>))]
    public interface IEntityJobSystem {
        SystemMode Mode { get; }
        Query GetQuery(ref World world);
        void OnUpdate(ref Entity entity, float deltaTime);
    }
    public static class EntityJobSystemExtensions {
        [StructLayout(LayoutKind.Sequential)]
        internal struct EntityJobWrapper<TJob> where TJob : struct, IEntityJobSystem {
            public TJob JobData;
            public Query query;
            public float deltaTime;
            public UpdateContext updateContext;
            public World world;
            internal static readonly SharedStatic<IntPtr> JobReflectionData =
                SharedStatic<IntPtr>.GetOrCreate<EntityJobWrapper<TJob>>();

            [BurstDiscard]
            internal static void Initialize() {
                if (JobReflectionData.Data == IntPtr.Zero) {
                    JobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(EntityJobWrapper<TJob>),
                        typeof(TJob), (object) new EntityJobSystemExtensions.EntityJobWrapper<TJob>.ExecuteJobFunction(EntityJobSystemExtensions.EntityJobWrapper<TJob>.Execute));
                }
            }

            private delegate void ExecuteJobFunction(ref EntityJobWrapper<TJob> fullData, IntPtr additionalPtr,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);
            
            public static void Execute(ref EntityJobWrapper<TJob> fullData, IntPtr additionalPtr,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {
                if(fullData.query.Count == 0) return;
                while (true) {
                    if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end))
                        break;
                    //JobsUtility.PatchBufferMinMaxRanges(bufferRangePatchData, UnsafeUtility.AddressOf<TJob>(ref fullData.JobData), begin, end - begin);
                    fullData.world.CurrentContext = fullData.updateContext;
                    for (var i = begin; i < end; i++) {
                        unsafe {
                            fullData.JobData.OnUpdate(ref fullData.query.impl->GetEntity(i), fullData.deltaTime);
                        }
                    }
                }
            }
        }


        public static void EarlyJobInit<T>() where T : struct, IEntityJobSystem {
            EntityJobWrapper<T>.Initialize();
        }

        private static IntPtr GetReflectionData<T>() where T : struct, IEntityJobSystem {
            EntityJobWrapper<T>.Initialize();
            return EntityJobWrapper<T>.JobReflectionData.Data;
        }

        public static unsafe JobHandle Schedule<TJob>(this TJob jobData, ref Query query, float deltaTime,
            SystemMode mode, UpdateContext updateContext, ref World world, JobHandle dependsOn = default)
            where TJob : struct, IEntityJobSystem {
            var fullData = new EntityJobWrapper<TJob> {
                JobData = jobData,
                query = query,
                deltaTime = deltaTime,
                updateContext = updateContext,
                world = world
                
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
            var fullData = new EntityJobWrapper<TJob> {
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
}
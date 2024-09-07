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

        public JobHandle Schedule(UpdateContext updateContext, ref State state)
        {
            ref var world = ref state.World;
            if (Mode == SystemMode.Main) {
                world.CurrentContext = updateContext;
                for (var i = 0; i < Query.Count; i++) {
                    System.OnUpdate(ref Query.GetEntity(i), ref state);    
                }
                EcbJob.ECB = world.GetEcbVieContext(updateContext);
                EcbJob.world = world;
                EcbJob.Run();
                return state.Dependencies;
            }
            state.Dependencies = System.Schedule(ref Query, Mode, updateContext, ref state);
            EcbJob.ECB = world.GetEcbVieContext(updateContext);
            EcbJob.world = world;
            return EcbJob.Schedule(state.Dependencies);
        }

        public void Run(ref State state) {
            for (int i = 0; i < Query.Count; i++) {
                System.OnUpdate(ref this.Query.GetEntity(i), ref state);
            }
            state.World.ECB.Playback(ref state.World);
        }
    }
    [JobProducerType(typeof(EntityJobSystemExtensions.EntityJobWrapper<>))]
    public interface IEntityJobSystem {
        SystemMode Mode { get; }
        Query GetQuery(ref World world);
        void OnUpdate(ref Entity entity, ref State state);
    }
    public static class EntityJobSystemExtensions {
        [StructLayout(LayoutKind.Sequential)]
        internal struct EntityJobWrapper<TJob> where TJob : struct, IEntityJobSystem {
            public TJob JobData;
            public Query query;
            public UpdateContext updateContext;
            public State State;
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
                switch (fullData.JobData.Mode) {
                    case SystemMode.Parallel:
                        while (true) {
                            if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end))
                                break;
                            //JobsUtility.PatchBufferMinMaxRanges(bufferRangePatchData, UnsafeUtility.AddressOf<TJob>(ref fullData.JobData), begin, end - begin);
                            fullData.State.World.CurrentContext = fullData.updateContext;
                            for (var i = begin; i < end; i++) {
                                unsafe {
                                    ref var e = ref fullData.query.impl->GetEntity(i);
                                    if (e != Entity.Null) {
                                        fullData.JobData.OnUpdate(ref fullData.query.impl->GetEntity(i), ref fullData.State);
                                    }
                                }
                            }
                        }
                        break;
                    case SystemMode.Single:
                        for (var i = 0; i < fullData.query.Count; i++) {
                            unsafe {
                                ref var e = ref fullData.query.impl->GetEntity(i);
                                if (e != Entity.Null) {
                                    fullData.JobData.OnUpdate(ref fullData.query.impl->GetEntity(i), ref fullData.State);
                                }
                            }
                        }
                        break;
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

        public static unsafe JobHandle Schedule<TJob>(this TJob jobData, ref Query query,
            SystemMode mode, UpdateContext updateContext, ref State state)
            where TJob : struct, IEntityJobSystem {
            var fullData = new EntityJobWrapper<TJob> {
                JobData = jobData,
                query = query,
                updateContext = updateContext,
                State = state
            };
            
            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData),
                GetReflectionData<TJob>(), state.Dependencies,
                mode == SystemMode.Parallel ? ScheduleMode.Parallel : ScheduleMode.Single);
            switch (mode) {
                case SystemMode.Single:
                    return JobsUtility.Schedule(ref scheduleParams);
                case SystemMode.Parallel:
                    return JobsUtility.ScheduleParallelFor(ref scheduleParams, query.Count, 1);
            }
            //var workers = JobsUtility.JobWorkerCount;
            //var batchCount = query.Count > workers ? query.Count / workers : 1;
            return state.Dependencies;
        }
        
        public static unsafe void Run<TJob>(this TJob jobData, ref Query query, float deltaTime) where TJob : struct, IEntityJobSystem
        {
            var fullData = new EntityJobWrapper<TJob> {
                JobData = jobData,
                query = query,
                //deltaTime = deltaTime
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
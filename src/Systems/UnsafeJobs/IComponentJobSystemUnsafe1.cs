using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Wargon.Nukecs {
    [JobProducerType(typeof(IComponentJobSystemUnsafe1Extensions.EntityJobWrapper<>))]
    public unsafe interface IComponentJobSystemUnsafe1 {
        public void OnUpdate(ref Entity entity, void* c1, ref State state);
    }
    public static class IComponentJobSystemUnsafe1Extensions 
    {
        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct EntityJobWrapper<TJob> where TJob : struct, IComponentJobSystemUnsafe1 
        {
            public int c1;
            public TJob JobData;
            public Query query;
            public UpdateContext updateContext;
            public State State;
            public World.WorldUnsafe* world;
            internal static readonly SharedStatic<IntPtr> JobReflectionData =
                SharedStatic<IntPtr>.GetOrCreate<EntityJobWrapper<TJob>>();
        
        [BurstDiscard]
        internal static void Initialize() {
            if (JobReflectionData.Data == IntPtr.Zero) {
                JobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(EntityJobWrapper<TJob>),
                    typeof(TJob), (ExecuteJobFunction)Execute);
            }
        }

        private delegate void ExecuteJobFunction(ref EntityJobWrapper<TJob> fullData, IntPtr additionalPtr,
            IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex, SystemMode mode);
        
        public static void Execute(ref EntityJobWrapper<TJob> fullData, IntPtr additionalPtr,
            IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex, SystemMode mode) {
            if(fullData.query.Count == 0) return;
            ref var pool1 = ref fullData.world->GetUntypedPool(fullData.c1);
            switch (mode) {
                case SystemMode.Parallel:
                    while (true) {
                        if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end))
                            break;
                        JobsUtility.PatchBufferMinMaxRanges(bufferRangePatchData, UnsafeUtility.AddressOf<TJob>(ref fullData.JobData), begin, end - begin);
                        for (var i = begin; i < end; i++) {
                            ref var e = ref fullData.query.impl->GetEntity(i);
                            if (e != Entity.Null) {
                                fullData.JobData.OnUpdate(ref e, pool1.GetUnsafePtr(e.id), ref fullData.State);
                            }
                        }
                    }
                    break;
                case SystemMode.Single:
                    for (var i = 0; i < fullData.query.Count; i++) {
                        ref var e = ref fullData.query.impl->GetEntity(i);
                        if (e != Entity.Null) {
                            fullData.JobData.OnUpdate(ref e, pool1.GetUnsafePtr(e.id), ref fullData.State);
                        }
                    }
                    break;
                }
            }
        }


        public static void EarlyJobInit<T>() where T : struct, IComponentJobSystemUnsafe1 {
            EntityJobWrapper<T>.Initialize();
        }

        private static IntPtr GetReflectionData<T>() where T : struct, IComponentJobSystemUnsafe1 {
            EntityJobWrapper<T>.Initialize();
            return EntityJobWrapper<T>.JobReflectionData.Data;
        }

        public static unsafe JobHandle Schedule<TJob>(this TJob jobData, ref Query query,
            SystemMode mode, UpdateContext updateContext, ref State state)
            where TJob : struct, IComponentJobSystemUnsafe1 {
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
        
        public static unsafe void Run<TJob>(this TJob jobData, ref Query query, float deltaTime) where TJob : struct, IComponentJobSystemUnsafe1
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
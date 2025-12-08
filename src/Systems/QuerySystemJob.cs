using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Wargon.Nukecs.Transforms;

namespace Wargon.Nukecs
{
    [JobProducerType(typeof(IQuerySystemJobExtensions.QuerySystemJobWrapper<>))]
    public interface IQuerySystemJob {
        void OnUpdate(Query<Transform, Input>.WithEntity query);
    }
    // ReSharper disable once InconsistentNaming
    public static class IQuerySystemJobExtensions {
        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct QuerySystemJobWrapper<TJob> where TJob : struct, IQuerySystemJob {
            public TJob JobData;
            public SystemMode mode;
            [NativeDisableUnsafePtrRestriction]
            public Query<Transform, Input>.WithEntity* query;
            public State State;
            internal static readonly SharedStatic<IntPtr> JobReflectionData =
                SharedStatic<IntPtr>.GetOrCreate<QuerySystemJobWrapper<TJob>>();

            [BurstDiscard]
            internal static void Initialize() {
                if (JobReflectionData.Data == IntPtr.Zero) {
                    JobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(QuerySystemJobWrapper<TJob>),
                        typeof(TJob), (ExecuteJobFunction)Execute);
                }
            }

            private delegate void ExecuteJobFunction(ref QuerySystemJobWrapper<TJob> fullData, IntPtr additionalPtr,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);
            
            public static void Execute(ref QuerySystemJobWrapper<TJob> fullData, IntPtr additionalPtr,
                IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {
                if(fullData.query->Count == 0) return;
                Range range;
                switch (fullData.mode) {
                    case SystemMode.Parallel:
                        while (true) {
                            if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end))
                                break;
                            //dbug.log($"PER THREAD {(thead : jobIndex, from : begin, to : end)}");
                            range = new Range(begin, end);
                            var copy = *fullData.query;
                            copy.Update(ref fullData.State.World, (IntPtr)UnsafeUtility.AddressOf(ref range));
                            fullData.JobData.OnUpdate(copy);
                        }
                        break;
                    case SystemMode.Single:
                        range = new Range(0, fullData.query->Count);
                        //dbug.log($"SINGLE {(0, fullData.query->count)}");
                        fullData.query->Update(ref fullData.State.World, (IntPtr)UnsafeUtility.AddressOf(ref range));

                        fullData.JobData.OnUpdate(*fullData.query);
                        break;
                }
            }
        }


        public static void EarlyJobInit<T>() where T : struct, IQuerySystemJob {
            QuerySystemJobWrapper<T>.Initialize();
        }

        private static IntPtr GetReflectionData<T>() where T : struct, IQuerySystemJob {
            QuerySystemJobWrapper<T>.Initialize();
            return QuerySystemJobWrapper<T>.JobReflectionData.Data;
        }

        internal static unsafe JobHandle Schedule<TJob>(this TJob jobData, ptr<Query<Transform, Input>.WithEntity> q,
            SystemMode mode, ref State state)
            where TJob : struct, IQuerySystemJob {
            var fullData = new QuerySystemJobWrapper<TJob> {
                JobData = jobData,
                query = q.Ptr,
                State = state,
                mode = mode
            };
            
            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData),
                GetReflectionData<TJob>(), state.Dependencies,
                mode == SystemMode.Parallel ? ScheduleMode.Parallel : ScheduleMode.Single);
            var workers = JobsUtility.JobWorkerCount;
            var batchCount = fullData.query->Count > workers ? fullData.query->Count / workers : 1;
            switch (mode) {
                case SystemMode.Single:
                    return JobsUtility.Schedule(ref scheduleParams);
                case SystemMode.Parallel:
                    return JobsUtility.ScheduleParallelFor(ref scheduleParams, fullData.query->Count, batchCount);
            }

            return state.Dependencies;
        }
        
        public static unsafe void Run<TJob>(this TJob jobData, ref Query query, float deltaTime) 
            where TJob : struct, IQuerySystemJob
        {
            var fullData = new QuerySystemJobWrapper<TJob> {
                JobData = jobData,
                //query = query,
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

    public class IQuerySystemJobRunner<TJob> : ISystemRunner where TJob : struct, IQuerySystemJob
    {
        public TJob System;
        public ptr<Query<Transform, Input>.WithEntity> Query;
        public SystemMode Mode;
        public ECBJob EcbJob;
        public string Name => System.GetType().Name;
        public JobHandle Schedule(UpdateContext updateContext, ref State state)
        {
            ref var world = ref state.World;
            if (Mode == SystemMode.Main) {
                System.OnUpdate(Query.Ref);
                EcbJob.ECB = world.GetEcbVieContext(updateContext);
                EcbJob.world = world;
                EcbJob.Execute();
            }
            else {
                state.Dependencies = System.Schedule(Query, Mode, ref state);
                EcbJob.ECB = world.GetEcbVieContext(updateContext);
                EcbJob.world = world;
                state.Dependencies = EcbJob.Schedule(state.Dependencies);
            }
            return state.Dependencies;
        }

        public void Run(ref State state) {
            for (int i = 0; i < Query.Ref.Count; i++) {
                System.OnUpdate(Query.Ref);
            }
            state.World.ECB.Playback(ref state.World);
        }
    }
    public static class QSJobExtensions
    {
        public static Systems AddSystem14<TSystem>(this Systems systems) where TSystem : struct, IQuerySystemJob
        {
            TSystem system = default;

            var runner = new IQuerySystemJobRunner<TSystem>
            {
                System = system,
                Mode = SystemMode.Parallel,
                EcbJob = default,
                Query = systems.World.UnsafeWorldRef.GetSystemParam2<Query<Transform, Input>.WithEntity>()
            };
            systems.runners.Add(runner);
            return systems;
        }
    }

}
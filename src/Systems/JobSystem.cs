using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    internal class JobSystemRunner<TSystem> : ISystemRunner where TSystem : struct, IJobSystem {
        public TSystem System;
        public ECBJob EcbJob;
        public bool isComplete;
        public string Name => System.GetType().Name;
        public JobHandle Schedule(UpdateContext updateContext, ref State state) {
            System.Schedule(SystemMode.Single, updateContext, ref state);
            if(isComplete) state.Dependencies.Complete();
            EcbJob.ECB = state.World.GetEcbVieContext(updateContext);
            EcbJob.world = state.World;
            return EcbJob.Schedule(state.Dependencies);
        }

        public void Run(ref State state) {
            System.OnUpdate(ref state);
            state.World.ECB.Playback(ref state.World);
        }
    }
    /// <summary>
    /// Run on single thread. Can be bursted
    /// </summary>
    [JobProducerType(typeof(JobSystemExtensions.JobSystemWrapper<>))]
    public interface IJobSystem {
        void OnUpdate(ref State state);
    }
    
    public static class JobSystemExtensions {
        [StructLayout(LayoutKind.Sequential)]
        public struct JobSystemWrapper<TJob> where TJob : struct, IJobSystem {
            public TJob JobData;
            public State State;
            public UpdateContext UpdateContext;
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
                fullData.JobData.OnUpdate(ref fullData.State);
                // return;
                // while (true) {
                //     if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end))
                //         return;
                //
                //     for (var i = begin; i < end; i++) {
                //         fullData.JobData.OnUpdate(ref fullData.State);
                //     }
                // }
            }
        }


        public static void EarlyJobInit<T>() where T : struct, IJobSystem {
            JobSystemWrapper<T>.Initialize();
        }

        private static IntPtr GetReflectionData<T>() where T : struct, IJobSystem {
            JobSystemWrapper<T>.Initialize();
            return JobSystemWrapper<T>.JobReflectionData.Data;
        }

        public static unsafe JobHandle Schedule<TJob>(this TJob jobData,
            SystemMode mode, UpdateContext updateContext, ref State state)
            where TJob : struct, IJobSystem {
            var fullData = new JobSystemWrapper<TJob> {
                JobData = jobData,
                State = state,
                UpdateContext = updateContext
            };

            var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData),
                GetReflectionData<TJob>(), state.Dependencies,
                mode == SystemMode.Parallel ? ScheduleMode.Parallel : ScheduleMode.Single);
            switch (mode) {
                case SystemMode.Single:
                    return JobsUtility.Schedule(ref scheduleParams);
                // case SystemMode.Parallel:
                //     throw new Exception($"{typeof(IJobSystem)} is not support Parallel for now. Use single");
                //     return state.Dependencies;
                //     return JobsUtility.ScheduleParallelFor(ref scheduleParams, 1, 1);
            }
            return state.Dependencies;
        }
        // public static unsafe void Run<TJob>(this TJob jobData, ref World world, float deltaTime) where TJob : struct, IJobSystem
        // {
        //     var fullData = new JobSystemWrapper<TJob> {
        //         JobData = jobData,
        //         world = world,
        //         deltaTime = deltaTime
        //     };
        //     JobsUtility.JobScheduleParameters parameters = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData),
        //         JobSystemExtensions.GetReflectionData<TJob>(),
        //         new JobHandle(), 
        //         ScheduleMode.Run);
        //     JobsUtility.Schedule(ref parameters);
        // }
    }
}
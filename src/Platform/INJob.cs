using System;
#if UNITY_5_3_OR_NEWER
using Unity.Burst;
using Unity.Jobs.LowLevel.Unsafe;
#endif

namespace Wargon.Nukecs
{
    /// <summary>
    /// Job executed once. In Unity it is a job type of its own with the same producer as Unity's
    /// <c>IJob</c>, so Burst compiles <c>[BurstCompile]</c> implementations as before.
    /// </summary>
#if UNITY_5_3_OR_NEWER
    [JobProducerType(typeof(INJobExtensions.JobStruct<>))]
#endif
    public interface INJob
    {
        void Execute();
    }

    /// <summary>
    /// Job executed for every index in [0, length), split into batches. In Unity it has the same
    /// producer as Unity's <c>IJobParallelFor</c>.
    /// </summary>
#if UNITY_5_3_OR_NEWER
    [JobProducerType(typeof(INJobParallelForExtensions.ParallelForJobStruct<>))]
#endif
    public interface INJobParallelFor
    {
        void Execute(int index);
    }

    public static class INJobExtensions
    {
#if UNITY_5_3_OR_NEWER
        internal struct JobStruct<T> where T : struct, INJob
        {
            internal static readonly SharedStatic<IntPtr> JobReflectionData =
                SharedStatic<IntPtr>.GetOrCreate<JobStruct<T>>();

            [BurstDiscard]
            internal static void Initialize()
            {
                if (JobReflectionData.Data == IntPtr.Zero)
                    JobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(T), (ExecuteJobFunction)Execute);
            }

            private delegate void ExecuteJobFunction(ref T data, IntPtr additionalPtr, IntPtr bufferRangePatchData,
                ref JobRanges ranges, int jobIndex);

            public static void Execute(ref T data, IntPtr additionalPtr, IntPtr bufferRangePatchData,
                ref JobRanges ranges, int jobIndex) => data.Execute();
        }

        public static void EarlyJobInit<T>() where T : struct, INJob => JobStruct<T>.Initialize();

        private static IntPtr GetReflectionData<T>() where T : struct, INJob
        {
            JobStruct<T>.Initialize();
            return JobStruct<T>.JobReflectionData.Data;
        }

        /// <summary>Runs the job on a worker thread after <paramref name="dependsOn"/>.</summary>
        public static NJobHandle Schedule<T>(this T jobData, NJobHandle dependsOn = default) where T : struct, INJob =>
            NJobs.Schedule(ref jobData, GetReflectionData<T>(), dependsOn);

        /// <summary>Executes the job on the calling thread; takes no dependency.</summary>
        public static void Run<T>(this T jobData) where T : struct, INJob =>
            NJobs.Run(ref jobData, GetReflectionData<T>());
#else
        // Without a scheduler the job runs inside Schedule, after its dependency.
        public static NJobHandle Schedule<T>(this T jobData, NJobHandle dependsOn = default) where T : struct, INJob
        {
            dependsOn.Complete();
            jobData.Execute();
            return default;
        }

        public static void Run<T>(this T jobData) where T : struct, INJob => jobData.Execute();
#endif
    }

    public static class INJobParallelForExtensions
    {
#if UNITY_5_3_OR_NEWER
        internal struct ParallelForJobStruct<T> where T : struct, INJobParallelFor
        {
            internal static readonly SharedStatic<IntPtr> JobReflectionData =
                SharedStatic<IntPtr>.GetOrCreate<ParallelForJobStruct<T>>();

            [BurstDiscard]
            internal static void Initialize()
            {
                if (JobReflectionData.Data == IntPtr.Zero)
                    JobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(T), (ExecuteJobFunction)Execute);
            }

            private delegate void ExecuteJobFunction(ref T data, IntPtr additionalPtr, IntPtr bufferRangePatchData,
                ref JobRanges ranges, int jobIndex);

            public static void Execute(ref T jobData, IntPtr additionalPtr, IntPtr bufferRangePatchData,
                ref JobRanges ranges, int jobIndex)
            {
                while (JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end))
                {
                    for (var i = begin; i < end; ++i)
                        jobData.Execute(i);
                }
            }
        }

        public static void EarlyJobInit<T>() where T : struct, INJobParallelFor => ParallelForJobStruct<T>.Initialize();

        private static IntPtr GetReflectionData<T>() where T : struct, INJobParallelFor
        {
            ParallelForJobStruct<T>.Initialize();
            return ParallelForJobStruct<T>.JobReflectionData.Data;
        }

        /// <summary>Executes indices [0, arrayLength) in batches of <paramref name="innerloopBatchCount"/>
        /// on worker threads after <paramref name="dependsOn"/>.</summary>
        public static NJobHandle Schedule<T>(this T jobData, int arrayLength, int innerloopBatchCount,
            NJobHandle dependsOn = default) where T : struct, INJobParallelFor =>
            NJobs.ScheduleParallelFor(ref jobData, GetReflectionData<T>(), arrayLength, innerloopBatchCount, dependsOn);
#else
        public static NJobHandle Schedule<T>(this T jobData, int arrayLength, int innerloopBatchCount,
            NJobHandle dependsOn = default) where T : struct, INJobParallelFor
        {
            dependsOn.Complete();
            for (var i = 0; i < arrayLength; i++) jobData.Execute(i);
            return default;
        }
#endif
    }
}

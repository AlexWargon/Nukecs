using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using static Wargon.Nukecs.UnsafeStatic;
namespace Wargon.Nukecs {
    // public unsafe delegate void SystemActionPtr(void* param0);
    // public unsafe delegate void SystemActionPtr2(void* param0, void* param1);
    // public delegate void SystemFnDelegate(ref World world, ref SystemParams systemParams, ref Range range);
    //
    // public static unsafe class SystemFnRegistry2<TParam0>  
    //     where TParam0 : unmanaged, ISystemParam
    // {
    //     private static delegate* <TParam0, void> _fnPtr;
    //     public delegate void SystemAction(TParam0 query);
    //     private static FunctionPointer<SystemActionPtr> _fnFunctionPointer;
    //
    //     [BurstCompile(CompileSynchronously = true)]
    //     [AOT.MonoPInvokeCallback(typeof(SystemFnDelegate))]
    //     public static void Execute(ref World world, ref SystemParams systemParams, ref Range range)
    //     {
    //         ref var param0 = ref systemParams.FirstRef;
    //         var param0Copy = *param0.Ptr<TParam0>();
    //         var rangeFromQuery = (Range*)param0Copy.GetData();
    //         *rangeFromQuery = range;
    //         param0.data = (IntPtr)rangeFromQuery;
    //         param0Copy.Update(ref world, param0.data);
    //         if (!_fnFunctionPointer.IsCreated)
    //         {
    //             _fnFunctionPointer = new FunctionPointer<SystemActionPtr>(systemParams.system);
    //         }
    //         _fnFunctionPointer.Invoke(UnsafeUtility.AddressOf(ref param0Copy));
    //     }
    //     public static IntPtr Create()
    //     {
    //         return Marshal.GetFunctionPointerForDelegate(new SystemFnDelegate(Execute));
    //     }
    // }
    // [BurstCompile(CompileSynchronously = true)]
    // public static unsafe class SystemFnRegistryRef<TParam0>  
    //     where TParam0 : unmanaged, ISystemParam
    // {
    //     private static delegate* <ref TParam0, void> _fnPtr;
    //
    //     [BurstCompile(CompileSynchronously = true)]
    //     [AOT.MonoPInvokeCallback(typeof(SystemFnDelegate))]
    //     public static void Execute(ref World world, ref SystemParams systemParams, ref Range range)
    //     {
    //         ref var param0 = ref systemParams.FirstRef;
    //         var param0Copy = *param0.Ptr<TParam0>();
    //         var rangeFromQuery = (Range*)param0Copy.GetData();
    //         *rangeFromQuery = range;
    //         param0.data = (IntPtr)rangeFromQuery;
    //         param0Copy.Update(ref world, param0.data);
    //         if (_fnPtr == null)
    //         {
    //             _fnPtr = (delegate* <ref TParam0, void>)systemParams.system;
    //         }
    //         _fnPtr(ref param0Copy);
    //     }
    //     public static IntPtr Create()
    //     {
    //         return Marshal.GetFunctionPointerForDelegate(new SystemFnDelegate(Execute));
    //     }
    // }
    // [BurstCompile(CompileSynchronously = true)]
    // public static unsafe class SystemFnRegistry<TParam0>  
    //     where TParam0 : unmanaged, ISystemParam
    // {
    //     private static delegate* <TParam0, void> _fnPtr;
    //     public delegate void SystemAction(TParam0 query);
    //
    //     [BurstCompile(CompileSynchronously = true)]
    //     [AOT.MonoPInvokeCallback(typeof(SystemFnDelegate))]
    //     public static void Execute(ref World world, ref SystemParams systemParams, ref Range range)
    //     {
    //         ref var param0 = ref systemParams.FirstRef;
    //         var param0Copy = *param0.Ptr<TParam0>();
    //         var rangeFromQuery = (Range*)param0Copy.GetData();
    //         *rangeFromQuery = range;
    //         param0.data = (IntPtr)rangeFromQuery;
    //         param0Copy.Update(ref world, param0.data);
    //         if (_fnPtr == null)
    //         {
    //             _fnPtr = (delegate* <TParam0, void>)systemParams.system;
    //         }
    //         _fnPtr(param0Copy);
    //     }
    //     public static IntPtr Create()
    //     {
    //         return Marshal.GetFunctionPointerForDelegate(new SystemFnDelegate(Execute));
    //     }
    // }
    // [BurstCompile(CompileSynchronously = true)]
    // public static class SystemFnRegistry<TParam0, TParam1>  
    //     where TParam0 : unmanaged, ISystemParam
    //     where TParam1 : unmanaged, ISystemParam
    // {
    //     [BurstCompile(CompileSynchronously = true)]
    //     [AOT.MonoPInvokeCallback(typeof(SystemFnDelegate))]
    //     public static unsafe void Execute(ref World world, ref SystemParams systemParams, ref Range range)
    //     {
    //         ref var param0 = ref systemParams.FirstRef;
    //         var param0Copy = *param0.Ptr<TParam0>();
    //         var rangeFromQuery = (Range*)param0Copy.GetData();
    //         *rangeFromQuery = range;
    //         param0.data = (IntPtr)rangeFromQuery;
    //         param0Copy.Update(ref world, param0.data);
    //         ref var param1 = ref systemParams.list.ElementAt(1);
    //         param1.Ptr<TParam1>()->Update(ref world, param1.data);
    //         new FunctionPointer<SystemAction<TParam0, TParam1>>(systemParams.system).Invoke(*param0.Ptr<TParam0>(), *param1.Ptr<TParam1>());
    //     }
    //     public static IntPtr Create()
    //     {
    //         return Marshal.GetFunctionPointerForDelegate(new SystemFnDelegate(Execute));
    //     }
    // }
    // public static class SystemFnRegistry<TParam0, TParam1, TParam2>  
    //     where TParam0 : unmanaged, ISystemParam
    //     where TParam1 : unmanaged, ISystemParam
    //     where TParam2 : unmanaged, ISystemParam
    // {
    //     [BurstCompile(CompileSynchronously = true)]
    //     [AOT.MonoPInvokeCallback(typeof(SystemFnDelegate))]
    //     public static unsafe void Execute(ref World world, ref SystemParams systemParams, ref Range range)
    //     {
    //         ref var param0 = ref systemParams.FirstRef;
    //         var param0Copy = *param0.Ptr<TParam0>();
    //         var rangeFromQuery = (Range*)param0Copy.GetData();
    //         *rangeFromQuery = range;
    //         param0.data = (IntPtr)rangeFromQuery;
    //         param0Copy.Update(ref world, param0.data);
    //         ref var param1 = ref systemParams.list.ElementAt(1);
    //         param1.Ptr<TParam1>()->Update(ref world, param1.data);
    //         ref var param2 = ref systemParams.list.ElementAt(2);
    //         param1.Ptr<TParam2>()->Update(ref world, param2.data);
    //         new FunctionPointer<SystemAction<TParam0, TParam1>>(systemParams.system).Invoke(*param0.Ptr<TParam0>(), *param1.Ptr<TParam1>());
    //     }
    //     public static IntPtr Create()
    //     {
    //         return Marshal.GetFunctionPointerForDelegate(new SystemFnDelegate(Execute));
    //     }
    // }
    // [BurstCompile(CompileSynchronously = true, OptimizeFor = OptimizeFor.Performance)]
    // public unsafe struct DelegateJob : IDelegateJobSystem
    // {
    //     public FunctionPointer<SystemFnDelegate> fn;
    //     [NativeDisableUnsafePtrRestriction]
    //     public SystemParams* systemParams;
    //     public World world;
    //     public void OnUpdate(Range range)
    //     {
    //         fn.Invoke(ref world, ref *systemParams, ref range);
    //     }
    // }
    //
    // public unsafe struct SystemParams
    // {
    //     public UnsafeList<SystemParam> list;
    //     [NativeDisableUnsafePtrRestriction]
    //     public IntPtr system;
    //     public Threads mode;
    //     public ref SystemParam FirstRef => ref list.ElementAt(0);
    //     public static SystemParams New<TParam0>(World.WorldUnsafe* world, IntPtr systemAction)
    //         where TParam0 : unmanaged, ISystemParam
    //     {
    //         var systemParams = new SystemParams
    //         {
    //             list = new UnsafeList<SystemParam>(1, Allocator.Persistent),
    //             system = (IntPtr)systemAction
    //         };
    //         var param0 = new SystemParam
    //         {
    //             value = world->GetSystemParam<TParam0>(out var metaType),
    //             data = metaType == SystemParamMetaType.Query ? (IntPtr)malloc_t<Range>(Allocator.Persistent) : IntPtr.Zero,
    //         };
    //         
    //         systemParams.list.Add(param0);
    //         return systemParams;
    //     }
    //
    //     public static SystemParams New<TParam0, TParam1>(World.WorldUnsafe* world, 
    //         IntPtr systemAction) 
    //         where TParam0 : unmanaged, ISystemParam
    //         where TParam1 : unmanaged, ISystemParam
    //     
    //     {
    //         var systemParams = new SystemParams
    //         {
    //             list = new UnsafeList<SystemParam>(1, Allocator.Persistent),
    //             system = (IntPtr)systemAction
    //         };
    //         var param0 = new SystemParam
    //         {
    //             value = world->GetSystemParam<TParam0>(out var metaType),
    //             data = metaType == SystemParamMetaType.Query ? (IntPtr)malloc_t<Range>(Allocator.Persistent) : IntPtr.Zero,
    //         };
    //         systemParams.list.Add(param0);
    //         var param1 = new SystemParam
    //         {
    //             value = world->GetSystemParam<TParam1>(out metaType),
    //             data = metaType == SystemParamMetaType.Query ? (IntPtr)malloc_t<Range>(Allocator.Persistent) : IntPtr.Zero,
    //         };
    //         systemParams.list.Add(param1);
    //         return systemParams;
    //     }
    //     
    //     public static SystemParams New<TParam0, TParam1, TParam2>(World.WorldUnsafe* world, 
    //         IntPtr systemAction) 
    //         where TParam0 : unmanaged, ISystemParam
    //         where TParam1 : unmanaged, ISystemParam
    //         where TParam2 : unmanaged, ISystemParam
    //     {
    //         var systemParams = new SystemParams
    //         {
    //             list = new UnsafeList<SystemParam>(2, Allocator.Persistent),
    //             system = (IntPtr)systemAction
    //         };
    //         var param0 = new SystemParam
    //         {
    //             value = world->GetSystemParam<TParam0>(out var metaType),
    //             data = metaType == SystemParamMetaType.Query ? (IntPtr)malloc_t<Range>(Allocator.Persistent) : IntPtr.Zero
    //         };
    //         systemParams.list.Add(param0);
    //         var param1 = new SystemParam
    //         {
    //             value = world->GetSystemParam<TParam1>(out metaType),
    //             data = metaType == SystemParamMetaType.Query ? (IntPtr)malloc_t<Range>(Allocator.Persistent) : IntPtr.Zero
    //         };
    //         systemParams.list.Add(param1);
    //         var param2 = new SystemParam
    //         {
    //             value = world->GetSystemParam<TParam2>(out metaType),
    //             data = metaType == SystemParamMetaType.Query ? (IntPtr)malloc_t<Range>(Allocator.Persistent) : IntPtr.Zero
    //         };
    //         systemParams.list.Add(param2);
    //         return systemParams;
    //     }
    // }
    //
    // public struct SystemParam : IDisposable
    // {
    //     public ptr value;
    //     [NativeDisableUnsafePtrRestriction]
    //     public IntPtr data;
    //     public unsafe T* Ptr<T>() where T : unmanaged, ISystemParam
    //     {
    //         return value.As<T>();
    //     }
    //
    //     public unsafe void Dispose()
    //     {
    //         free_t((void*)data, Allocator.Domain);
    //     }
    // }
    //
    // [JobProducerType(typeof(DelegateJobSystemExtensions.DelegateJobWrapper<>))]
    // public interface IDelegateJobSystem {
    //     void OnUpdate(Range range);
    // }
    // public static class DelegateJobSystemExtensions {
    //     [StructLayout(LayoutKind.Sequential)]
    //     internal unsafe struct DelegateJobWrapper<TJob> where TJob : struct, IDelegateJobSystem {
    //         public TJob JobData;
    //         public Threads mode;
    //         [NativeDisableUnsafePtrRestriction]
    //         public QueryUnsafe* query;
    //         public State State;
    //         internal static readonly SharedStatic<IntPtr> JobReflectionData =
    //             SharedStatic<IntPtr>.GetOrCreate<DelegateJobWrapper<TJob>>();
    //
    //         [BurstDiscard][MethodImpl(MethodImplOptions.AggressiveInlining)]
    //         internal static void Initialize() {
    //             if (JobReflectionData.Data == IntPtr.Zero) {
    //                 JobReflectionData.Data = JobsUtility.CreateJobReflectionData(typeof(DelegateJobWrapper<TJob>),
    //                     typeof(TJob), (ExecuteJobFunction)Execute);
    //             }
    //         }
    //
    //         private delegate void ExecuteJobFunction(ref DelegateJobWrapper<TJob> fullData, IntPtr additionalPtr,
    //             IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex);
    //         [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //         public static void Execute(ref DelegateJobWrapper<TJob> fullData, IntPtr additionalPtr,
    //             IntPtr bufferRangePatchData, ref JobRanges ranges, int jobIndex) {
    //             if(fullData.query->count == 0) return;
    //             Range range;
    //             switch (fullData.mode) {
    //                 case Threads.Parallel:
    //                     while (true) {
    //                         if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end))
    //                             break;
    //                         //dbug.log($"PER THREAD {(thead : jobIndex, from : begin, to : end)}");
    //                         range = new Range(begin, end);
    //                         fullData.JobData.OnUpdate(range);
    //                     }
    //                     break;
    //                 case Threads.Single:
    //                     range = new Range(0, fullData.query->count);
    //                     //dbug.log($"SINGLE {(0, fullData.query->count)}");
    //                     fullData.JobData.OnUpdate(range);
    //                     break;
    //             }
    //         }
    //     }
    //
    //     [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //     public static void EarlyJobInit<T>() where T : struct, IDelegateJobSystem {
    //         DelegateJobWrapper<T>.Initialize();
    //     }
    //     [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //     private static IntPtr GetReflectionData<T>() where T : struct, IDelegateJobSystem {
    //         DelegateJobWrapper<T>.Initialize();
    //         return DelegateJobWrapper<T>.JobReflectionData.Data;
    //     }
    //     [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //     internal static unsafe JobHandle Schedule<TJob>(this TJob jobData, QueryUnsafe* query,
    //         Threads mode, ref State state)
    //         where TJob : struct, IDelegateJobSystem {
    //         var fullData = new DelegateJobWrapper<TJob> {
    //             JobData = jobData,
    //             query = query,
    //             State = state,
    //             mode = mode
    //         };
    //         
    //         var scheduleParams = new JobsUtility.JobScheduleParameters(UnsafeUtility.AddressOf(ref fullData),
    //             GetReflectionData<TJob>(), state.Dependencies,
    //             mode == Threads.Parallel ? ScheduleMode.Parallel : ScheduleMode.Single);
    //         var workers = JobsUtility.JobWorkerCount;
    //         var batchCount = query->count > workers ? query->count / workers : 1;
    //         switch (mode) {
    //             case Threads.Single:
    //                 return JobsUtility.Schedule(ref scheduleParams);
    //             case Threads.Parallel:
    //                 return JobsUtility.ScheduleParallelFor(ref scheduleParams, query->count, batchCount);
    //         }
    //
    //         return state.Dependencies;
    //     }
    //     
    //     public static unsafe void Run<TJob>(this TJob jobData, ref Query query, float deltaTime) where TJob : struct, IDelegateJobSystem
    //     {
    //         var fullData = new DelegateJobWrapper<TJob> {
    //             JobData = jobData,
    //             //query = query,
    //             //deltaTime = deltaTime
    //         };
    //         JobsUtility.JobScheduleParameters parameters = new JobsUtility.JobScheduleParameters(
    //             UnsafeUtility.AddressOf(ref fullData),
    //             GetReflectionData<TJob>(),
    //         new JobHandle(), 
    //             ScheduleMode.Run);
    //         JobsUtility.Schedule(ref parameters);
    //     }
    // }
}
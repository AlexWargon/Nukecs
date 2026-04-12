using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Wargon.Nukecs.Transforms;

//namespace Wargon.Nukecs
//{
//    public static class DelegateSystemsExtensions {
//
//         private static unsafe Systems add_system_impl<TParam0>(this Systems systems, 
//             SystemFnRegistry2<TParam0>.SystemAction func, Threads threads = Threads.Parallel)
//             where TParam0 : unmanaged, ISystemParam
//         {
//             SystemActionPtr burstWrapper = (void* ptr) =>
//             {
//                 ref TParam0 param = ref *(TParam0*)ptr;
//                 func(param);
//             };
//             var funcPtr = BurstCompiler.CompileFunctionPointer(burstWrapper);
//             var systemParams = SystemParams.New<TParam0>(systems.World.UnsafeWorld, funcPtr.Value);
//             var fn = new FunctionPointer<SystemFnDelegate>
//                 (SystemFnRegistry2<TParam0>.Create());
//             
//             systemParams.FirstRef.value.AsRef<TParam0>()
//                 .TryGetQuery(out var q);
//             var runner = new DelegateSystemRunner
//             {
//                 mode = threads,
//                 query = q,
//                 systemParams = systemParams,
//                 job = new DelegateJob
//                 {
//                     fn = fn,
//                     world = systems.World
//                 }
//             };
//             systems.runners.Add(runner);
//             return systems;
//         }
//         private static unsafe Systems add_system_impl<TParam0>(this Systems systems, 
//             delegate* <ref TParam0, void> func, Threads threads = Threads.Parallel)
//             where TParam0 : unmanaged, ISystemParam
//         {
//             var systemParams = SystemParams.New<TParam0>(systems.World.UnsafeWorld, (IntPtr)func);
//             var fnWrapper = new FunctionPointer<SystemFnDelegate>
//                 (SystemFnRegistryRef<TParam0>.Create());
//             
//             systemParams.FirstRef.value.AsRef<TParam0>()
//                 .TryGetQuery(out var query);
//             var runner = new DelegateSystemRunner
//             {
//                 mode = threads,
//                 query = query,
//                 systemParams = systemParams,
//                 job = new DelegateJob
//                 {
//                     fn = fnWrapper,
//                     world = systems.World
//                 },
//                 name = "DelegateSystem2Ref"
//             };
//             systems.runners.Add(runner);
//             return systems;
//         }
//         private static unsafe Systems add_system_impl<TParam0>(this Systems systems, 
//             delegate* <TParam0, void> func, Threads threads = Threads.Parallel)
//             where TParam0 : unmanaged, ISystemParam
//         {
//             var systemParams = SystemParams.New<TParam0>(systems.World.UnsafeWorld, (IntPtr)func);
//             var fnWrapper = new FunctionPointer<SystemFnDelegate>
//                 (SystemFnRegistry<TParam0>.Create());
//             
//             systemParams.FirstRef.value.AsRef<TParam0>()
//                 .TryGetQuery(out var query);
//             var runner = new DelegateSystemRunner
//             {
//                 mode = threads,
//                 query = query,
//                 systemParams = systemParams,
//                 job = new DelegateJob
//                 {
//                     fn = fnWrapper,
//                     world = systems.World
//                 }
//             };
//             systems.runners.Add(runner);
//             return systems;
//         }
//         private static unsafe Systems add_system_impl<TParam0, TParam1>(this Systems systems, 
//             delegate* <TParam0, TParam1, void> func, Threads threads = Threads.Parallel)
//             where TParam0 : unmanaged, ISystemParam
//             where TParam1 : unmanaged, ISystemParam
//         {
//             var systemParams = SystemParams.New<TParam0, TParam1>(systems.World.UnsafeWorld, (IntPtr)func);
//             var fnWrapper = new FunctionPointer<SystemFnDelegate>
//                 (SystemFnRegistry<TParam0, TParam1>.Create());
//             
//             systemParams.FirstRef.value.AsRef<TParam0>()
//                 .TryGetQuery(out var query);
//             var runner = new DelegateSystemRunner
//             {
//                 mode = threads,
//                 query = query,
//                 systemParams = systemParams,
//                 job = new DelegateJob
//                 {
//                     fn = fnWrapper,
//                     world = systems.World
//                 }
//             };
//             systems.runners.Add(runner);
//             return systems;
//         }
//         
//         private static unsafe Systems add_system_impl<TParam0, TParam1, TParam2>(this Systems systems, 
//             delegate* <TParam0, TParam1, TParam2, void> func, Threads threads = Threads.Parallel)
//             where TParam0 : unmanaged, ISystemParam
//             where TParam1 : unmanaged, ISystemParam
//             where TParam2 : unmanaged, ISystemParam
//         {
//             var systemParams = SystemParams.New<TParam0, TParam1, TParam2>(systems.World.UnsafeWorld, (IntPtr)func);
//             var fn = new FunctionPointer<SystemFnDelegate>
//                 (SystemFnRegistry<TParam0, TParam1, TParam2>.Create());
//             
//             systemParams.FirstRef.value.AsRef<TParam0>()
//                 .TryGetQuery(out var q);
//             var runner = new DelegateSystemRunner
//             {
//                 mode = threads,
//                 query = q,
//                 systemParams = systemParams,
//                 job = new DelegateJob
//                 {
//                     fn = fn,
//                     world = systems.World
//                 }
//             };
//             systems.runners.Add(runner);
//             return systems;
//         }
//         public static unsafe Systems AddSystem2Ref(this Systems systems, 
//             delegate* <ref Query<Transform, Input>.WithEntity,void> func, Threads threads = Threads.Parallel)
//         {
//             return add_system_impl(systems, func, threads);
//         }
//         public static unsafe Systems AddSystem2(this Systems systems, 
//             delegate* <Query<Transform, Input>.WithEntity,void> func, Threads threads = Threads.Parallel)
//         {
//             return add_system_impl(systems, func, threads);
//         }
//         public static Systems AddSystem2(this Systems systems, 
//             SystemFnRegistry2<Query<Transform, Input>.WithEntity>.SystemAction func, Threads threads = Threads.Parallel)
//         {
//             return add_system_impl(systems, func, threads);
//         }
//         [BurstCompile(CompileSynchronously = true, OptimizeFor = OptimizeFor.Performance)]
//         public unsafe struct SystemDelegatePtrGenericJob<TParam0> : IDelegateJobSystem 
//             where TParam0 : unmanaged, ISystemParam
//         {
//             [NativeDisableUnsafePtrRestriction]
//             public IntPtr fn;
//             public ptr<TParam0> q;
//             public World world;
//             [MethodImpl(MethodImplOptions.AggressiveInlining)]
//             public void OnUpdate(Range range)
//             {
//                 var copy = q.Ref;
//                 copy.Update(ref world, (IntPtr)UnsafeUtility.AddressOf(ref range));
//                 ((delegate* <TParam0, void>)fn)(copy);
//             }
//         }
//
//         [BurstCompile(CompileSynchronously = true, OptimizeFor = OptimizeFor.Performance)]
//         public struct SystemActionQueryTransformInputWithEntityJob : IDelegateJobSystem
//         {
//             public FunctionPointer<SystemActionQueryTransformInputWithEntity> fn;
//             public ptr<Query<Transform, Input>.WithEntity> q;
//             public World world;
//             public void OnUpdate(Range range)
//             {
//                 var copy = q.Ref;
//                 copy.SetRange(range);
//                 copy.UpdateInner();
//                 fn.Invoke(ref copy);
//             }
//         }
//
//         public delegate void SystemActionQueryTransformInputWithEntity(ref Query<Transform, Input>.WithEntity query);
//         public static Systems AddSystem12(this Systems systems, 
//             SystemActionQueryTransformInputWithEntity func, Threads threads = Threads.Parallel)
//         {
//             var funcPtr = BurstCompiler.CompileFunctionPointer(func);
//             var queryPtr = systems.World.UnsafeWorldRef.GetSystemParam2<Query<Transform, Input>.WithEntity>();
//             queryPtr.Ref.TryGetQuery(out var query);
//             var runner = new DelegateSystemRunner<SystemActionQueryTransformInputWithEntityJob>
//             {
//                 mode = threads,
//                 query = query,
//
//                 job = new SystemActionQueryTransformInputWithEntityJob
//                 {
//                     fn = funcPtr,
//                     world = systems.World,
//                     q = queryPtr,
//                 }
//             };
//             systems.runners.Add(runner);
//             return systems;
//         }
//         public static unsafe Systems AddSystem13Ref<TParam0>(this Systems systems, 
//             delegate* <ref TParam0, void> func, Threads threads = Threads.Parallel)
//             where TParam0 : unmanaged, ISystemParam
//         {
//             return systems.add_system_impl(func, threads);
//         }
//         public static unsafe Systems AddSystem13<TParam0>(this Systems systems, 
//             delegate* <TParam0, void> func, Threads threads = Threads.Parallel)
//             where TParam0 : unmanaged, ISystemParam
//         {
//             var queryPtr = systems.World.UnsafeWorldRef.GetSystemParam2<TParam0>();
//             queryPtr.Ref.TryGetQuery(out var query);
//             var runner = new DelegateSystemRunner<SystemDelegatePtrGenericJob<TParam0>>
//             {
//                 mode = threads,
//                 query = query,
//
//                 job = new SystemDelegatePtrGenericJob<TParam0>
//                 {
//                     fn = (IntPtr)func,
//                     world = systems.World,
//                     q = queryPtr,
//                 }
//             };
//             systems.runners.Add(runner);
//             return systems;
//         }
//         public static unsafe Systems AddSystem13<TParam0>(this Systems systems, 
//             delegate* <ref TParam0, void> func, Threads threads = Threads.Parallel)
//             where TParam0 : unmanaged, ISystemParam
//         {
//             var queryPtr = systems.World.UnsafeWorldRef.GetSystemParam2<TParam0>();
//             queryPtr.Ref.TryGetQuery(out var query);
//             var runner = new DelegateSystemRunner<SystemDelegatePtrGenericJob<TParam0>>
//             {
//                 mode = threads,
//                 query = query,
//
//                 job = new SystemDelegatePtrGenericJob<TParam0>
//                 {
//                     fn = (IntPtr)func,
//                     world = systems.World,
//                     q = queryPtr,
//                 }
//             };
//             systems.runners.Add(runner);
//             return systems;
//         }
//     }
//     
//     public unsafe class DelegateSystemRunner<TJob> : ISystemRunner where TJob : struct, IDelegateJobSystem {
//         public TJob job;
//         public ptr<QueryUnsafe> query;
//         public Threads mode;
// #if NUKECS_DEBUG
//         private Marker _marker;
// #endif
//         [MethodImpl(MethodImplOptions.AggressiveInlining)]
//         public JobHandle Schedule(UpdateContext updateContext, ref State state) {
// #if NUKECS_DEBUG
//             _marker.Autostart(typeof(TJob).Name);
// #endif
//             ref var handle = ref state.Dependencies;
//             if (mode != Threads.Main)
//             {
//                 handle = job.Schedule(query.Ptr, mode, ref state);
//             }
//             else
//             {
//                 job.OnUpdate(new Range(0, query.Ref.count));
//             }
// #if NUKECS_DEBUG
//             _marker.End();
// #endif
//             return handle;
//         }
//         public void Run(ref State state) {
//             job.OnUpdate(new Range(0, query.Ref.count));
//         }
//
//         public string Name => typeof(TJob).Name;
//     }
//     public unsafe class DelegateSystemRunner : ISystemRunner {
//         public DelegateJob job;
//         public ptr<QueryUnsafe> query;
//         public Threads mode;
//         public SystemParams systemParams;
// #if NUKECS_DEBUG
//         private Marker _marker;
// #endif
//         public JobHandle Schedule(UpdateContext updateContext, ref State state) {
// #if NUKECS_DEBUG
//             _marker.Autostart(Name);
// #endif
//             job.systemParams = (SystemParams*)UnsafeUtility.AddressOf(ref systemParams);
//             ref var handle = ref state.Dependencies;
//             if (mode != Threads.Main)
//             {
//                 handle = job.Schedule(query.Ptr, mode, ref state);
//             }
//             else
//             {
//                 job.OnUpdate(new Range(0, query.Ref.count));
//             }
// #if NUKECS_DEBUG
//             _marker.End();
// #endif
//             return handle;
//         }
//         public void Run(ref State state) {
//             job.OnUpdate(new Range(0, query.Ref.count));
//         }
//         public string name = "DelegateSystem2";
//         public string Name => name;
//     }
//}
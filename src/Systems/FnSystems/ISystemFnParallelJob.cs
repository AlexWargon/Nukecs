using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Wargon.Nukecs {
    public interface ISystemFnParallelJob {
        
        void Execute(SystemParams systemParams);
    }
    
    public delegate void SystemFnDelegate(ref World world, ref SystemParams systemParams);

    public static class SystemFnRegistry<TParam0>  
        where TParam0 : unmanaged, ISystemParam
    {
        [BurstCompile(CompileSynchronously = true)]
        [AOT.MonoPInvokeCallback(typeof(SystemFnDelegate))]
        public static unsafe void Execute(ref World world, ref SystemParams systemParams)
        {
            ref var param0 = ref systemParams.list.ElementAt(0);
            var rangeFromQuery = (Range*)param0.Ptr<TParam0>()->GetData();
            switch (systemParams.mode)
            {
                case SystemMode.Main:
                    *rangeFromQuery = new Range(0, rangeFromQuery->end);
                    break;
                case SystemMode.Parallel:
                    *rangeFromQuery = new Range(0, rangeFromQuery->end);
                    break;
                case SystemMode.Single:
                    *rangeFromQuery = new Range(0, rangeFromQuery->end);
                    break;
            }
            param0.data = (IntPtr)rangeFromQuery;
            param0.Ptr<TParam0>()->Update(ref world, param0.data);

            new FunctionPointer<SystemAction<TParam0>>(systemParams.system).Invoke(*param0.Ptr<TParam0>());
        }

        public static IntPtr Create()
        {
            return Marshal.GetFunctionPointerForDelegate(new SystemFnDelegate(Execute));
        }
    }
    public static class SystemFnRegistry<TParam0, TParam1>  
        where TParam0 : unmanaged, ISystemParam
        where TParam1 : unmanaged, ISystemParam
    {
        [BurstCompile(CompileSynchronously = true)]
        [AOT.MonoPInvokeCallback(typeof(SystemFnDelegate))]
        public static unsafe void Execute(ref World world, ref SystemParams systemParams)
        {
            ref var param0 = ref systemParams.list.ElementAt(0);
            var range = (Range*)param0.Ptr<TParam0>()->GetData();
            switch (systemParams.mode)
            {
                case SystemMode.Main:
                    *range = new Range(0, range->end);
                    break;
                case SystemMode.Parallel:
                    *range = new Range(0, range->end);
                    break;
                case SystemMode.Single:
                    *range = new Range(0, range->end);
                    break;
            }
            param0.data = (IntPtr)range;
            param0.Ptr<TParam0>()->Update(ref world, param0.data);
            ref var param1 = ref systemParams.list.ElementAt(1);
            param1.Ptr<TParam1>()->Update(ref world, param1.data);
            new FunctionPointer<SystemAction<TParam0, TParam1>>(systemParams.system).Invoke(*param0.Ptr<TParam0>(), *param1.Ptr<TParam1>());
        }
        public static IntPtr Create()
        {
            return Marshal.GetFunctionPointerForDelegate(new SystemFnDelegate(Execute));
        }
    }

    public struct SystemFnJob
    {
        public FunctionPointer<SystemFnDelegate> fn;
        public SystemFnDelegate fnManaged;
        public SystemParams systemParams;
        public World world;
        public void Execute()
        {
            fnManaged.Invoke(ref world, ref systemParams);
            //fn.Invoke(ref world, ref systemParams);
        }
    }

    public unsafe struct SystemParams
    {
        public UnsafeList<SystemParam> list;
        public IntPtr system;
        public SystemMode mode;
        public ref SystemParam FirstRef => ref list.ElementAt(0);
        public static SystemParams New<TParam0>(World.WorldUnsafe* world, SystemAction<TParam0> systemAction)
            where TParam0 : unmanaged, ISystemParam
        {
            var systemParams = new SystemParams
            {
                list = new UnsafeList<SystemParam>(1, Allocator.Persistent),
                system = Marshal.GetFunctionPointerForDelegate(systemAction)
            };
            var param0 = new SystemParam
            {
                value = world->GetSystemParam<TParam0>(out var metaType),
                data = metaType == SystemParamMetaType.Query ? (IntPtr)UnsafeStatic.malloc_t<Range>(Allocator.Persistent) : IntPtr.Zero,
            };
            
            systemParams.list.Add(param0);
            return systemParams;
        }
        public static SystemParams New<TParam0, TParam1>(World.WorldUnsafe* world, SystemAction<TParam0, TParam1> systemAction) 
            where TParam0 : unmanaged, ISystemParam
            where TParam1 : unmanaged, ISystemParam
        
        {
            var systemParams = new SystemParams
            {
                list = new UnsafeList<SystemParam>(1, Allocator.Persistent)
            };
            var param0 = new SystemParam
            {
                value = world->GetSystemParam<TParam0>(out var metaType),
                data = metaType == SystemParamMetaType.Query ? (IntPtr)UnsafeStatic.malloc_t<Range>(Allocator.Persistent) : IntPtr.Zero,
            };
            systemParams.list.Add(param0);
            var param1 = new SystemParam
            {
                value = world->GetSystemParam<TParam1>(out metaType),
                data = metaType == SystemParamMetaType.Query ? (IntPtr)UnsafeStatic.malloc_t<Range>(Allocator.Persistent) : IntPtr.Zero,
            };
            systemParams.list.Add(param1);
            return systemParams;
        }
        
        public static SystemParams New<TParam0, TParam1, TParam2>(World.WorldUnsafe* world, SystemAction<TParam0, TParam1, TParam2> systemAction) 
            where TParam0 : unmanaged, ISystemParam
            where TParam1 : unmanaged, ISystemParam
            where TParam2 : unmanaged, ISystemParam
        
        {
            var systemParams = new SystemParams
            {
                list = new UnsafeList<SystemParam>(2, Allocator.Persistent)
            };
            var param0 = new SystemParam
            {
                value = world->GetSystemParam<TParam0>(out var metaType),
                data = metaType == SystemParamMetaType.Query ? (IntPtr)UnsafeStatic.malloc_t<Range>(Allocator.Persistent) : IntPtr.Zero,
            };
            systemParams.list.Add(param0);
            var param1 = new SystemParam
            {
                value = world->GetSystemParam<TParam1>(out metaType),
                data = metaType == SystemParamMetaType.Query ? (IntPtr)UnsafeStatic.malloc_t<Range>(Allocator.Persistent) : IntPtr.Zero,
            };
            systemParams.list.Add(param1);
            var param2 = new SystemParam
            {
                value = world->GetSystemParam<TParam2>(out metaType),
                data = metaType == SystemParamMetaType.Query ? (IntPtr)UnsafeStatic.malloc_t<Range>(Allocator.Persistent) : IntPtr.Zero,
            };
            systemParams.list.Add(param2);
            return systemParams;
        }
    }
    public struct SystemParams1
    {
        public SystemParam element0;
    }

    public struct SystemParam : IDisposable
    {
        public ptr value;
        public IntPtr data;
        public unsafe T* Ptr<T>() where T : unmanaged, ISystemParam
        {
            return value.As<T>();
        }

        public unsafe void Dispose()
        {
            UnsafeStatic.free_t((void*)data, Allocator.Persistent);
        }
    }
}
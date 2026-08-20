using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Jobs;

namespace Wargon.Nukecs
{
    public struct QueryParamInfo<T>
    {
        internal static readonly SharedStatic<QueryParamData> data = SharedStatic<QueryParamData>.GetOrCreate<QueryParamInfo<T>>();

        public static bool IsComponent
        {

            get => data.Data.IsComponent == 1;

            set => data.Data.IsComponent = value ? (byte)1 : (byte)0;
        }

        public static int Index
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => data.Data.Index;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => data.Data.Index = value ;
        }
        public static StorageType StorageType
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => data.Data.StorageType;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => data.Data.StorageType = value;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct QueryParamData
    {
        public byte IsComponent;
        public int Index;
        public StorageType StorageType;
    }

    public interface IQQuery<in T>
    {
        
    }

    public struct QQuery<T> : IQQuery<T>
    {
        
    }
    public unsafe interface IFilter
    {
        void Setup(QueryUnsafe* query);
    }

    public struct Filter<TWith, TNone> : IFilter
        where TWith : unmanaged, IFilter
        where TNone : unmanaged, IFilter
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            TWith with = default;
            TNone none = default;
            with.Setup(query);
            none.Setup(query);
        }
    }
    public struct With<T1> : IFilter where T1 : unmanaged, IComponent
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            query->With(ComponentType<T1>.Index);
        }
    }
    
    
    public struct With<T1, T2> : IFilter
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            query->With(ComponentType<T1>.Index);
            query->With(ComponentType<T2>.Index);
        }
    }
    public struct With<T1, T2, T3> : IFilter
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            query->With(ComponentType<T1>.Index);
            query->With(ComponentType<T2>.Index);
            query->With(ComponentType<T3>.Index);
        }
    }
    
    public struct With<T1, T2, T3, T4> : IFilter
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            query->With(ComponentType<T1>.Index);
            query->With(ComponentType<T2>.Index);
            query->With(ComponentType<T3>.Index);
            query->With(ComponentType<T4>.Index);
        }
    }
    
    public struct With<T1, T2, T3, T4, T5> : IFilter
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged, IComponent
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            query->With(ComponentType<T1>.Index);
            query->With(ComponentType<T2>.Index);
            query->With(ComponentType<T3>.Index);
            query->With(ComponentType<T4>.Index);
            query->With(ComponentType<T5>.Index);
        }
    }
    
    public struct With<T1, T2, T3, T4, T5, T6> : IFilter
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged, IComponent
        where T6 : unmanaged, IComponent
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            query->With(ComponentType<T1>.Index);
            query->With(ComponentType<T2>.Index);
            query->With(ComponentType<T3>.Index);
            query->With(ComponentType<T4>.Index);
            query->With(ComponentType<T5>.Index);
            query->With(ComponentType<T6>.Index);
        }
    }
    
    public struct None<T1> : IFilter where T1 : unmanaged, IComponent
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            query->None(ComponentType<T1>.Index);
        }
    }
    
    public struct None<T1, T2> : IFilter where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            query->None(ComponentType<T1>.Index);
            query->None(ComponentType<T2>.Index);
        }
    }
    public struct None<T1, T2, T3> : IFilter
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            query->None(ComponentType<T1>.Index);
            query->None(ComponentType<T2>.Index);
            query->None(ComponentType<T3>.Index);
        }
    }
    
    public struct None<T1, T2, T3, T4> : IFilter 
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            query->None(ComponentType<T1>.Index);
            query->None(ComponentType<T2>.Index);
            query->None(ComponentType<T3>.Index);
            query->None(ComponentType<T4>.Index);
        }
    }
    
    public struct None<T1, T2, T3, T4, T5> : IFilter 
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged, IComponent
    {
        public unsafe void Setup(QueryUnsafe* query)
        {
            query->None(ComponentType<T1>.Index);
            query->None(ComponentType<T2>.Index);
            query->None(ComponentType<T3>.Index);
            query->None(ComponentType<T4>.Index);
            query->None(ComponentType<T5>.Index);
        }
    }

    public struct Empty : IFilter, IComponent {
        public unsafe void Setup(QueryUnsafe* query) {
            
        }
    }
    public struct Nothing : IComponent{}


    public interface IService { }
    public delegate void System1<TQuery>(ref TQuery q) where TQuery : unmanaged, IQuery;
    public delegate void System2<TQuery>(ref TQuery q1, ref TQuery q2) where TQuery : unmanaged, IQuery;
    public delegate void System1AndService<TQuery, TService>(ref TQuery q1, ref TService service)
        where TQuery : unmanaged, IQuery, IService;

    internal class DelegateSystem1Runner<TQuery> : ISystemRunner
        where TQuery : unmanaged, IQuery {
        private IntPtr _fn;
        private TQuery _query;

        public DelegateSystem1Runner(IntPtr fn, TQuery q, string name) {
            _fn = fn;
            _query = q;
            Name = name;
        }
        public JobHandle Schedule(UpdateContext updateContext, ref State state) {
            new FunctionPointer<System1<TQuery>>(_fn).Invoke(ref _query);
            return state.Dependencies;
        }

        public void Run(ref State state) {
            new FunctionPointer<System1<TQuery>>(_fn).Invoke(ref _query);
        }

        public string Name { get; }
    }





    public delegate void SystemAction<T>(T t)
        where T : unmanaged, ISystemParam;

    public delegate void SystemActionNotGeneric1(IntPtr param0);
    public delegate void SystemAction<T1, T2>(T1 t1, T2 t2)
        where T1 : unmanaged, ISystemParam where T2 : unmanaged, ISystemParam;
    public delegate void SystemActionNotGeneric2(IntPtr param0, IntPtr param1);
    public delegate void SystemAction<T1, T2, T3>(ref T1 t1, ref T2 t2, ref T3 t3) 
        where T1 : unmanaged, ISystemParam 
        where T2 : unmanaged, ISystemParam
        where T3 : unmanaged, ISystemParam;
    public delegate void SystemActionNotGeneric3(IntPtr param0, IntPtr param1, IntPtr param2);
    public delegate void SystemAction<T1, T2, T3, T4>(T1 t1, T2 t2, T3 t3, T4 t4) 
        where T1 : unmanaged, ISystemParam 
        where T2 : unmanaged, ISystemParam
        where T3 : unmanaged, ISystemParam
        where T4 : unmanaged, ISystemParam;
    public delegate void SystemActionNotGeneric4(IntPtr param0, IntPtr param1, IntPtr param2, IntPtr param3);
}
﻿using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AOT;
using Unity.Collections;
using Unity.Jobs.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;
    using Unity.Burst;
    using Unity.Collections.LowLevel.Unsafe;
    using UnityEngine;

    public sealed class NoComponentException : Exception
    {
        public NoComponentException(string msg) : base(msg)
        {

        }
    }
    public interface IComponent { }
    public interface IArrayComponent { }
    public interface IReactive { }
    public interface ICustomConvertor {
        void Convert(ref World world, ref Entity entity);
    }
    // public interface IDisposable<T> {
    //     void Dispose(ref T value);
    // }
    public interface ICopyable<T> {
        T Copy(ref T toCopy, int to);
    }
    public abstract class Convertor : ScriptableObject, ICustomConvertor {
        public abstract void Convert(ref World world, ref Entity entity);
    }
    public struct Changed<T> : IComponent where T : unmanaged, IComponent {}

    public struct Reactive<T> : IComponent where T : unmanaged, IComponent
    {
        public T oldValue;
    }
    public struct DestroyEntity : IComponent { }
    public struct EntityCreated : IComponent { }
    public struct IsPrefab : IComponent { }
    public struct ChildOf : IComponent {
        public Entity Value;
    }

    public sealed class UseWith : Attribute
    {
        public Type[] types;
        public UseWith(params Type[] componets)
        {
            types = componets;
        }
    }

    public struct Child : IEquatable<Child>, IArrayComponent {
        public Entity Value;
        public bool Equals(Child other) {
            return Value == other.Value;
        }
    }
    public struct Input : IComponent {
        public float h;
        public float v;
        public bool fire;
        public bool use;

        public Unity.Mathematics.float2 Axis
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get=> new (h, v);
        }
    }
    internal static partial class ComponentList
    {
        public readonly static IReadOnlyList<Type> DefaultComponents = new List<Type>()
        {
            typeof(global::Wargon.Nukecs.ChildOf),
            typeof(global::Wargon.Nukecs.ChildOf),
            typeof(global::Wargon.Nukecs.ChildOf),
            typeof(global::Wargon.Nukecs.ChildOf),
            typeof(global::Wargon.Nukecs.ChildOf),
            typeof(global::Wargon.Nukecs.ChildOf),
            typeof(global::Wargon.Nukecs.ChildOf),

        };
    }
    public struct ComponentAmount {
        /// <summary>
        /// Component types amount total
        /// </summary>
        public static readonly SharedStatic<int> Value = SharedStatic<int>.GetOrCreate<ComponentAmount>();
    }
    internal struct Component {
        /// <summary>
        /// Components count that are using right now
        /// </summary>
        public static readonly SharedStatic<int> Count = SharedStatic<int>.GetOrCreate<Component>();
        private static bool _initialized;
        static Component() {
            Count.Data = 0;
        }
        
        [BurstDiscard]
        public static void Initialization() {

            if(_initialized) return;
            ComponentTypeMap.Init();
            var count = 0;
            var componentTypes = new List<(Type, int)>();
            var arrayElementTypes = new List<(Type,int)>();
            
            Generated.GeneratedComponentList.InitializeComponentList();
            var components = Generated.GeneratedComponentList.GetAllComponents();
            dbug.log(components.ToList().Count.ToString());
            foreach (var component in components)
            {
                if(component == typeof(IComponent)) continue;
                //Debug.Log(component);
                //ComponentTypeMap.InitializeComponentTypeReflection(component, count);
                componentTypes.Add((component, count));
                if (component.IsGenericType && component.GetGenericTypeDefinition() == typeof(ComponentArray<>))
                {
                    //ComponentTypeMap.InitializeArrayElementTypeReflection(component.GetGenericArguments()[0], count);
                    arrayElementTypes.Add((component.GetGenericArguments()[0], count));
                    count++;
                }
                count++;
            }
            ComponentAmount.Value.Data = count;
            
            // ComponentAmount.Value.Data = count;
            foreach (var (type, index) in componentTypes)
            {
                ComponentTypeMap.InitializeComponentTypeReflection(type, index);
            }
            foreach (var (type, index) in arrayElementTypes)
            {
                ComponentTypeMap.InitializeArrayElementTypeReflection(type, index);
            }
            Generated.GeneratedDisposeRegistryStatic.RegisterTypes();
            componentTypes.Clear();
            arrayElementTypes.Clear();
            // var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            // var componentTypes = new List<(Type, int)>();
            // var arrayElementTypes = new List<(Type,int)>();
            // foreach (var assembly in assemblies) {
            //     var types = assembly.GetTypes();
            //     foreach (var type in types) {
            //         if (typeof(IComponent).IsAssignableFrom(type) && type != typeof(IComponent)) {
            //             if (type.IsGenericType)
            //             {
            //                 var args = FindGenericUsages(type, assembly);
            //                 foreach (var type1 in args)
            //                 {
            //                     //componentTypes.Add((type1, count));
            //                     dbug.log(type1.Name);
            //                 }
            //                 continue;
            //             }
            //             componentTypes.Add((type, count));
            //             count++;
            //         }
            //         if (typeof(IArrayComponent).IsAssignableFrom(type) && type != typeof(IArrayComponent)) {
            //             arrayElementTypes.Add((type, count));
            //             count++;
            //             count++;
            //         }
            //     }
            // }
            // ComponentAmount.Value.Data = count;
            // foreach (var (type, index) in componentTypes)
            // {
            //     
            // }
            // foreach (var (type, index) in arrayElementTypes)
            // {
            //     ComponentTypeMap.InitializeComponentArrayTypeReflection(type, index);
            // }
            //componentTypes.Clear();
           // arrayElementTypes.Clear();
            _initialized = true;
            
        }

        internal static void LogComponent(ComponentType type)
        {
            if (NukecsDebugData.Instance.showInitedComponents)
            {
                Debug.Log(type.ToString());
            }
        }
        public static IEnumerable<Type> FindGenericUsages(Type genericTypeDefinition, Assembly assembly)
        {
            if (!genericTypeDefinition.IsGenericTypeDefinition)
            {
                throw new ArgumentException("Переданный тип должен быть определением дженерик-типа", nameof(genericTypeDefinition));
            }

            //var assembly = Assembly.GetExecutingAssembly(); // Или укажите нужную сборку
        
            return assembly.GetTypes()
                .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .Concat<MemberInfo>(t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                    .Concat(t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)))
                .SelectMany(member => GetGenericArguments(member, genericTypeDefinition))
                .Distinct();
        }

        private static IEnumerable<Type> GetGenericArguments(MemberInfo member, Type genericTypeDefinition)
        {
            Type memberType = null;

            if (member is FieldInfo fieldInfo)
                memberType = fieldInfo.FieldType;
            else if (member is PropertyInfo propertyInfo)
                memberType = propertyInfo.PropertyType;
            else if (member is MethodInfo methodInfo)
                memberType = methodInfo.ReturnType;

            if (memberType != null && memberType.IsGenericType && memberType.GetGenericTypeDefinition() == genericTypeDefinition)
            {
                return memberType.GetGenericArguments();
            }

            return Enumerable.Empty<Type>();
        }
    }

    public static class MemoryDebug
    {
        public static void LogMemoryStatus(string label = "")
        {
            long totalMemory = GC.GetTotalMemory(false);
            Debug.Log($"{label} - Total Memory: {totalMemory} bytes");
        }

        public static unsafe void CheckAndLogBuffer(byte* buffer, int size, string label = "")
        {
            for (int i = 0; i < size; i++)
            {
                if (buffer[i] != 0)
                {
                    Debug.Log($"{label} - Buffer at index {i} has value: {buffer[i]}");
                }
            }
        }
    }

    internal static class ComponentHelpers
    {
        static ComponentHelpers() {
            writers = new IUnsafeBufferWriter[ComponentAmount.Value.Data];
            disposers = new IComponentDisposer[ComponentAmount.Value.Data];
            coppers = new IComponentCopper[ComponentAmount.Value.Data];
        }
        private static readonly IUnsafeBufferWriter[] writers;
        private static readonly IComponentDisposer[] disposers;
        private static readonly IComponentCopper[] coppers;
        internal static void CreateWriter<T>(int typeIndex) where T : unmanaged {
            writers[typeIndex] = new UnsafeBufferWriter<T>();
        }
        internal static void CreateDisposer<T>(int typeIndex)  where T : unmanaged{
            disposers[typeIndex] = new ComponentDisposer<T>();
        }
        
        internal static void CreateCopper<T>(int typeIndex)  where T : unmanaged{
            coppers[typeIndex] = new ComponentCopper<T>();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void Write(void* buffer, int index, int sizeInBytes, int typeIndex, IComponent component){
            writers[typeIndex].Write(buffer, index, sizeInBytes, component);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [BurstDiscard]
        internal static unsafe void Dispose(byte* buffer, int index, int typeIndex){
            disposers[typeIndex].Dispose(buffer, index);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void Copy(void* buffer, int from, int to,int typeIndex){
            coppers[typeIndex].Copy(buffer, from, to);
        }
    }

    public unsafe interface IUnsafeBufferWriter {
        void Write(void* buffer, int index, int sizeInBytes, IComponent component);
    }
    public class UnsafeBufferWriter<T> : IUnsafeBufferWriter  where T: unmanaged {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Write(void* buffer, int index, int sizeInBytes, IComponent component) {
            //*(T*) (buffer + index * sizeInBytes) = (T)component;
            //UnsafeUtility.WriteArrayElement(buffer, index, (T)component);
            ((T*) buffer)[index] = (T)component;
        }
    }
    
    public unsafe interface IComponentDisposer {
        void Dispose(byte* buffer, int index);
    }

    public class ComponentDisposer<T> : IComponentDisposer where T : unmanaged {
        private delegate void DisposeDelegate();

        private readonly DisposeDelegate _disposeFunc;
#if ENABLE_IL2CPP && !UNITY_EDITOR
        T _fakeInstance;
#endif
        public ComponentDisposer() {
            var dispose = typeof(T).GetMethod(nameof(IDisposable.Dispose));
            if (dispose == null) {
                throw new Exception (
                    $"IDispose<{typeof (T)}> explicit implementation not supported, use implicit instead.");
            }
            _disposeFunc = (DisposeDelegate)Delegate.CreateDelegate(typeof(DisposeDelegate),
#if ENABLE_IL2CPP && !UNITY_EDITOR
                    _fakeInstance,
#else
                null,
#endif
                dispose);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Dispose(byte* buffer, int index) {
            //ref var component  = ref ((T*)buffer)[index];
            _disposeFunc.Invoke();
        }
    }

    public readonly unsafe struct DisposeInfo
    {
        public readonly byte* buffer;
        public readonly int type;
        public readonly int entity;

        public DisposeInfo(int type, int entity, byte* buffer)
        {
            this.type = type;
            this.entity = entity;
            this.buffer = buffer;
        }
    }
    public unsafe struct DisposeComponentsSystem : ISystem, IOnCreate, IOnDestroy
    {
        ParallelNativeList<DisposeInfo> toDispose;
        public void OnCreate(ref World world)
        {
            toDispose = new ParallelNativeList<DisposeInfo>(128);
        }
        public void OnUpdate(ref State state)
        {
            foreach (var disposeInfo in toDispose)
            {
                ComponentHelpers.Dispose(disposeInfo.buffer, disposeInfo.entity, disposeInfo.type);
            }
        }
        public void OnDestroy(ref World world)
        {
            toDispose.Dispose();
        }
    }

    public unsafe struct ParallelNativeList<T> : IDisposable where T : unmanaged
    {
        private UnsafePtrList<UnsafeList<T>>* parallelList;

        public ParallelNativeList(int capacity, Allocator allocator = Allocator.Persistent)
        {
            var threads = JobsUtility.ThreadIndexCount;
            parallelList = UnsafePtrList<UnsafeList<T>>.Create(threads, allocator);
            for (int i = 0; i < threads; i++)
            {
                parallelList->Add(UnsafeList<T>.Create(capacity, allocator));
            }
        }

        public int Count()
        {
            int c = 0;
            for (int i = 0; i < parallelList->m_length; i++)
            {
                ref var list = ref parallelList->ElementAt(i);
                c += list->m_length;
            }

            return c;
        }
        public void Add(in T item, int thread)
        {
            parallelList->ElementAt(thread)->Add(item);
        }

        public Enumarator GetEnumerator()
        {
            ref var list = ref parallelList->ElementAt(0);
            return new Enumarator
            {
                maxIndex = JobsUtility.MaxJobThreadCount,
                index = 0,
                thread = 0,
                maxThread = list->m_length,
                list = list
            };
        }
        public struct Enumarator
        {
            internal int maxIndex;
            internal int index;
            internal int thread;
            internal int maxThread;
            internal UnsafePtrList<UnsafeList<T>>* parallelList;
            internal UnsafeList<T>* list;
            private T current;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                index++;
                if (index >= maxIndex)
                {
                    while (parallelList->ElementAt(thread)->m_length < 1)
                    {
                        thread++;
                        if (thread == maxThread) return false;
                    }
                    list = parallelList->ElementAt(thread);
                    index = 0;
                }
                current = list->Ptr[index];
                return true;
            }
            public T Current => current;
        }

        public void Dispose()
        {
            for (int i = 0; i < parallelList->m_length; i++)
            {
                parallelList->ElementAt(i)->Dispose();
            }
            UnsafePtrList<UnsafeList<T>>.Destroy(parallelList);
        }
    }
    
    public static unsafe class DisposeRegistryStatic<T> where T : unmanaged, IDisposable
    {
        [BurstCompile(CompileSynchronously = true)]
        [AOT.MonoPInvokeCallback(typeof(DisposeDelegate))]
        public static void Dispose(byte* buffer, int index) {
            //UnsafeUtility.AsRef<T>((T*)comp).Dispose();
            ((T*)buffer)[index].Dispose();
        }

        internal static IntPtr CreatePtr()
        {
            return BurstCompiler.CompileFunctionPointer<DisposeDelegate>(Dispose).Value;
        }

        public static void Register()
        {
            var componentType = ComponentTypeMap.GetComponentType(typeof(T));
            componentType.disposeFn = CreatePtr();
            ComponentTypeMap.SetComponentType<T>(componentType);
            //Generated.GeneratedDisposeRegistryStatic.fn[ComponentType<T>.Index] = CreatePtr();
        }
        public static void Register(ref ComponentType componentType)
        {
            componentType.disposeFn = CreatePtr();
        }
    }

    public struct TestArrayComponnet : IArrayComponent
    {
        
    }

    public struct TestDisposable : IDisposable, IComponent
    {
        public void Dispose()
        {
            
        }
    }


    public unsafe delegate void DisposeDelegate(byte* comp, int index);

    public readonly struct UnmanagedDelegate<T> : IDisposable where T : Delegate
    {
        public UnmanagedDelegate(T function)
        {
#if UNITY_EDITOR
            var method = function.Method;
            if (method == null || !method.IsStatic ||
                method.GetCustomAttributes(typeof(AOT.MonoPInvokeCallbackAttribute), false).Length == 0)
            {
                throw new Exception(
                    "Unmanaged delegate may only be created from static method with MonoPInvokeCallback attribute");
            }
#endif
            _gcHandle = GCHandle.Alloc(function);
            ptr = new FunctionPointer<T>(Marshal.GetFunctionPointerForDelegate(function));
        }

        public readonly FunctionPointer<T> ptr;
        private readonly GCHandle _gcHandle;

        public void Dispose()
        {
            _gcHandle.Free();
        }
    }
    public unsafe interface IComponentCopper {
        void Copy(void* buffer, int from, int to);
    }

    public class ComponentCopper<T> : IComponentCopper where T : unmanaged {
        private delegate T CopyDelegate(ref T value, int to);
        private readonly CopyDelegate _copyFunc;
#if ENABLE_IL2CPP && !UNITY_EDITOR
        T _fakeInstance;
#endif
        public ComponentCopper() {
            var copy = typeof(T).GetMethod(nameof(ICopyable<T>.Copy));
            if (copy == null) {
                throw new Exception (
                    $"IDispose<{typeof (T).Name}> explicit implementation not supported, use implicit instead.");
            }
            _copyFunc = (CopyDelegate)Delegate.CreateDelegate(typeof(CopyDelegate),
#if ENABLE_IL2CPP && !UNITY_EDITOR
                    _fakeInstance,
#else
                null,
#endif
                copy);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Copy(void* buffer, int from, int to) {
            ref var component  = ref UnsafeUtility.ArrayElementAsRef<T>(buffer, from);
            UnsafeUtility.WriteArrayElement(buffer, to, _copyFunc.Invoke(ref component, to));

        }
    }
    public interface IPool {
        
    }
    public sealed class ManagedPool<T> : IPool where T : struct {
        internal T[] data;
        public ManagedPool(int size) {
            data = new T[size];
        }
    }

    public static class ManagedPoolExtensions {
        internal static NativePool<T> AsNative<T>(this ManagedPool<T> pool) where T : unmanaged {
            return new NativePool<T>(pool.data);
        }
    }
    public unsafe struct NativePool<T> : IDisposable where T : unmanaged {
        private readonly T* _ptr;
        private GCHandle _handle;
        public NativePool(T[] array) {
            _handle =  GCHandle.Alloc(array, GCHandleType.Pinned);
            fixed (T* p = array) {
                _ptr = p;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetRef(int index) {
            return ref *(_ptr + index);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index, in T value) {
            *(_ptr + index) = value;
        }
        
        public void Dispose() {
            _handle.Free();
        }
    }

    
    
    public unsafe struct GetRef<TComponent> where TComponent : unmanaged, IComponent
    {
        internal int index;
        [NativeDisableUnsafePtrRestriction]
        private readonly GenericPool.GenericPoolUnsafe* buffer;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal GetRef(ref GenericPool pool)
        {
            index = 0;
            buffer = pool.UnsafeBuffer;
        }
        public readonly ref TComponent Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref buffer->GetRef<TComponent>(index);
        }
        public static implicit operator TComponent(in GetRef<TComponent> getRef)
        {
            return getRef.Value;
        }
    }

    public interface IAspect
    {
        Entity Entity { get;}
    }
    public interface IAspect<TComponent> : IAspect 
        where TComponent : unmanaged, IComponent 
    { }
    public interface IAspect<TComponent1, TComponent2> : IAspect 
        where TComponent1 : unmanaged, IComponent 
        where TComponent2 : unmanaged, IComponent
    { }
    public interface IAspect<TComponent1, TComponent2, TComponent3> : IAspect 
        where TComponent1 : unmanaged, IComponent 
        where TComponent2 : unmanaged, IComponent
        where TComponent3 : unmanaged, IComponent
    { }
    public interface IAspect<TComponent1, TComponent2, TComponent3, TComponent4> : IAspect 
        where TComponent1 : unmanaged, IComponent 
        where TComponent2 : unmanaged, IComponent
        where TComponent3 : unmanaged, IComponent
        where TComponent4 : unmanaged, IComponent
    { }
    public interface IAspect<TComponent1, TComponent2, TComponent3, TComponent4, TComponent5> : IAspect 
        where TComponent1 : unmanaged, IComponent 
        where TComponent2 : unmanaged, IComponent
        where TComponent3 : unmanaged, IComponent
        where TComponent4 : unmanaged, IComponent
        where TComponent5 : unmanaged, IComponent
    { }
    public interface IAspect<TComponent1, TComponent2, TComponent3, TComponent4, TComponent5, TComponent6> : IAspect 
        where TComponent1 : unmanaged, IComponent 
        where TComponent2 : unmanaged, IComponent
        where TComponent3 : unmanaged, IComponent
        where TComponent4 : unmanaged, IComponent
        where TComponent5 : unmanaged, IComponent
        where TComponent6 : unmanaged, IComponent
    { }
    public struct PlayerAspect : IAspect
    {
        public Entity Entity { get; }
        public GetRef<Input> Input;
        public GetRef<Transforms.Transform> Transform;
    }

    public struct ComponentsTuple<T1, T2> : ITuple where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
    {
        private int entity;
        public GetRef<T1> Value1;
        public GetRef<T2> Value2;
        public object this[int index] => null;

        public int Length => 2;
        public void Deconstruct(out GetRef<T1> v1, out GetRef<T2> v2)
        {
            v1 = Value1;
            v2 = Value2;
        }
    }

    public readonly ref struct ComponentTupleRO<T1, T2, T3>
    {
        public readonly T1 value1;
        public readonly T2 value2;
        public readonly T3 value3;

        public ComponentTupleRO(in T1 v1, in T2 v2, in T3 v3)
        {
            value1 = v1;
            value2 = v2;
            value3 = v3;
        }
        public void Deconstruct(out T1 v1, out T2 v2, out T3 v3)
        {
            v1 = value1;
            v2 = value2;
            v3 = value3;
        }
    }


}

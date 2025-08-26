namespace Wargon.Nukecs
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Unity.Burst;
    using Unity.Collections.LowLevel.Unsafe;
    using UnityEngine;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Unity.Collections;
    using Collections;
    
    public sealed class DrawAsAttribute : Attribute
    {
        public Type drawerType;
        public DrawAsAttribute(Type drawerType)
        {
            this.drawerType = drawerType;
        }
    }
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
    public unsafe interface IOnPoolResize
    {
        void OnPoolResize(byte* buffer);
    }
    public interface ICopyable<T> {
        T Copy(int to);
    }

    public abstract class Convertor : ScriptableObject, ICustomConvertor {
        public abstract void Convert(ref World world, ref Entity entity);
    }
    public struct Changed<T> : IComponent where T : unmanaged, IComponent {}
    public struct Reactive<T> : IComponent where T : unmanaged, IComponent
    {
        public T oldValue;
    }

    public struct Name : IComponent, IDisposable
    {
        public ObjectRef<string> value;

        public Name(string val)
        {
            value = val;
        }
        public void Dispose()
        {
            value.DisposeNotRemoving();
        }
    }
    
    public unsafe struct Events<T> where T : unmanaged
    {
        private MemoryList<T> _list;
        private readonly World.WorldUnsafe* _world;

        public Events(int capacity, World.WorldUnsafe* world)
        {
            _list = new MemoryList<T>(capacity, ref world->AllocatorRef);
            _world = world;
        }
        
        public void Add(T item)
        {
            _list.Add(item, ref _world->AllocatorRef);
        }

        public void Clear()
        {
            _list.Clear();
        }
        public MemoryList<T>.Enumerator GetEnumerator()
        {
            return _list.GetEnumerator();
        }
    }
    
    public struct DestroyEntity : IComponent { }
    public struct EntityCreated : IComponent { }
    public struct IsPrefab : IComponent { }
    public struct ChildOf : IComponent {
        public Entity Value;
    }
#if NUKECS_DEBUG
    public struct DebugView : IComponent {}
#endif
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
        public static readonly IReadOnlyList<Type> DefaultComponents = new System.Collections.Generic.List<Type>()
        {
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
        internal static bool _initialized;
        static Component()
        {
            _initialized = false;
        }
        
        [BurstDiscard]
        public static void Initialization() {

            if(_initialized) return;
            
            var count = 0;
            var componentTypes = new System.Collections.Generic.List<(Type, int)>();
            var arrayElementTypes = new System.Collections.Generic.List<(Type,int)>();
            ComponentTypeData.Init();
            Generated.GeneratedComponentList.InitializeComponentList();
            var components = Generated.GeneratedComponentList.GetAllComponents();
            //dbug.log(components.ToList().Count.ToString());
            foreach (var component in components)
            {
                if(component == typeof(IComponent)) continue;

                componentTypes.Add((component, count));
                if (component.IsGenericType && component.GetGenericTypeDefinition() == typeof(ComponentArray<>))
                {
                    arrayElementTypes.Add((component.GetGenericArguments()[0], count));
                    count++;
                }
                count++;
            }
            
            ComponentAmount.Value.Data = count;
            ComponentTypeMap.Init();
            foreach (var (type, index) in componentTypes)
            {
                ComponentTypeMap.InitializeComponentTypeReflection(type, index);
            }
            
            foreach (var (type, index) in arrayElementTypes)
            {
                ComponentTypeMap.InitializeArrayElementTypeReflection(type, index);
            }
            
            Generated.GeneratedDisposeRegistryStatic.EnsureGenericMethodInstantiation();
            Generated.GeneratedDisposeRegistryStatic.RegisterTypes();
            
            componentTypes.Clear();
            arrayElementTypes.Clear();
            
            _initialized = true;
        }

        internal static void LogComponent(ComponentTypeData typeData)
        {
            if (NukecsDebugData.Instance.showInitedComponents)
            {
                Debug.Log(typeData.ToString());
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
            readers = new IUnsafeBufferReader[ComponentAmount.Value.Data];
        }
        private static readonly IUnsafeBufferWriter[] writers;
        private static readonly IUnsafeBufferReader[] readers;
        internal static void CreateWriter<T>(int typeIndex) where T : unmanaged {
            writers[typeIndex] = new UnsafeBufferWriter<T>();
            readers[typeIndex] = new UnsafeBufferReader<T>();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void Write(void* buffer, int index, int sizeInBytes, int typeIndex, IComponent component){
            writers[typeIndex].Write(buffer, index, sizeInBytes, component);
        }

        internal static unsafe IComponent Read(void* buffer, int index, int sizeInBytes, int type)
        {
            return readers[type].Read(buffer, index, sizeInBytes);
        }
    }

    public unsafe interface IUnsafeBufferWriter {
        void Write(void* buffer, int index, int sizeInBytes, IComponent component);
    }
    public class UnsafeBufferWriter<T> : IUnsafeBufferWriter  where T: unmanaged {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Write(void* buffer, int index, int sizeInBytes, IComponent component) {
            ((T*) buffer)[index] = (T)component;
        }
    }

    public unsafe interface IUnsafeBufferReader
    {
        IComponent Read(void* buffer, int index, int sizeInBytes);
    }

    public class UnsafeBufferReader<T> : IUnsafeBufferReader  where T: unmanaged
    {
        public unsafe IComponent Read(void* buffer, int index, int sizeInBytes)
        {
            return (IComponent)((T*) buffer)[index];
        }
    }
    
    public unsafe interface IComponentDisposer {
        void Dispose(byte* buffer, int index);
    }

    public struct TestDisposable : IDisposable, IComponent
    {
        public void Dispose()
        {
        }
    }

    public readonly struct UntypedUnmanagedDelegate : IDisposable
    {
        private readonly IntPtr _ptr;
        private readonly GCHandle _gcHandle;

        public T As<T>()
        {
            return new FunctionPointer<T>(_ptr).Invoke;
        }
        
        public UntypedUnmanagedDelegate(IntPtr ptr, GCHandle gcHandle)
        {
            _ptr = ptr;
            _gcHandle = gcHandle;
        }
        public static UntypedUnmanagedDelegate Create<T>(T function) where T : Delegate
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
            return new UntypedUnmanagedDelegate(Marshal.GetFunctionPointerForDelegate(function), GCHandle.Alloc(function));
        }

        public static unsafe UntypedUnmanagedDelegate* CreatePointer<T>(T function) where T : Delegate
        {
            UntypedUnmanagedDelegate* ptr = (UntypedUnmanagedDelegate*)UnsafeUtility.MallocTracked(sizeof(UntypedUnmanagedDelegate), UnsafeUtility.AlignOf<UntypedUnmanagedDelegate>(), Allocator.Persistent, 0);
            *ptr = Create(function);
            return ptr;
        }

        public static unsafe void Destroy(UntypedUnmanagedDelegate* untypedUnmanagedDelegate)
        {
            untypedUnmanagedDelegate->Dispose();
            UnsafeUtility.FreeTracked(untypedUnmanagedDelegate, Allocator.Persistent);
        }
        public void Dispose()
        {
            _gcHandle.Free();
        }
    }
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
            _ptr = new FunctionPointer<T>(Marshal.GetFunctionPointerForDelegate(function));
        }

        private readonly FunctionPointer<T> _ptr;
        private readonly GCHandle _gcHandle;

        public T Invoke
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _ptr.Invoke;
        }
        public void Dispose()
        {
            _gcHandle.Free();
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
        //Entity Entity { get; set; }
    }

    public interface IAspect<T> where T : unmanaged, IAspect
    {
        T Create(ref World world);
        void Update(ref Entity entity);
    }


    public unsafe struct AspectBuilder<T> where T : unmanaged, IAspect<T>, IAspect
    {
        private static readonly T factory;
        public static T Create(ref World world)
        {
            return factory.Create(ref world);
        }
        public static ptr<T> CreatePtr(ref World world)
        {
            var ptr = world.UnsafeWorld->_allocate_ptr<T>();
            ptr.Ref = factory.Create(ref world);
            return ptr;
        }
        public static void Destroy(T* pointer, ref World world)
        {
            world.UnsafeWorld->_free(pointer);
        }
    }

    // public unsafe struct AspectData<T> where T : unmanaged, IComponent
    // {
    //     internal T* Buffer;
    //     public ref T Value => ref *Buffer;
    //     public ref readonly T Read => ref *Buffer;
    //     [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //     public void Update(ref Entity entity, ref GenericPool pool)
    //     {
    //         Buffer = (T*)pool.UnsafeBuffer->buffer + entity.id;
    //     }
    // }
    public unsafe struct AspectData<T> where T : unmanaged, IComponent
    {
        internal T* Buffer;
        private int _entity;
        public ref T Value => ref Buffer[_entity];
        public ref readonly T Read => ref Buffer[_entity];
        public static implicit operator T(in AspectData<T> getRef)
        {
            return getRef.Value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(ref Entity entity)
        {
            _entity = entity.id;
        }
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

    public readonly unsafe struct UnsafeTuple<T1, T2, T3> where T1 : unmanaged where T2 : unmanaged where T3 : unmanaged {
        public readonly T1* value1;
        public readonly T2* value2;
        public readonly T3* value3;

        public UnsafeTuple(T1* t1, T2* t2, T3* t3) {
            value1 = t1;
            value2 = t2;
            value3 = t3;
        }
        public void Deconstruct(out T1* t1, out T2* t2, out T3* t3) {
            t1 = value1;
            t2 = value2;
            t3 = value3;
        }
    }
    public struct TestCopyDispose : IComponent, IDisposable, ICopyable<TestCopyDispose>
    {
        public void Dispose()
        {
            
        }

        public TestCopyDispose Copy(int to)
        {
            return new TestCopyDispose();
        }
    }
    public static unsafe class UnsafeAPI {
        public static ref T As<T>(void* ptr) where T: unmanaged {
            return ref *(T*) ptr;
        }
        public static ref T Index<T>(void* buffer, int index) where T : unmanaged {
            return ref ((T*)buffer)[index];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T ToComponent<T>(void* ptr) where T : unmanaged {
            return ref *(T*) ptr;
        }
    }
    
    public unsafe struct ClassPtr<T> : System.IEquatable<ClassPtr<T>> where T : class {

        [NativeDisableUnsafePtrRestriction]
        private System.IntPtr ptr;
        [NativeDisableUnsafePtrRestriction]
        private System.Runtime.InteropServices.GCHandle gcHandle;

        public bool IsValid => this.ptr.ToPointer() != null;

        public T Value => (T)this.gcHandle.Target;

        public ClassPtr(T data) {
            this.gcHandle = (data != null ? System.Runtime.InteropServices.GCHandle.Alloc(data) : default);
            this.ptr = System.Runtime.InteropServices.GCHandle.ToIntPtr(this.gcHandle);
        }

        public void Dispose() {
            if (this.gcHandle.IsAllocated == true) {
                this.gcHandle.Free();
            }
        }

        public bool Equals(ClassPtr<T> other) {
            return other.ptr == ptr;
        }
    }
}

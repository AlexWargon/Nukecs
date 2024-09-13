using System.Collections.Generic;

namespace Wargon.Nukecs {
    
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Unity.Burst;
    using Unity.Collections.LowLevel.Unsafe;
    using UnityEngine;

    public interface IComponent { }
    public interface IArrayComponent { }
    public interface ICustomConvertor {
        void Convert(ref World world, ref Entity entity);
    }
    public abstract class Convertor : ScriptableObject, ICustomConvertor {
        public abstract void Convert(ref World world, ref Entity entity);
    }
    public interface IDisposable<T> {
        void Dispose(ref T value);
    }

    public interface ICopyable<T> {
        T Copy(ref T toCopy);
    }
    public struct IsAlive : IComponent { }
    public struct DestroyEntity : IComponent { }
    public struct EntityCreated : IComponent { }
    public struct IsPrefab : IComponent { }
    //public struct Dispose<T> : IComponent where T : struct, IComponent{ }
    public struct ChildOf : IComponent {
        public Entity Value;
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
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var componentTypes = new List<(Type, int)>();
            var arrayElementTypes = new List<(Type,int)>();
            foreach (var assembly in assemblies) {
                var types = assembly.GetTypes();
                foreach (var type in types) {
                    if (typeof(IComponent).IsAssignableFrom(type) && type != typeof(IComponent)) {
                        componentTypes.Add((type, count));
                        count++;
                    }
                    if (typeof(IArrayComponent).IsAssignableFrom(type) && type != typeof(IArrayComponent)) {
                        arrayElementTypes.Add((type, count));
                        count++;
                    }
                }
            }
            ComponentAmount.Value.Data = count;
            foreach (var (type, index) in componentTypes)
            {
                ComponentTypeMap.InitializeComponentTypeReflection(type, index);
            }
            foreach (var (type, index) in arrayElementTypes)
            {
                ComponentTypeMap.InitializeComponentArrayTypeReflection(type, index);
            }
            componentTypes.Clear();
            arrayElementTypes.Clear();
            _initialized = true;
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
        internal static unsafe void Dispose(ref byte* buffer, int index,int typeIndex){
            disposers[typeIndex].Dispose(ref buffer, index);
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
        void Dispose(ref byte* buffer, int index);
    }

    public class ComponentDisposer<T> : IComponentDisposer where T : unmanaged {
        private delegate void DisposeDelegate(ref T value);

        private readonly DisposeDelegate _disposeFunc;
#if ENABLE_IL2CPP && !UNITY_EDITOR
        T _fakeInstance;
#endif
        public ComponentDisposer() {
            var dispose = typeof(T).GetMethod(nameof(IDisposable<T>.Dispose));
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
        public unsafe void Dispose(ref byte* buffer, int index) {
            ref var component  = ref ((T*)buffer)[index];
            _disposeFunc.Invoke(ref component);
        }
    }

    public unsafe interface IComponentCopper {
        void Copy(void* buffer, int from, int to);
    }

    public class ComponentCopper<T> : IComponentCopper where T : unmanaged {
        private delegate T CopyDelegate(ref T value);
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
            UnsafeUtility.WriteArrayElement(buffer, to, _copyFunc.Invoke(ref component));

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
    
}
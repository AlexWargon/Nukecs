namespace Wargon.Nukecs {
    
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;
    using UnityEngine;

    public interface IComponent { }

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
    public struct IsPrefab : IComponent { }
    public struct Dispose<T> : IComponent where T : struct, IComponent{ }
    public struct ChildOf : IComponent {
        public Entity Value;
    }
    public struct Child : IEquatable<Child> {
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
        public static readonly SharedStatic<int> Value = SharedStatic<int>.GetOrCreate<ComponentAmount>();
    }
    public struct Component {
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
            ComponentsMap.Init();
            var count = 0;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies) {
                var types = assembly.GetTypes();
                foreach (var type in types) {
                    if (typeof(IComponent).IsAssignableFrom(type) && type != typeof(IComponent)) {
                        count++;
                    }
                }
            }
            ComponentAmount.Value.Data = count;
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
        internal static unsafe void Write(void* buffer, int index, int sizeInBytes, int typeIndex, IComponent component){
            writers[typeIndex].Write(buffer, index, sizeInBytes, component);
        }
        internal static unsafe void Dispose(void* buffer, int index,int typeIndex){
            disposers[typeIndex].Dispose(buffer, index);
        }
        internal static unsafe void Copy(void* buffer, int from, int to,int typeIndex){
            coppers[typeIndex].Copy(buffer, from, to);
        }
    }
    public unsafe interface IUnsafeBufferWriter {
        void Write(void* buffer, int index, int sizeInBytes, IComponent component);
    }
    public class UnsafeBufferWriter<T> : IUnsafeBufferWriter  where T: unmanaged {
        public unsafe void Write(void* buffer, int index, int sizeInBytes, IComponent component) {
            //*(T*) (buffer + index * sizeInBytes) = (T)component;
            UnsafeUtility.WriteArrayElement(buffer, index, (T)component);
        }
    }
    
    public unsafe interface IComponentDisposer {
        void Dispose(void* buffer, int index);
    }

    public class ComponentDisposer<T> : IComponentDisposer where T : unmanaged {
        private delegate void DisposeDelegate(ref T value);

        private readonly DisposeDelegate disposeFunc;
#if ENABLE_IL2CPP && !UNITY_EDITOR
        T _fakeInstance;
#endif
        public ComponentDisposer() {
            var dispose = typeof(T).GetMethod(nameof(IDisposable<T>.Dispose));
            if (dispose == null) {
                throw new Exception (
                    $"IDispose<{typeof (T).Name}> explicit implementation not supported, use implicit instead.");
            }
            disposeFunc = (DisposeDelegate)Delegate.CreateDelegate(
                typeof(DisposeDelegate),
#if ENABLE_IL2CPP && !UNITY_EDITOR
                    _fakeInstance,
#else
                null,
#endif
                dispose);
        }
        public unsafe void Dispose(void* buffer, int index) {
            ref var component  = ref UnsafeUtility.ArrayElementAsRef<T>(buffer, index);
            disposeFunc.Invoke(ref component);
        }
    }

    public unsafe interface IComponentCopper {
        void Copy(void* buffer, int from, int to);
    }

    public class ComponentCopper<T> : IComponentCopper where T : unmanaged {
        private delegate T CopyDelegate(ref T value);
        private readonly CopyDelegate copyFunc;
#if ENABLE_IL2CPP && !UNITY_EDITOR
        T _fakeInstance;
#endif
        public ComponentCopper() {
            var copy = typeof(T).GetMethod(nameof(ICopyable<T>.Copy));
            if (copy == null) {
                throw new Exception (
                    $"IDispose<{typeof (T).Name}> explicit implementation not supported, use implicit instead.");
            }
            copyFunc = (CopyDelegate)Delegate.CreateDelegate(
                typeof(CopyDelegate),
#if ENABLE_IL2CPP && !UNITY_EDITOR
                    _fakeInstance,
#else
                null,
#endif
                copy);
        }
        public unsafe void Copy(void* buffer, int from, int to) {
            ref var component  = ref UnsafeUtility.ArrayElementAsRef<T>(buffer, from);
            UnsafeUtility.WriteArrayElement(buffer, to, copyFunc.Invoke(ref component));
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
            _handle.Free();
        }
        public ref T GetRef(int index) {
            return ref *(_ptr + index);
        }

        public void Set(int index, in T value) {
            *(_ptr + index) = value;
        }
        public void Dispose() {
            _handle.Free();
        }
    }
    public struct TransformIndex : IComponent {
        public int value;
    }


    [BurstCompile(CompileSynchronously = true)]
    public static class ComponentsArrayExtensions {
        [BurstCompile(CompileSynchronously = true)]
        public static int RemoveAtSwapBack<T>(this ref ComponentArray<T> buffer, in T item) where T: unmanaged, IEquatable<T> {
            int index = 0;
            for (int i = 0; i < buffer.list.Length; i++) {
                if (item.Equals(buffer.list.ElementAt(i))) {
                    index = i;
                    break;
                }
            }
            buffer.list.RemoveAtSwapBack(index);
            return buffer.list.Length - 1;
        }
    }

    public static class UnsafeListExtensions {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T ElementAtNoCheck<T>(this UnsafeList<T> list, int index) where T : unmanaged {
            return ref list.Ptr[index];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T* ElementAtNoCheck<T>(this UnsafePtrList<T> list, int index) where T: unmanaged{
            return ref list.Ptr[index];
        }
    }
    public static class UnsafeHelp {
        public static UnsafeList<T> UnsafeListWithMaximumLenght<T>(int size, Allocator allocator,
            NativeArrayOptions options) where T : unmanaged {
            return new UnsafeList<T>(size, allocator, options) {
                m_length = size
            };
        }

        public static unsafe UnsafeList<T>* UnsafeListPtrWithMaximumLenght<T>(int size, Allocator allocator,
            NativeArrayOptions options) where T : unmanaged {
            var ptr = UnsafeList<T>.Create(size, allocator, options);
            ptr->m_length = size;
            return ptr;
        }

        public static ref UnsafeList<T> ResizeUnsafeList<T>(ref UnsafeList<T> list, int size,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory) where T : unmanaged 
        {
            list.Resize(size, options);
            list.m_length = size;
            return ref list;
        }

        public static unsafe void ResizeUnsafeList<T>(ref UnsafeList<T>* list, int size,
            NativeArrayOptions options = NativeArrayOptions.UninitializedMemory) where T : unmanaged 
        {
            list->Resize(size, options);
            list->m_length = size;
        }

        public static int AlignOf(ComponentType type) {
            return type.size + sizeof(byte) * 2 - type.size;
        }
        public static int AlignOf(Type type) {
            return UnsafeUtility.SizeOf(type) + sizeof(byte) * 2 - UnsafeUtility.SizeOf(type);
        }

        public static unsafe T* Malloc<T>(Allocator allocator) where T : unmanaged {
            return (T*)UnsafeUtility.Malloc(sizeof(T), UnsafeUtility.AlignOf<T>(), allocator);
        }
        public static unsafe T* Malloc<T>(int elements, Allocator allocator) where T : unmanaged {
            return (T*)UnsafeUtility.Malloc(sizeof(T) * elements, UnsafeUtility.AlignOf<T>(), allocator);
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Resize<T>(int oldCapacity, int newCapacity, ref T* buffer, Allocator allocator) where T : unmanaged
        {
            // Calculate new capacity

            var typeSize = sizeof(T);
            // Allocate new buffer
            var newBuffer = (T*)UnsafeUtility.Malloc(
                newCapacity * typeSize,
                UnsafeUtility.AlignOf<T>(),
                allocator
            );

            if (newBuffer == null)  
            {
                throw new OutOfMemoryException("Failed to allocate memory for resizing.");
            }

            //UnsafeUtility.MemClear(newBuffer, newCapacity * impl->elementSize);
            // Copy old data to new buffer
            UnsafeUtility.MemCpy(newBuffer, buffer, oldCapacity * typeSize);

            // Free old buffer
            UnsafeUtility.Free(buffer, allocator);

            // Update impl
            buffer = newBuffer;
            //Debug.Log($"Resized ptr from {oldCapacity} to {newCapacity}");
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void CheckResize<T>(int index, ref int capacity, T* buffer, Allocator allocator) where T : unmanaged
        {
            if (index >= capacity)
            {
                // Calculate new capacity
                var newCapacity = math.max(capacity * 2, index + 1);
                var typeSize = sizeof(T);
                // Allocate new buffer
                var newBuffer = (T*)UnsafeUtility.Malloc(
                    newCapacity * sizeof(T),
                    UnsafeUtility.AlignOf<T>(),
                    allocator
                );

                if (newBuffer == null)
                {
                    throw new OutOfMemoryException("Failed to allocate memory for resizing.");
                }

                //UnsafeUtility.MemClear(newBuffer, newCapacity * impl->elementSize);
                // Copy old data to new buffer
                UnsafeUtility.MemCpy(newBuffer, buffer, capacity * typeSize);

                // Free old buffer
                UnsafeUtility.Free(buffer, allocator);

                // Update impl
                buffer = newBuffer;
                capacity = newCapacity;
            }
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void CheckResize<T>(int index, ref int capacity, void* buffer, Allocator allocator, int typeSize, int align) where T : unmanaged
        {
            if (index >= capacity)
            {
                // Calculate new capacity
                int newCapacity = math.max(capacity * 2, index + 1);
                // Allocate new buffer
                void* newBuffer = UnsafeUtility.Malloc(
                    newCapacity * sizeof(T),
                    align,
                    allocator
                );

                if (newBuffer == null)
                {
                    throw new OutOfMemoryException("Failed to allocate memory for resizing.");
                }

                //UnsafeUtility.MemClear(newBuffer, newCapacity * impl->elementSize);
                // Copy old data to new buffer
                UnsafeUtility.MemCpy(newBuffer, buffer, capacity * typeSize);

                // Free old buffer
                UnsafeUtility.Free(buffer, allocator);

                // Update impl
                buffer = newBuffer;
                capacity = newCapacity;
            }
        }
    }
}
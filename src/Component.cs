using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs {

    public interface IComponent { }
    public struct IsAlive : IComponent { }
    public struct DestroyEntity : IComponent { }
    public struct IsPrefab : IComponent { }
    public struct ChildOf : IComponent {
        public Entity Value;
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
    
    [Serializable]
    public struct ComponentType {
        public int size;
        public int index;
        public int align;
        public bool isTag;
    }
    public struct ComponentType<T> where T : unmanaged {
        internal static readonly SharedStatic<int> ID = SharedStatic<int>.GetOrCreate<ComponentType<T>>();

        public static unsafe int Index {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => *(int*) ID.UnsafeDataPointer;
        }
        static ComponentType() {
            Init();
        }
        [BurstDiscard]
        private static void Init() {
            ID.Data = Component.Count.Data++;
            ComponentsMap.Init();
            ComponentsMap.Add(typeof(T), ID.Data);
            ComponentsMap.AddComponentType(UnsafeUtility.AlignOf<T>(), Index, UnsafeUtility.SizeOf<T>());
            BoxedWriters.CreateWriter<T>(Index);
            //Debug.Log($"{typeof(T).Name} with id {id.Data}");
        }
    }

    internal struct ComponentsMap {
        private static ComponentsMapCache cache;
        internal static readonly SharedStatic<NativeHashMap<int, ComponentType>> ComponentTypes;
        private static bool _initialized = false;
        public static List<int> TypesIndexes => cache.TypesIndexes;
        static ComponentsMap() {
            ComponentTypes = SharedStatic<NativeHashMap<int, ComponentType>>.GetOrCreate<ComponentsMap>();
        }
        [BurstDiscard]
        public static void Init() {
            if(_initialized) return;
            cache = new ComponentsMapCache();
            ComponentTypes.Data =
                new NativeHashMap<int, ComponentType>(ComponentAmount.Value.Data + 1, Allocator.Persistent);
            _initialized = true;
        }

        public static void AddComponentType(int align, int index, int size) {
            ComponentTypes.Data.TryAdd(index, new ComponentType {
                align = align,
                size = size,
                index = index,
                isTag = size == 1
            });
        }
        public static ComponentType GetComponentType(int index) => ComponentTypes.Data[index];
        public static void Add(Type type, int index) {
            cache.Add(type, index);
        }
        public static Type GetType(int index) => cache.GetType(index);
        public static int Index(Type type) => cache.Index(type);
        public static int Index(string name) {
            return cache.Index(name);
        }
        static void StaticClass_Dtor(object sender, EventArgs e) {
            ComponentsMapCache.Save(cache);
        }

        public static void Save() {
            //ComponentsMapCache.Save(cache);
        }
        
    }

    [Serializable]
    public class ComponentsMapCache {
        private readonly Dictionary<int, Type> _typeByIndex = new();
        private readonly Dictionary<Type, int> _indexByType = new();
        private readonly Dictionary<string, Type> _nameToType = new();
        public readonly List<int> TypesIndexes = new();

        public void Add(Type type, int index) {
            _typeByIndex[index] = type;
            _indexByType[type] = index;
            if (TypesIndexes.Contains(index) == false)
                TypesIndexes.Add(index);
            _nameToType[type.FullName] = type;
        }

        public Type GetType(int index) => _typeByIndex[index];
        public int Index(Type type) => _indexByType[type];

        public int Index(string name) {
            return _indexByType[_nameToType[name]];
        }

        public static void Save(ComponentsMapCache mapCache) {
            var dataStream =
                new FileStream(Application.dataPath + "/Resources/ComponentsMap.nuke", FileMode.OpenOrCreate);
            var converter = new BinaryFormatter();
            converter.Serialize(dataStream, mapCache);
            dataStream.Close();
            //Debug.Log("SAVED");
        }

        public static ComponentsMapCache Load() {
            var filePath = Application.dataPath + "/Resources/ComponentsMap.nuke";
            ComponentsMapCache saveData;
            if (File.Exists(filePath)) {
                // File exists 
                var dataStream = new FileStream(filePath, FileMode.Open);
                var converter = new BinaryFormatter();
                saveData = converter.Deserialize(dataStream) as ComponentsMapCache;
                dataStream.Close();
                return saveData;
            }
            else {
                // File does not exist
                Debug.LogError("Save file not found in " + filePath);
                saveData = new ComponentsMapCache();
                Save(saveData);
                return saveData;
            }
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

    internal static class BoxedWriters
    {
        private static IUnsafeBufferWriter[] writers = new IUnsafeBufferWriter[8];

        internal static void CreateWriter<T>(int typeIndex) where T : unmanaged {
            if (typeIndex >= writers.Length) {
                Array.Resize(ref writers, typeIndex + 16);
            }
            writers[typeIndex] = new UnsafeBufferWriter<T>();
        }
        internal static unsafe void Write(void* buffer, int index, int sizeInBytes, int typeIndex, IComponent component){
            writers[typeIndex].Write(buffer, index, sizeInBytes, component);
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

    public struct Transforms {
        internal UnityEngine.Jobs.TransformAccessArray Array;
        internal World World;
        internal int lastFreeIndex;
        public void Add(ref Entity e, Transform transform) {
            var index = lastFreeIndex > 0 ? lastFreeIndex : Array.length;
            Array.Add(transform);
            e.Add(new TransformIndex{value = index});
        }
        
        public void Remove(ref Entity e, Transform transform) {
            lastFreeIndex = Array.length;
            Array.RemoveAtSwapBack(e.Get<TransformIndex>().value);
        }
    }

    public struct TransformIndex : IComponent {
        public int value;
    }

    public struct DynamicBuffer<T> : IComponent, IDisposable where T : unmanaged {
        internal UnsafeList<T> list;

        public DynamicBuffer(int capacity) {
            list = new UnsafeList<T>(capacity, Allocator.Persistent);
        }
        public ref T ElementAt(int index) => ref list.ElementAt(index);
        public void Add(in T item) {
            list.Add(in item);
        }
        public void RemoveAt(int index) {
            list.RemoveAt(index);
        }
        public void Dispose() {
            list.Dispose();
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public static class DynamicBufferExtensions {
        [BurstCompile(CompileSynchronously = true)]
        public static int RemoveAndSwapBack<T>(ref this DynamicBuffer<T> buffer, in T item) where T: unmanaged, IEquatable<T> {
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
            Debug.Log($"Resized list {size}");
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
            Debug.Log($"Resized ptr from {oldCapacity} to {newCapacity}");
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
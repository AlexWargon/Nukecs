using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Wargon.Nukecs {

    public interface IComponent {

    }

    public interface ISerializableComponent {
        
    }
    public struct ComponentAmount {
        public static readonly SharedStatic<int> Value = SharedStatic<int>.GetOrCreate<ComponentAmount>();
    }
    public struct Component {
        /// <summary>
        /// Components count that are using right now
        /// </summary>
        public static readonly SharedStatic<int> Count;

        private static bool initialized;
        static Component() {
            Count = SharedStatic<int>.GetOrCreate<Component>();
            Count.Data = 0;
            //Initialization();
        }
        
        [BurstDiscard]
        public static void Initialization() {
            if(initialized) return;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies) {
                var types = assembly.GetTypes();
                foreach (var type in types) {
                    if (typeof(Wargon.Nukecs.IComponent).IsAssignableFrom(type) && type != typeof(Wargon.Nukecs.IComponent)) {
                        //Debug.Log($"Component {type.Name} with id {ComponentAmount.Value.Data}");
                        ComponentAmount.Value.Data++;
                    }
                }
            }

            initialized = true;
        }
    }
    
    [Serializable]
    public class ComponentType {
        public FixedString512Bytes nativeName;
        public int SizeInBytes;
        public string Name;
    }
    public struct ComponentType<T> where T : unmanaged {
        private static readonly SharedStatic<int> id;

        public static int Index {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => id.Data;
        }
        static ComponentType() {
            id = SharedStatic<int>.GetOrCreate<ComponentType<T>>();
            id.Data = Component.Count.Data++;
            //Debug.Log($"{typeof(T).Name} with id {id.Data}");
            Init();
        }
        [BurstDiscard]
        private static void Init() {
            ComponentsMap.Add(typeof(T), UnsafeUtility.AlignOf<T>(), Index);
            BoxedWriters.CreateWriter<T>(Index);
        }
    }

    public static class ComponentsMap {
        private static ComponentsMapCache cache;
        public static List<int> TypesIndexes => cache.TypesIndexes;
        static ComponentsMap() {
            cache = ComponentsMapCache.Load();
        }

        public static void Add(Type type, int align, int index) {
            cache.Add(type, align, index);
        }
        public static int AlignOf(Type type) => cache.AlignOf(type);
        public static Type GetType(int index) => cache.GetType(index);
        public static int Index(Type type) => cache.Index(type);
        public static int Index(string name) {
            return cache.Index(name);
        }
        static void StaticClass_Dtor(object sender, EventArgs e) {
            ComponentsMapCache.Save(cache);
        }

        public static void Save() {
            ComponentsMapCache.Save(cache);
        }
        
    }

    [Serializable]
    public class ComponentsMapCache {
        private readonly Dictionary<Type, int> _aligns = new();
        private readonly Dictionary<int, Type> _typeByIndex = new();
        private readonly Dictionary<Type, int> _indexByType = new();
        private readonly Dictionary<string, Type> _nameToType = new();
        public readonly List<int> TypesIndexes = new();

        public void Add(Type type, int align, int index) {
            _aligns[type] = align;
            _typeByIndex[index] = type;
            _indexByType[type] = index;
            if(TypesIndexes.Contains(index) == false)
                TypesIndexes.Add(index);
            _nameToType[type.FullName] = type;
            Debug.Log(type.FullName);
        }

        public int AlignOf(Type type) => _aligns[type];
        public Type GetType(int index) => _typeByIndex[index];
        public int Index(Type type) => _indexByType[type];

        public int Index(string name) {
            return _indexByType[_nameToType[name]];
        }

        public static void Save(ComponentsMapCache mapCache) {
            FileStream dataStream =
                new FileStream(Application.dataPath + "/Resources/ComponentsMap.nuke", FileMode.OpenOrCreate);
            BinaryFormatter converter = new BinaryFormatter();
            converter.Serialize(dataStream, mapCache);
            dataStream.Close();
            Debug.Log("SAVED");
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

    [NativeContainer][BurstCompile]
    public unsafe struct GenericPool : IDisposable {
        [NativeDisableUnsafePtrRestriction] internal Impl* impl;
        public bool IsCreated;
        public static GenericPool Create<T>(int size, Allocator allocator) where T : unmanaged {
            return new GenericPool {
                impl = Impl.CreateImpl<T>(size, allocator),
                IsCreated = true
            };
        }
        [BurstDiscard]
        public static void Create<T>(int size, Allocator allocator, out GenericPool pool) where T : unmanaged {
            pool = Create<T>(size, allocator);
        }
        public static GenericPool Create(Type type, int size, Allocator allocator) {
            return new GenericPool {
                impl = Impl.CreateImpl(type, size, allocator),
                IsCreated = true
            };
        }

        public static GenericPool* CreatePtr<T>(int size, Allocator allocator) where T : unmanaged {
            var ptr = (GenericPool*) UnsafeUtility.Malloc(sizeof(GenericPool), UnsafeUtility.AlignOf<GenericPool>(),
                allocator);
            *ptr = new GenericPool {
                impl = Impl.CreateImpl<T>(size, allocator),
                IsCreated = true
            };
            return ptr;
        }
        [NativeContainer]
        internal struct Impl {
            [NativeDisableUnsafePtrRestriction] internal byte* buffer;
            internal int elementSize;
            internal int count;
            internal int capacity;
            internal int componentTypeIndex;
            internal Allocator allocator;

            internal static Impl* CreateImpl<T>(int size, Allocator allocator) where T : unmanaged {
                
                var ptr = (Impl*) UnsafeUtility.Malloc(sizeof(Impl), UnsafeUtility.AlignOf<Impl>(), allocator);
                *ptr = new Impl {
                    elementSize = sizeof(T),
                    capacity = size,
                    count = 0,
                    allocator = allocator,
                    buffer = (byte*) UnsafeUtility.Malloc(sizeof(T) * size, UnsafeUtility.AlignOf<T>(), allocator),
                    componentTypeIndex = ComponentType<T>.Index
                };
                UnsafeUtility.MemClear(ptr->buffer, (long) size * (long) sizeof(T));

                return ptr;
            }

            internal static Impl* CreateImpl(Type type, int size, Allocator allocator) {
                var ptr = (Impl*) UnsafeUtility.Malloc(sizeof(Impl), UnsafeUtility.AlignOf<Impl>(), allocator);
                var typeSize = UnsafeUtility.SizeOf(type);
                *ptr = new Impl {
                    elementSize = typeSize,
                    capacity = size,
                    count = 0,
                    allocator = allocator,
                    buffer = (byte*) UnsafeUtility.Malloc(typeSize * size, ComponentsMap.AlignOf(type), allocator),
                    componentTypeIndex = ComponentsMap.Index(type)
                };
                UnsafeUtility.MemClear(ptr->buffer, typeSize * size);
                return ptr;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set<T>(int index, T value) where T : unmanaged {
            // if (index < 0 || index >= impl->capacity) {
            //     throw new IndexOutOfRangeException(
            //         $"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
            // }
            UnsafeUtility.WriteArrayElement(impl->buffer, index, value);
            //*(T*) (impl->buffer + index * impl->elementSize) = value;
        }

        public ref T GetRef<T>(int index) where T : unmanaged {
            if (index < 0 || index >= impl->capacity) {
                throw new IndexOutOfRangeException(
                    $"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
            }

            return ref *(T*) (impl->buffer + index * impl->elementSize);
        }

        public void SetPtr(int index, void* value) {
            if (index < 0 || index >= impl->capacity) {
                throw new IndexOutOfRangeException(
                    $"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
            }

            *(impl->buffer + index * impl->elementSize) = *(byte*) value;
            if (index >= impl->count) {
                impl->count = index + 1;
            }
        }

        public void WriteBytes(int index, byte[] value) {
            //var target = impl->buffer + index * impl->elementSize;
            // fixed (byte* ptr = value) {
            //     UnsafeUtility.MemCpy(impl->buffer + index * impl->elementSize, ptr, impl->elementSize);
            // }
            for (var i = 0; i < impl->elementSize; i++) {
                impl->buffer[index * impl->elementSize+i] = value[i];
            }
        }
        public void WriteBytesUnsafe(int index, byte* value, int sizeInBytes) {
            var target = impl->buffer + index * impl->elementSize;
            UnsafeUtility.MemCpy(target, value, sizeInBytes);
        }

        public void SetObject(int index, IComponent component) {
            BoxedWriters.Write(impl->buffer, index, impl->elementSize, impl->componentTypeIndex, component);
        }
        public byte* GetComponentBytes(int index) {
            return impl->buffer + index * impl->elementSize;
        }
        public void Dispose() {
            if (impl == null) return;
            var allocator = impl->allocator;
            UnsafeUtility.Free(impl->buffer, allocator);
            impl->buffer = null;
            impl->count = 0;
            UnsafeUtility.Free(impl, allocator);
            IsCreated = false;
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
        private static readonly IUnsafeBufferWriter[] writers = new IUnsafeBufferWriter[ComponentAmount.Value.Data];

        [RuntimeInitializeOnLoadMethod]
        internal static void CreateWriter<T>(int typeIndex) where T : unmanaged {
            writers[typeIndex] = new UnsafeBufferWriter<T>();
        }
        internal static unsafe void Write(byte* buffer, int index, int sizeInBytes, int typeIndex, IComponent component){
            writers[typeIndex].Write(buffer, index, sizeInBytes, component);
        }
    }
    public unsafe interface IUnsafeBufferWriter {
        void Write(byte* buffer, int index, int sizeInBytes, IComponent component);
    }
    public class UnsafeBufferWriter<T> : IUnsafeBufferWriter  where T: unmanaged {
        public unsafe void Write(byte* buffer, int index, int sizeInBytes, IComponent component) {
            //*(T*) (buffer + index * sizeInBytes) = (T)component;
            UnsafeUtility.WriteArrayElement(buffer, index, (T)component);
        }
    }
}
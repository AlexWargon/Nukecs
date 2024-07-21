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

    public interface IComponent { }
    public struct IsAlive : IComponent { }
    public struct DestroyEntity : IComponent { }

    public struct ComponentAmount {
        public static readonly SharedStatic<int> Value = SharedStatic<int>.GetOrCreate<ComponentAmount>();
    }
    public struct Component {
        /// <summary>
        /// Components count that are using right now
        /// </summary>
        public static readonly SharedStatic<int> Count;

        private static bool _initialized;
        static Component() {
            Count = SharedStatic<int>.GetOrCreate<Component>();
            Count.Data = 0;
            //Initialization();
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
                        //Debug.Log($"Component {type.Name} with id {ComponentAmount.Value.Data}");
                        ComponentsMap.Add(type,count++);
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
    }
    public struct ComponentType<T> where T : unmanaged {
        internal static readonly SharedStatic<int> ID = SharedStatic<int>.GetOrCreate<ComponentType<T>>();

        public static int Index {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ID.Data;
        }
        static ComponentType() {
            Init();
        }
        [BurstDiscard]
        private static void Init() {
            ID.Data = ComponentsMap.Index(typeof(T));
            ComponentsMap.AddComponentType(UnsafeUtility.AlignOf<T>(), Index, UnsafeUtility.SizeOf<T>());
            BoxedWriters.CreateWriter<T>(Index);
            //Debug.Log($"{typeof(T).Name} with id {id.Data}");
        }
    }

    public struct ComponentsMap {
        private static ComponentsMapCache cache;
        public  static readonly SharedStatic<NativeHashMap<int, ComponentType>> ComponentTypes;
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
                index = index
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
            if(TypesIndexes.Contains(index) == false)
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

    public unsafe struct GenericPool : IDisposable {
        [NativeDisableUnsafePtrRestriction] internal Impl* impl;
        public bool IsCreated;
        public static GenericPool Create<T>(int size, Allocator allocator) where T : unmanaged {
            return new GenericPool {
                impl = Impl.CreateImpl<T>(size, allocator),
                IsCreated = true
            };
        }

        public static void Create<T>(int size, Allocator allocator, out GenericPool pool) where T : unmanaged {
            pool = Create<T>(size, allocator);
        }
        public static GenericPool Create(ComponentType type, int size, Allocator allocator) {
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
        [StructLayout(LayoutKind.Sequential)]
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
                UnsafeUtility.MemClear(ptr->buffer,size * sizeof(T));

                return ptr;
            }

            internal static Impl* CreateImpl(ComponentType type, int size, Allocator allocator) {
                var ptr = (Impl*) UnsafeUtility.Malloc(sizeof(Impl), UnsafeUtility.AlignOf<Impl>(), allocator);

                *ptr = new Impl {
                    elementSize = type.size,
                    capacity = size,
                    count = 0,
                    allocator = allocator,
                    buffer = (byte*) UnsafeUtility.Malloc(type.size * size, type.align, allocator),
                    componentTypeIndex = type.index
                };
                UnsafeUtility.MemClear(ptr->buffer, type.size * size);
                return ptr;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set<T>(int index, in T value) where T : unmanaged {
            if (index < 0 || index >= impl->capacity) {
                throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
            }
            *(T*) (impl->buffer + index * impl->elementSize) = value;
        }

        public ref T GetRef<T>(int index) where T : unmanaged {
            if (index < 0 || index >= impl->capacity) {
                throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
            }
            return ref *(T*) (impl->buffer + index * impl->elementSize);
            //return ref *(T*) (impl->buffer + index * impl->elementSize);
        }

        public void SetPtr(int index, void* value) {
            if (index < 0 || index >= impl->capacity) {
                throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
            }
            UnsafeUtility.MemCpy((impl->buffer + index * impl->elementSize), value, impl->elementSize);
            // *(impl->buffer + index * impl->elementSize) = *(byte*) value;
            // if (index >= impl->count) {
            //     impl->count = index + 1;
            // }
        }

        public void WriteBytes(int index, byte[] value) {
            if (index < 0 || index >= impl->capacity) {
                throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
            }
            fixed (byte* ptr = value) {
                UnsafeUtility.MemCpy(impl->buffer + index * impl->elementSize, ptr, impl->elementSize);
            }
            // for (var i = 0; i < impl->elementSize; i++) {
            //     impl->buffer[index * impl->elementSize+i] = value[i];
            // }
        }
        public void WriteBytesUnsafe(int index, byte* value, int sizeInBytes) {
            if (index < 0 || index >= impl->capacity) {
                throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
            }
            // var target = impl->buffer + index * impl->elementSize;
            UnsafeUtility.MemCpy(impl->buffer + index * impl->elementSize, value, sizeInBytes);
        }

        public void SetObject(int index, IComponent component) {
            if (index < 0 || index >= impl->capacity) {
                throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
            }
            BoxedWriters.Write(impl->buffer, index, impl->elementSize, impl->componentTypeIndex, component);
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
    // public unsafe struct Pools {
    //     public void** pools;
    //     public int amount;
    //     public Pools(int amount, Allocator allocator) {
    //         pools = (void**)UnsafeUtility.Malloc(sizeof(void*) * amount, UnsafeUtility.AlignOf<IntPtr>(), allocator);
    //         this.amount = amount;
    //     }
    //
    //     internal UnsafeList<T>* GetPool<T>(World.WorldImpl* world) where T : unmanaged {
    //         var index = ComponentType<T>.Index;
    //         var list =  (UnsafeList<T>*)pools[index];
    //         if (list == null) {
    //             list = UnsafeList<T>.Create(world->config.StartPoolSize, world->allocator,
    //                 NativeArrayOptions.ClearMemory);
    //             pools[index] = (IntPtr)list;
    //         }
    //         return list;
    //     }
    //
    //     internal void Dispose() {
    //         for (int i = 0; i < amount; i++) {
    //             var ptr = pools[i];
    //             
    //         }
    //     }
    // }
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
        public static void RemoveAndSwapBack<T>(ref this DynamicBuffer<T> buffer, in T item) where T: unmanaged, IEquatable<T> {
            int index = 0;
            for (int i = 0; i < buffer.list.Length; i++) {
                if (item.Equals(buffer.list.ElementAt(i))) {
                    index = i;
                    break;
                }
            }
            buffer.list.RemoveAtSwapBack(index);
        }
    }
}
using System.Linq;

namespace Wargon.Nukecs {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Runtime.Serialization.Formatters.Binary;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using UnityEngine;
    
    [Serializable]
    public struct ComponentType {
        public int size;
        public int index;
        public int align;
        public bool isTag;
        public bool isDisposable;
        public bool isCopyable;
        public unsafe void* defaultValue;
    }
    
    public struct ComponentType<T> where T : unmanaged {
        private static readonly SharedStatic<ComponentType> ID = SharedStatic<ComponentType>.GetOrCreate<ComponentType<T>>();

        public static unsafe int Index {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (*(ComponentType*) ID.UnsafeDataPointer).index;
        }

        internal static unsafe ref ComponentType Data
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref UnsafeUtility.AsRef<ComponentType>(ID.UnsafeDataPointer);
        }
        
        static ComponentType() {
            Init();
        }
        [BurstDiscard]
        private static void Init() {
            var id = Component.Count.Data++;
            ComponentsMap.Init();
            ComponentsMap.Add(typeof(T), id);
            ID.Data =  ComponentsMap.AddComponentType<T>(id);
            ComponentHelpers.CreateWriter<T>(id);
            if (ID.Data.isDisposable) {
                ComponentHelpers.CreateDisposer<T>(id);
            }
            if (ID.Data.isCopyable) {
                ComponentHelpers.CreateCopper<T>(id);
            }
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
            ComponentTypes.Data = new NativeHashMap<int, ComponentType>(ComponentAmount.Value.Data + 1, Allocator.Persistent);
            _initialized = true;
        }

        public static unsafe ComponentType AddComponentType<T>(int index) where T : unmanaged
        {
            var size = UnsafeUtility.SizeOf<T>();
            var data = new ComponentType
            {
                align = UnsafeUtility.AlignOf<T>(),
                size = size,
                index = index,
                isTag = size == 1,
                isDisposable = typeof(T).GetInterfaces()
                    .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDisposable<>)),
                isCopyable = typeof(T).GetInterfaces()
                    .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICopyable<>))
            };
            data.defaultValue = UnsafeUtility.Malloc(data.size, data.align, Allocator.Persistent);
            *(T*) data.defaultValue = default(T);
            ComponentTypes.Data.TryAdd(index, data);
            
            return data;
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

        public static unsafe void Dispose() {
            foreach (var kvPair in ComponentTypes.Data) {
                UnsafeUtility.Free(kvPair.Value.defaultValue, Allocator.Persistent);
            }
            ComponentTypes.Data.Dispose();
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
}
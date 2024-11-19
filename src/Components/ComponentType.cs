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
        public bool isArray;
        internal static readonly SharedStatic<NativeHashMap<int, ComponentType>> elementTypes;
        public static ref NativeHashMap<int, ComponentType> ElementTypes => ref elementTypes.Data;
        
        public unsafe void* defaultValue;
        internal IntPtr disposeFn;
        internal IntPtr copyFn;
        public Type ManagedType => ComponentTypeMap.GetType(index);
        public FunctionPointer<DisposeDelegate> DisposeFn()
        {
            return new FunctionPointer<DisposeDelegate>(disposeFn);
        }
        
        public FunctionPointer<CopyDelegate> CopyFn()
        {
            if (copyFn == IntPtr.Zero)
            {
                throw new NullReferenceException($"copyFn is null for type {ManagedType.Name}");
            }
            return new FunctionPointer<CopyDelegate>(copyFn);
        }
        
        static ComponentType() {
            elementTypes = SharedStatic<NativeHashMap<int, ComponentType>>.GetOrCreate<ComponentType>();
            elementTypes.Data = new NativeHashMap<int, ComponentType>(32, Allocator.Persistent);
        }

        internal static void AddElementType(ComponentType componentType, int index)
        {
            ElementTypes[index] = componentType;
        }
        public override string ToString() {
            return
                $"ComponentType: {ComponentTypeMap.GetType(index)}  Index = {index}, size = {size}, Tag={isTag}, Disposable={isDisposable}, Copyable={isCopyable}, IsArray={isArray}";
        }

        public string LogString()
        {
            return $"ComponentType: {ComponentTypeMap.GetType(index)}  Index = {index}, size = {size}, Tag?[{isTag}], Disposable?[{isDisposable}], Copyable?[{isCopyable}], IsArray?[{isArray}]";
        }
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
            ID.Data = ComponentTypeMap.GetComponentType<T>();
        }
    }

    internal struct ComponentTypeMap {
        private static ComponentsMapCache cache;
        internal static readonly SharedStatic<NativeHashMap<int, ComponentType>> ComponentTypes;
        private static readonly Dictionary<Type, ComponentType> ComponentTypesByTypes;
        private static bool _initialized = false;
        public static List<int> TypesIndexes => cache.TypesIndexes;
        static ComponentTypeMap() {
            ComponentTypes = SharedStatic<NativeHashMap<int, ComponentType>>.GetOrCreate<ComponentTypeMap>();
            ComponentTypesByTypes = new Dictionary<Type, ComponentType>();
        }
        [BurstDiscard]
        internal static void Init() {
            if(_initialized) return;
            cache = new ComponentsMapCache();
            ComponentTypes.Data = new NativeHashMap<int, ComponentType>(ComponentAmount.Value.Data + 1, Allocator.Persistent);
            
            _initialized = true;
        }
        [BurstDiscard]
        internal static void InitializeArrayElementTypeReflection(Type typeElement, int index)
        {
            var addElement = typeof(ComponentTypeMap).GetMethod(nameof(InitializeElementType));
            var addElementMethod = addElement.MakeGenericMethod(typeElement);
            addElementMethod.Invoke(null, new object[] { index });
        }
        [BurstDiscard]
        internal static void InitializeComponentArrayTypeReflection(Type typeElement, int index)
        {
            var addElement = typeof(ComponentTypeMap).GetMethod(nameof(InitializeElementType));
            var addElementMethod = addElement.MakeGenericMethod(typeElement);
            addElementMethod.Invoke(null, new object[] { index });
            
            var arrayType = typeof(ComponentArray<>);
            var type = arrayType.MakeGenericType(typeElement);
            var addComponent = typeof(ComponentTypeMap).GetMethod(nameof(InitializeComponentType));
            var genericMethod = addComponent.MakeGenericMethod(type);
            genericMethod.Invoke(null, new object[] { index });
        }
        [BurstDiscard]
        internal static void InitializeComponentTypeReflection(Type type, int index)
        {
            //dbug.log(type.FullName);
            //Debug.Log(type);
            var method = typeof(ComponentTypeMap).GetMethod(nameof(InitializeComponentType));
            var genericMethod = method.MakeGenericMethod(type);
            genericMethod.Invoke(null, new object[] { index });
        }
        public static void InitializeComponentType<T>(int index) where T : unmanaged
        {
            Add(typeof(T), index);
            var componentType = AddComponentType<T>(index);
            ComponentHelpers.CreateWriter<T>(index);
        }

        public static unsafe void InitializeElementType<T>(int index) where T : unmanaged, IArrayComponent
        {
            var size = sizeof(T);
            var data = new ComponentType
            {
                align = UnsafeUtility.AlignOf<T>(),
                size = size,
                index = index,
                isTag = false,
                isDisposable = false,
                isCopyable = false,
                isArray = false
            };
            ComponentType.AddElementType(data, index);
        }
        internal static unsafe ComponentType AddComponentType<T>(int index) where T : unmanaged
        {
            if (ComponentTypes.Data.ContainsKey(index)) return ComponentTypes.Data[index];
            var size = UnsafeUtility.SizeOf<T>();
            var data = new ComponentType
            {
                align = UnsafeUtility.AlignOf<T>(),
                size = size,
                index = index,
                isTag = size == 1,
                isDisposable = typeof(IDisposable).IsAssignableFrom(typeof(T)),
                isCopyable = typeof(T).GetInterfaces()
                    .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICopyable<>)),
                isArray = typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(ComponentArray<>),
            };


            data.defaultValue = UnsafeUtility.Malloc(data.size, data.align, Allocator.Persistent);
            *(T*) data.defaultValue = default(T);
            ComponentTypes.Data.TryAdd(index, data);
            ComponentTypesByTypes.TryAdd(typeof(T), data);
            return data;
        }
        
        public static ComponentType GetComponentType(int index) => ComponentTypes.Data[index];
        public static ComponentType GetComponentType<T>() => ComponentTypesByTypes[typeof(T)];

        public static void SetComponentType<T>(ComponentType componentType) where T : unmanaged
        {
            ComponentTypesByTypes[typeof(T)] = componentType;
            ComponentTypes.Data[ComponentType<T>.Index] =  componentType;
            ComponentType<T>.Data = componentType;
        }
        public static ComponentType GetComponentType(Type type) => ComponentTypesByTypes[type];
        internal static void Add(Type type, int index) {
            cache.Add(type, index);
        }
        public static Type GetType(int index) => cache.GetType(index);
        public static int Index(Type type) => cache.Index(type);
        public static int Index(string name) {
            return cache.Index(name);
        }

        public static void Save() {
            //ComponentsMapCache.Save(cache);
        }

        internal static unsafe void CreatePools(ref UnsafeList<GenericPool> pools, int size, Allocator allocator, ref int poolsCount)
        {
            foreach (var kvPair in ComponentTypes.Data)
            {
                var type = kvPair.Value;
                ref var pool = ref pools.Ptr[type.index];
                // pool = GenericPool.Create(type, size, allocator);
                if (!type.isArray)
                {
                    //Debug.Log($"Pool {GetType(type.index)} created");
                    pool = GenericPool.Create(type, size, allocator);
                    poolsCount += 1;
                }
                else
                {
                    //var realType = GetType(type.index);
                    //Debug.Log($"Pool {realType} created");
                    pool = GenericPool.Create(type, size, allocator);
                    var elementType = ComponentType.ElementTypes[type.index];
                    ref var elementsPool = ref pools.Ptr[elementType.index + 1];
                    //Debug.Log($"Pool for elements {realType.GetGenericArguments()[0]} created");
                    elementsPool = GenericPool.Create(elementType, size * ComponentArray.DefaultMaxCapacity, allocator);
                    poolsCount += 2;
                }
                Component.LogComponent(kvPair.Value);
            }
        }
        internal static unsafe void Dispose() {
            foreach (var kvPair in ComponentTypes.Data) {
                UnsafeUtility.Free(kvPair.Value.defaultValue, Allocator.Persistent);
            }
            ComponentTypes.Data.Dispose();
            ComponentTypesByTypes.Clear();
            ComponentType.ElementTypes.Dispose();
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
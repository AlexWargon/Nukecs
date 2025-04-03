using System.Linq;
using System.Runtime.InteropServices;
using Wargon.Nukecs.Collections;

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
    
    [Serializable][StructLayout(LayoutKind.Sequential)]
    public struct ComponentType
    {
        public int size;
        public int index;
        public int align;
        public bool isTag;
        public bool isDisposable;
        public bool isCopyable;
        public bool isArray;
        
        public unsafe void* defaultValue;
        internal IntPtr disposeFn;
        internal IntPtr copyFn;
        internal static readonly SharedStatic<NativeHashMap<int, ComponentType>> elementTypes = SharedStatic<NativeHashMap<int, ComponentType>>.GetOrCreate<ComponentType>();

        public static ref NativeHashMap<int, ComponentType> ElementTypes
        {
            get
            {
                if (!elementTypes.Data.IsCreated)
                {
                    elementTypes.Data = new NativeHashMap<int, ComponentType>(64, Allocator.Persistent);
                }

                return ref elementTypes.Data;
            }
        }
        
        public Type ManagedType => ComponentTypeMap.GetType(index);
        public FunctionPointer<DisposeDelegate> DisposeFn()
        {
            if (disposeFn == IntPtr.Zero)
            {
                throw new NullReferenceException($"copyFn is null for type {ManagedType.Name}");
            }
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

        internal static void Init()
        {
            elementTypes.Data = new NativeHashMap<int, ComponentType>(32, Allocator.Persistent);
        }

        internal static void AddElementType(ComponentType componentType, int index)
        {
            ElementTypes[index] = componentType;
        }
        [BurstDiscard]
        public override string ToString() {
            return
                $"ComponentType: {ComponentTypeMap.GetType(index)}  Index = {index}, size = {size}, Tag={isTag}, Disposable={isDisposable}, Copyable={isCopyable}, IsArray={isArray}";
        }

        public string LogString()
        {
            return $"ComponentType: {ComponentTypeMap.GetType(index)}  Index = {index}, size = {size}, Tag?[{isTag}], Disposable?[{isDisposable}], Copyable?[{isCopyable}], IsArray?[{isArray}]";
        }

        public static implicit operator Type(ComponentType componentType)
        {
            return ComponentTypeMap.GetType(componentType.index);
        }

        public static explicit operator ComponentType(Type type)
        {
            return ComponentTypeMap.GetComponentType(type);
        }

        public static long GetSizeOfAllComponents(int poolSize = 1)
        {
            long size = 0;
            var sizeOfGenericPool = UnsafeUtility.SizeOf<GenericPool.GenericPoolUnsafe>();
            foreach (var kvPair in ComponentTypeMap.ComponentTypes.Data)
            {
                size += kvPair.Value.size * poolSize + sizeOfGenericPool;
            }
            foreach (var elementType in ElementTypes)
            {
                size += elementType.Value.size * ComponentArray.DEFAULT_MAX_CAPACITY * poolSize + sizeOfGenericPool*2;
            }
            return size;
        }
    }

    public struct ComponentType<T> where T : unmanaged {
        private static readonly SharedStatic<ComponentType> ID = SharedStatic<ComponentType>.GetOrCreate<ComponentType<T>>();

        public static unsafe int Index {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (*(ComponentType*) ID.UnsafeDataPointer).index;
        }

        internal static unsafe ref ComponentType Data {
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

    internal class TypeToComponentType {
        internal static Dictionary<Type, ComponentType> Map = new();
    }
    internal struct ComponentTypeMap {
        private static ComponentsMapCache cache;
        internal static readonly SharedStatic<NativeHashMap<int, ComponentType>> ComponentTypes;
        
        private static bool _initialized = false;
        public static List<int> TypesIndexes => cache.TypesIndexes;
        static ComponentTypeMap() {
            ComponentTypes = SharedStatic<NativeHashMap<int, ComponentType>>.GetOrCreate<ComponentTypeMap>();
        }

        internal static void Init() {
            if(_initialized) return;
            cache = new ComponentsMapCache();
            ComponentTypes.Data = new NativeHashMap<int, ComponentType>(ComponentAmount.Value.Data + 1, Allocator.Persistent);
            
            _initialized = true;
        }

        internal static void InitializeArrayElementTypeReflection(Type typeElement, int index)
        {
            var addElement = typeof(ComponentTypeMap).GetMethod(nameof(InitializeElementType));
            var addElementMethod = addElement.MakeGenericMethod(typeElement);
            addElementMethod.Invoke(null, new object[] { index });
        }

        internal static void InitializeComponentTypeReflection(Type type, int index)
        {
            var method = typeof(ComponentTypeMap).GetMethod(nameof(InitializeComponentType));
            var genericMethod = method.MakeGenericMethod(type);
            genericMethod.Invoke(null, new object[] { index });
        }
        
        public static void InitializeComponentType<T>(int index) where T : unmanaged
        {
            Add(typeof(T), index);
            _ = AddComponentType<T>(index);
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
            AddComponentType<T>(index);
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


            data.defaultValue = UnsafeUtility.MallocTracked(data.size, data.align, Allocator.Persistent , 0);
            *(T*) data.defaultValue = default;
            ComponentTypes.Data.TryAdd(index, data);
            TypeToComponentType.Map.TryAdd(typeof(T), data);
            return data;
        }
        
        public static ComponentType GetComponentType(int index) => ComponentTypes.Data[index];

        public static ComponentType GetComponentType(int index, bool isArrayElement = false)
        {
            if (isArrayElement) return ComponentType.ElementTypes[index - 1];
            return ComponentTypes.Data[index];
        }
        
        public static ComponentType GetComponentType<T>() => TypeToComponentType.Map[typeof(T)];
        
        public static void SetComponentType<T>(ComponentType componentType) where T : unmanaged
        {
            TypeToComponentType.Map[typeof(T)] = componentType;
            ComponentTypes.Data[ComponentType<T>.Index] =  componentType;
            ComponentType<T>.Data = componentType;
        }
        
        public static ComponentType GetComponentType(Type type) => TypeToComponentType.Map[type];
        
        internal static void Add(Type type, int index) {
            cache.Add(type, index);
        }
        
        [BurstDiscard]
        public static Type GetType(int index) => cache.GetType(index);
        
        public static int Index(Type type) => cache.Index(type);
        
        public static int Index(string name) {
            return cache.Index(name);
        }

        public static void Save() {
            //ComponentsMapCache.Save(cache);
        }

        internal static unsafe void CreatePools(ref MemoryList<GenericPool> pools, int size, World.WorldUnsafe* world, ref int poolsCount)
        {
            foreach (var kvPair in ComponentTypes.Data)
            {
                var type = kvPair.Value;
                ref var pool = ref pools.Ptr[type.index];
                if (!type.isArray)
                {
                    pool = GenericPool.Create(type, size, world);
                    poolsCount += 1;
                }
                else
                {
                    pool = GenericPool.Create(type, size, world);
                    var elementType = ComponentType.ElementTypes[type.index];
                    ref var elementsPool = ref pools.Ptr[elementType.index + 1];
                    elementsPool = GenericPool.Create(elementType, size * ComponentArray.DEFAULT_MAX_CAPACITY, world);
                    poolsCount += 2;
                }
                //Component.LogComponent(kvPair.Value);
            }
        }
        internal static unsafe void Dispose() {
            if(!_initialized) return;
            foreach (var kvPair in ComponentTypes.Data) {
                if(kvPair.Value.defaultValue != null)
                    UnsafeUtility.FreeTracked(kvPair.Value.defaultValue, Allocator.Persistent);
            }
            ComponentTypes.Data.Dispose();
            TypeToComponentType.Map.Clear();
            ComponentType.ElementTypes.Dispose();
        }
    }

    [Serializable]
    public class ComponentsMapCache {
        private readonly Dictionary<int, Type> _typeByIndex = new();
        private readonly Dictionary<Type, int> _indexByType = new();
        private readonly Dictionary<string, Type> _nameToType = new();
        public readonly System.Collections.Generic.List<int> TypesIndexes = new();

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
            {
                // File does not exist
                Debug.LogError("Save file not found in " + filePath);
                saveData = new ComponentsMapCache();
                Save(saveData);
                return saveData;
            }
        }
    }
}
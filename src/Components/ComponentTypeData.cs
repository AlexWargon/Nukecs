using System.Runtime.InteropServices;

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
    public struct ComponentTypeData
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
        internal static readonly SharedStatic<NativeHashMap<int, ComponentTypeData>> elementTypes = SharedStatic<NativeHashMap<int, ComponentTypeData>>.GetOrCreate<ComponentTypeData>();

        public static ref NativeHashMap<int, ComponentTypeData> ElementTypes
        {
            get
            {
                if (!elementTypes.Data.IsCreated)
                {
                    elementTypes.Data = new NativeHashMap<int, ComponentTypeData>(64, Allocator.Persistent);
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
            elementTypes.Data = new NativeHashMap<int, ComponentTypeData>(32, Allocator.Persistent);
        }

        internal static void AddElementType(ComponentTypeData componentTypeData, int index)
        {
            ElementTypes[index] = componentTypeData;
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

        public static implicit operator Type(ComponentTypeData componentTypeData)
        {
            return ComponentTypeMap.GetType(componentTypeData.index);
        }

        public static explicit operator ComponentTypeData(Type type)
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
        private static readonly SharedStatic<ComponentTypeData> ID = SharedStatic<ComponentTypeData>.GetOrCreate<ComponentType<T>>();

        public static unsafe int Index {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (*(ComponentTypeData*) ID.UnsafeDataPointer).index;
        }

        internal static unsafe ref ComponentTypeData Data {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref UnsafeUtility.AsRef<ComponentTypeData>(ID.UnsafeDataPointer);
        }
        
        static ComponentType() {
            Init();
        }
        [BurstDiscard]
        private static void Init() {
            ID.Data = ComponentTypeMap.GetComponentType<T>();
        }
    }

    internal static class TypeToComponentType {
        internal static readonly Dictionary<Type, ComponentTypeData> Map = new();
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

        public int Index(Type type)
        {
            return _indexByType.GetValueOrDefault(type, -1);
        }

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
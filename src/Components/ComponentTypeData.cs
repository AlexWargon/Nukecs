using System.Runtime.InteropServices;
using UnityEngine.Serialization;

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

    public enum StorageType : byte {
        Archetype = 0,
        Pool = 1
    }

    /// <summary>
    /// Storage category of a component type. Determines which archetype mask holds its bit:
    /// Inline — data components stored in the archetype data buffer;
    /// Tag — zero-sized components, filter-only, no bytes in the data buffer;
    /// Pool — components stored in a per-entity GenericPool outside the archetype data buffer.
    /// </summary>
    public enum ComponentCategory : byte {
        Inline = 0,
        Tag = 1,
        Pool = 2
    }

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
        public bool IsArrayElement;
        public StorageType storageType;
        public ComponentCategory category;
        [NativeDisableUnsafePtrRestriction]
        internal IntPtr disposeFn;
        [NativeDisableUnsafePtrRestriction]
        internal IntPtr copyFn;
        
        public Type ManagedType => ComponentTypeMap.GetType(index);
        [MethodImpl(256)]
        public FunctionPointer<DisposeDelegate> DisposeFn()
        {
            if (disposeFn == IntPtr.Zero)
            {
                var fromMap = ComponentTypeMap.GetComponentType(index);
                disposeFn = fromMap.disposeFn;
            }
            return new FunctionPointer<DisposeDelegate>(disposeFn);
        }
        [MethodImpl(256)]
        public FunctionPointer<CopyDelegate> CopyFn()
        {
            if (copyFn == IntPtr.Zero)
            {
                var fromMap = ComponentTypeMap.GetComponentType(index);
                copyFn = fromMap.copyFn;
            }
            return new FunctionPointer<CopyDelegate>(copyFn);
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
    }

    internal static class TypeToComponentType {
        internal static Dictionary<Type, ComponentTypeData> Map = new();
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
            _nameToType[type.Name] = type;
        }

        public Type GetType(int index) => _typeByIndex[index];
        public int Index(Type type) => _indexByType[type];
        public int Index(string name) => _indexByType[_nameToType[name]];
        public bool HasIndex(Type type) => _indexByType.ContainsKey(type);
        public bool TryGetIndex(Type type, out int index)
        {
            index = -1;
            if (_indexByType.TryGetValue(type, out index))
            {
                return true;
            }
            return false;
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
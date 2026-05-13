using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Wargon.Nukecs
{
    public struct ComponentTypeMap {
        private static int nextIndex;
        private static ComponentsMapCache cache;
        internal static readonly SharedStatic<NativeHashMap<int, ComponentTypeData>> ComponentTypes
            = SharedStatic<NativeHashMap<int, ComponentTypeData>>.GetOrCreate<ComponentTypeMap>();
        
        private static bool _initialized;
        public static List<int> TypesIndexes => cache.TypesIndexes;
        private static void EnsureInitialized() {
            if (_initialized) return;
            cache = new ComponentsMapCache();
            ComponentTypes.Data = new NativeHashMap<int, ComponentTypeData>(256, Allocator.Persistent);
            ComponentTypeData.Init();
            try {
                Generated.GeneratedDisposeRegistryStatic.EnsureGenericMethodInstantiation();
            }
            catch
            {
                // ignored
            }

            Application.quitting += Dispose;
            _initialized = true;
        }

        internal static void Dispose()
        {
            if (_initialized)
            {
                ComponentTypes.Data.Dispose();
                ComponentTypeData.ElementTypes.Dispose();
                Application.quitting -= Dispose;
                _initialized = false;
            }
        }
        [BurstDiscard]
        internal static void RegisterByReflection(Type type)
        {
            try
            {
                var regType = typeof(ComponentTypeMap);
                regType.GetMethod(nameof(RegisterIfNeeded),
                        BindingFlags.NonPublic | BindingFlags.Static)
                    ?.MakeGenericMethod(type).Invoke(null, null);
            }
            catch
            {
                dbug.log($"Failed to register component type {type.Name}", Color.red);
            }
            finally
            {
                
            }
        }

#region MAIN REGISTERS
        [BurstDiscard]
        internal static ComponentTypeData RegisterIfNeeded<T>() where T : unmanaged {
            EnsureInitialized();
            var type = typeof(T);
            if (TypeToComponentType.Map.TryGetValue(type, out var existing)) {
                if (!cache.HasIndex(type))
                    Add(type, existing.index);
                return existing;
            }
            
            var index = nextIndex++;
            var data = AddComponentType<T>(index);
            Add(type, index);
            ComponentHelpers.EnsureWriter<T>(index);
            RegisterDisposeCopy(type);
            //dbug.log($"REGISTER COMPONENT {type.Name}. Index:{index}", Color.cyan);
            if (data.isArray) {
                var elementType = typeof(T).GetGenericArguments()[0];
                if (typeof(IArrayComponent).IsAssignableFrom(elementType)) {
                    nextIndex++;
                    InitializeArrayElementTypeReflection(elementType, index+1);
                    //dbug.log($"REGISTER ELEMENT {elementType.Name}. Index:{index+1}", Color.green);
                }
            }

            ComponentType<T>.Data = data;
            ComponentAmount.Value.Data = nextIndex;
            return TypeToComponentType.Map[type];
        }

        private static void RegisterDisposeCopy(Type type) {
            if (typeof(IDisposable).IsAssignableFrom(type)) {
                var regType = typeof(DisposeRegistryStatic<>).MakeGenericType(type);
                regType.GetMethod("Register")?.Invoke(null, null);
            }
            foreach (var iface in type.GetInterfaces()) {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(ICopyable<>)) {
                    var regType = typeof(CopyRegistryStatic<>).MakeGenericType(type);
                    regType.GetMethod("Register")?.Invoke(null, null);
                    break;
                }
            }
        }
#endregion
        public static void ReRegisterFunctionPointers() {
            var types = new System.Collections.Generic.List<Type>(TypeToComponentType.Map.Keys);
            foreach (var type in types) {
                RegisterDisposeCopy(type);
            }
        }

        internal static void InitializeArrayElementTypeReflection(Type typeElement, int index)
        {
            var addElement = typeof(ComponentTypeMap).GetMethod(nameof(InitializeElementType));
            var addElementMethod = addElement.MakeGenericMethod(typeElement);
            addElementMethod.Invoke(null, new object[] { index });
        }
        
        public static void InitializeComponentType<T>(int index) where T : unmanaged
        {
            RegisterIfNeeded<T>();
        }

        public static unsafe void InitializeElementType<T>(int index) where T : unmanaged, IArrayComponent
        {
            var size = sizeof(T);
            var data = new ComponentTypeData
            {
                align = UnsafeUtility.AlignOf<T>(),
                size = size,
                index = index,
                isTag = false,
                isDisposable = false,
                isCopyable = false,
                isArray = false,
                IsArrayElement = true
            };
            ComponentTypeData.AddElementType(data, index);
            AddComponentType<T>(index);
        }
        
        internal static ComponentTypeData AddComponentType<T>(int index) where T : unmanaged
        {
            if (ComponentTypes.Data.ContainsKey(index)) return ComponentTypes.Data[index];
            var size = UnsafeUtility.SizeOf<T>();
            var data = new ComponentTypeData
            {
                align = UnsafeUtility.AlignOf<T>(),
                size = size,
                index = index,
                isTag = size == 1,
                isDisposable = typeof(IDisposable).IsAssignableFrom(typeof(T)),
                isCopyable = typeof(T).GetInterfaces()
                    .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICopyable<>)),
                isArray = typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(ComponentArray<>),
                IsArrayElement = typeof(T).GetInterfaces().Any(i => i == typeof(IArrayComponent)),
                storageType = typeof(IPoolComponent).IsAssignableFrom(typeof(T))
                    ? StorageType.Pool
                    : StorageType.Archetype
            };
            ComponentTypes.Data.TryAdd(index, data);
            TypeToComponentType.Map.TryAdd(typeof(T), data);
            cache.Add(typeof(T), index);
            return data;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComponentTypeData GetComponentType(int index) => ComponentTypes.Data[index];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ComponentTypeData GetComponentType(int index, bool isArrayElement = false)
        {
            if (isArrayElement) return ComponentTypeData.ElementTypes[index];
            return ComponentTypes.Data[index];
        }
        
        public static ComponentTypeData GetComponentType<T>() => TypeToComponentType.Map[typeof(T)];
        
        public static void SetComponentType<T>(ComponentTypeData componentTypeData) where T : unmanaged
        {
            TypeToComponentType.Map[typeof(T)] = componentTypeData;
            ComponentTypes.Data[ComponentType<T>.Index] =  componentTypeData;
            ComponentType<T>.Data = componentTypeData;
        }
        
        public static ComponentTypeData GetComponentType(Type type)
        {
            if (!cache.HasIndex(type))
            {
                RegisterByReflection(type);
            }
            return TypeToComponentType.Map[type];
        }

        internal static void Add(Type type, int index) {
            cache.Add(type, index);
        }
        public static Type GetType(int index) => cache.GetType(index);
        public static int Index(string name) => cache.Index(name);
        public static int Index(Type type)
        {
            if (!cache.HasIndex(type))
            {
                RegisterByReflection(type);
            }

            return cache.Index(type);
        }
    }

    public unsafe partial struct ComponentsMetaDataStatic
    {
        private static readonly SharedStatic<ComponentsMetaData> metaData =
            SharedStatic<ComponentsMetaData>.GetOrCreate<ComponentsMetaDataStatic>();

        public static void Initialize()
        {
            var data = new ComponentsMetaData(256);
            // data.SetAll(new ComponentTypeData[1]
            // {
            //
            // });
            AppDomain.CurrentDomain.ProcessExit += Dtor;
        }
        static void Dtor(object sender, EventArgs e) {
            metaData.Data.Dispose();
        }
    }
    public readonly unsafe struct ComponentsMetaData
    {
        private readonly ComponentTypeData* _data;
        private readonly int _capacity;
        public ComponentsMetaData(int capacity)
        {
            _data = (ComponentTypeData*)Marshal.AllocHGlobal(capacity * sizeof(ComponentTypeData));
            _capacity = capacity;
        }
        public ref ComponentTypeData Get(int index) => ref _data[index];

        public void SetAll(ComponentTypeData[] data)
        {
            if (_capacity != data.Length)
            {
                throw new Exception("Capacity mismatch in component metadata");
                return;
            }

            fixed (ComponentTypeData* ptr = data)
            {
                UnsafeUtility.MemCpy(_data, ptr, data.Length * sizeof(ComponentTypeData));
            }
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal((IntPtr)_data);
        }
    }
}

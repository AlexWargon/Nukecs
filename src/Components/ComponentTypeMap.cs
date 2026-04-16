using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs
{
    public struct ComponentTypeMap {
        private static int nextIndex;
        private static ComponentsMapCache cache;
        internal static readonly SharedStatic<NativeHashMap<int, ComponentTypeData>> ComponentTypes;
        
        private static bool _initialized = false;
        public static List<int> TypesIndexes => cache.TypesIndexes;
        static ComponentTypeMap() {
            ComponentTypes = SharedStatic<NativeHashMap<int, ComponentTypeData>>.GetOrCreate<ComponentTypeMap>();
        }

        private static void EnsureInitialized() {
            if (_initialized) return;
            cache = new ComponentsMapCache();
            ComponentTypes.Data = new NativeHashMap<int, ComponentTypeData>(256, Allocator.Persistent);
            ComponentTypeData.Init();
            try {
                Generated.GeneratedDisposeRegistryStatic.EnsureGenericMethodInstantiation();
            } catch {}
            _initialized = true;
        }
        [BurstDiscard]
        internal static void RegisterByReflection(Type type)
        {
            var regType = typeof(ComponentTypeMap);
            regType.GetMethod(nameof(RegisterIfNeeded), 
                BindingFlags.NonPublic | BindingFlags.Static)
                ?.MakeGenericMethod(type).Invoke(null, null);
        }
        [BurstDiscard]
        internal static ComponentTypeData RegisterIfNeeded<T>() where T : unmanaged {
            EnsureInitialized();
            var type = typeof(T);
            if (TypeToComponentType.Map.TryGetValue(type, out var existing))
                return existing;
            
            var index = nextIndex++;
            var data = AddComponentType<T>(index);
            Add(type, index);
            ComponentHelpers.EnsureWriter<T>(index);
            RegisterDisposeCopy(type);

            if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(ComponentArray<>)) {
                var elementType = typeof(T).GetGenericArguments()[0];
                if (typeof(IArrayComponent).IsAssignableFrom(elementType)) {
                    InitializeArrayElementTypeReflection(elementType, index);
                }
            }
            
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
        
        internal static unsafe ComponentTypeData AddComponentType<T>(int index) where T : unmanaged
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
            };

            data.defaultValue = UnsafeUtility.MallocTracked(data.size, data.align, Allocator.Persistent , 0);
            UnsafeUtility.MemClear(data.defaultValue, data.size);
            ComponentTypes.Data.TryAdd(index, data);
            TypeToComponentType.Map.TryAdd(typeof(T), data);
            return data;
        }
        
        public static ComponentTypeData GetComponentType(int index) => ComponentTypes.Data[index];

        public static ComponentTypeData GetComponentType(int index, bool isArrayElement = false)
        {
            if (isArrayElement) return ComponentTypeData.ElementTypes[index - 1];
            return ComponentTypes.Data[index];
        }
        
        public static ComponentTypeData GetComponentType<T>() => TypeToComponentType.Map[typeof(T)];
        
        public static void SetComponentType<T>(ComponentTypeData componentTypeData) where T : unmanaged
        {
            TypeToComponentType.Map[typeof(T)] = componentTypeData;
            ComponentTypes.Data[ComponentType<T>.Index] =  componentTypeData;
            ComponentType<T>.Data = componentTypeData;
        }
        
        public static ComponentTypeData GetComponentType(Type type) => TypeToComponentType.Map[type];
        
        internal static void Add(Type type, int index) {
            cache.Add(type, index);
        }
        

        public static Type GetType(int index) => cache.GetType(index);

        public static int Index(Type type)
        {
            if (!cache.HasIndex(type))
            {
                RegisterByReflection(type);
                dbug.log($"{type.Name} ADDED BY REFLECTION");
            }

            return cache.Index(type);
        }
        
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
                    pool = GenericPool.Create(type, size, ref world->selfPtr);
                    poolsCount += 1;
                }
                else
                {
                    pool = GenericPool.Create(type, size, ref world->selfPtr);
                    var elementType = ComponentTypeData.ElementTypes[type.index];
                    ref var elementsPool = ref pools.Ptr[elementType.index + 1];
                    elementsPool = GenericPool.Create(elementType, size * ComponentArray.DEFAULT_MAX_CAPACITY, ref world->selfPtr);
                    poolsCount += 2;
                }
            }
        }

        internal static int RegisteredCount => nextIndex;
    }
}

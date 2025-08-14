using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs
{
    public struct ComponentTypeMap {
        private static ComponentsMapCache cache;
        internal static readonly SharedStatic<NativeHashMap<int, ComponentTypeData>> ComponentTypes;
        
        private static bool _initialized = false;
        public static List<int> TypesIndexes => cache.TypesIndexes;
        static ComponentTypeMap() {
            ComponentTypes = SharedStatic<NativeHashMap<int, ComponentTypeData>>.GetOrCreate<ComponentTypeMap>();
        }

        internal static void Init() {
            if(_initialized) return;
            cache = new ComponentsMapCache();
            ComponentTypes.Data = new NativeHashMap<int, ComponentTypeData>(ComponentAmount.Value.Data + 1, Allocator.Persistent);
            
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
            var data = new ComponentTypeData
            {
                align = UnsafeUtility.AlignOf<T>(),
                size = size,
                index = index,
                isTag = false,
                isDisposable = false,
                isCopyable = false,
                isArray = false
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
            };


            data.defaultValue = UnsafeUtility.MallocTracked(data.size, data.align, Allocator.Persistent , 0);
            *(T*) data.defaultValue = default;
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
                    var elementType = ComponentTypeData.ElementTypes[type.index];
                    ref var elementsPool = ref pools.Ptr[elementType.index + 1];
                    elementsPool = GenericPool.Create(elementType, size * ComponentArray.DEFAULT_MAX_CAPACITY, world);
                    poolsCount += 2;
                }
                //Component.LogComponent(kvPair.Value);
            }
        }
        internal static unsafe void Dispose() {
            if(!_initialized) return;
            _initialized = false;
            foreach (var kvPair in ComponentTypes.Data) {
                if(kvPair.Value.defaultValue != null)
                    UnsafeUtility.FreeTracked(kvPair.Value.defaultValue, Allocator.Persistent);
            }
            ComponentTypes.Data.Dispose();
            TypeToComponentType.Map.Clear();
            ComponentTypeData.ElementTypes.Dispose();
            Component._initialized = false;
        }
    }
}
using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using UnityEngine;
using Wargon.Nukecs.Collections;
using Wargon.Nukecs.Tests;
// ReSharper disable InconsistentNaming

namespace Wargon.Nukecs
{
    public struct ALLOCATOR
    {
        public static ref MemAllocator DOMAIN
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref World.domainAllocator.Data;
        }

        public static readonly PER_WORLD_ALLOCATORS PER_WORLD = default;
        public struct PER_WORLD_ALLOCATORS
        {
            public ref MemAllocator this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref World.worlds.Data.ElementAt(index).AllocatorRef;
            }
        }
    }
    public unsafe partial struct World
    {
        private struct KeyDomainAllocator {}
        private struct KeyWorldsList {}
        private struct DummyWorld { }
        
        private static readonly SharedStatic<World> dummyWorld = SharedStatic<World>.GetOrCreate<DummyWorld>();
        internal static readonly SharedStatic<MemAllocator> domainAllocator = SharedStatic<MemAllocator>.GetOrCreate<KeyDomainAllocator>();
        internal static readonly SharedStatic<MemoryList<World>> worlds = SharedStatic<MemoryList<World>>.GetOrCreate<KeyWorldsList>();
        private static byte lastFreeSlot;
        private static int worldCount;
        private static int lastWorldID;
        private static bool staticInited;
        public const int MAX_WORLD_COUNT = 8;
        internal static void InitStatic()
        {
            if(staticInited) return;
            domainAllocator.Data = new MemAllocator(sizeof(MemoryList<World>) + sizeof(World) * MAX_WORLD_COUNT + Memory.MEGABYTE);
            worlds.Data = new MemoryList<World>(MAX_WORLD_COUNT, ref domainAllocator.Data, true);
            worldCount = 0;
            dummyWorld.Data = default;
            dummyWorld.Data.unsafeWorldPtr = ptr<WorldUnsafe>.NULL;
            Component.Initialization();

            staticInited = true;
        }
        public static int WorldCapacity => worlds.Data.Capacity;
        public static ref World Get(int index)
        {
            if(worlds.Data.IsCreated)
                return ref worlds.Data.ElementAt(index);
            return ref dummyWorld.Data;
        }

        public static bool TryGet(int worldID, out World world)
        {
            var w = worlds.Data.ElementAt(worldID);
            world = w;
            return w.unsafeWorldPtr.cached != null;
        }
        public static bool HasActiveWorlds()
        {
            for (var i = 0; i < worlds.Data.Length; i++)
            {
                if (worlds.Data[i].IsAlive) return true;
            }

            return false;
        }

        internal static World* GetPtr(int index)
        {
            return worlds.Data.ElementAtPtr(index);
        }

        public static ref World Default
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ref var w = ref Get(0);
                if (!w.IsAlive)
                {
                    w = Create();
                    Debug.Log("Created Default World");
                }

                return ref w;
            }
        }

        private static event Action OnWorldCreatingEvent;
        private static event Action OnDisposeStaticEvent;

        public static void OnWorldCreating(Action action)
        {
            OnWorldCreatingEvent += action;
        }

        public static void OnDisposeStatic(Action action)
        {
            OnDisposeStaticEvent += action;
        }

        public static World Create()
        {
            InitStatic();
            OnWorldCreatingEvent?.Invoke();
            World world;
            var id = lastFreeSlot++;
            lastWorldID = id;
            world.unsafeWorldPtr = WorldUnsafe.CreatePtr(id, WorldConfig.Default16384);
            worlds.Data[id] = world;
            world.UnsafeWorldRef.ManagedWorld = domainAllocator.Data.AllocatePtr<World>();
            world.UnsafeWorldRef.ManagedWorld.Ref = worlds.Data[id];
            worldCount++;
            return world;
        }
        public static World Create(WorldConfig config)
        {
            InitStatic();
            OnWorldCreatingEvent?.Invoke();
            World world;
            var id = lastFreeSlot++;
            lastWorldID = id;
            world.unsafeWorldPtr = WorldUnsafe.CreatePtr(id, config);
            worlds.Data[id] = world;
            world.UnsafeWorldRef.ManagedWorld = domainAllocator.Data.AllocatePtr<World>();
            world.UnsafeWorldRef.ManagedWorld.Ref = worlds.Data[id];
            Debug.Log($"[☢️NUKECS] Created World {id}");
            worldCount++;
            return world;
        }
        public static World Load(WorldConfig config, byte[] data)
        {
            InitStatic();
            OnWorldCreatingEvent?.Invoke();
            World world;
            var id = lastFreeSlot++;
            lastWorldID = id;
            world.unsafeWorldPtr = WorldUnsafe.CreatePtr(id, config);
            worlds.Data[id] = world;
            world.UnsafeWorldRef.ManagedWorld = domainAllocator.Data.AllocatePtr<World>();
            world.UnsafeWorldRef.ManagedWorld.Ref = worlds.Data[id];
            //Debug.Log($"Created World {id}");
            worldCount++;
            return world;
        }
        public static void DisposeStatic()
        {
            StaticObjectRefStorage.Clear();
            OnDisposeStaticEvent?.Invoke();
            OnDisposeStaticEvent = null;
            OnWorldCreatingEvent = null;
            staticInited = false;
            lastFreeSlot = 0;
            lastWorldID = 0;
            worldCount = 0;
            SingletonRegistry.ResetAll();
            if (domainAllocator.Data.IsActive)
                domainAllocator.Data.Dispose();
            EntityPrefabMap.Dispose();
            ComponentTypeMap.Dispose();
            Component._initialized = false;
        }

        internal static void FixManagedWorld(int id) {
            ref var world = ref Get(id);
            world.UnsafeWorld->ManagedWorld.OnDeserialize(ref domainAllocator.Data);
            world.UnsafeWorld->ManagedWorld.Ref = world;
        }
    }
}
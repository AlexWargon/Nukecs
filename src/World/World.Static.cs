using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using UnityEngine;
using Wargon.Nukecs.Collections;
using Wargon.Nukecs.Tests;

namespace Wargon.Nukecs
{
    public unsafe partial struct World
    {
        private struct KeyDomainAllocator {}
        private struct KeyWorldsList {}
        private struct DummyWorld { }
        private static readonly SharedStatic<World> dummyWorld = SharedStatic<World>.GetOrCreate<DummyWorld>();
        private static readonly SharedStatic<MemAllocator> domainAllocator = SharedStatic<MemAllocator>.GetOrCreate<KeyDomainAllocator>();
        private static readonly SharedStatic<MemoryList<World>> worlds = SharedStatic<MemoryList<World>>.GetOrCreate<KeyWorldsList>();
        private static byte lastFreeSlot;
        private static int worldCount;
        private static int lastWorldID;
        private static bool staticInited;
        internal static void InitStatic()
        {
            if(staticInited) return;
            domainAllocator.Data = new MemAllocator(sizeof(MemoryList<World>) + sizeof(World) * 4);
            worlds.Data = new MemoryList<World>(4, ref domainAllocator.Data, true);
            worldCount = 0;
            staticInited = true;
        }
        
        public static ref World Get(int index)
        {
            if(worlds.UnsafeDataPointer != null)
                return ref worlds.Data.ElementAt(index);
            return ref dummyWorld.Data;
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
            Debug.Log($"Created World {id}");
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
            Debug.Log($"Created World {id}");
            worldCount++;
            return world;
        }
        public static void DisposeStatic()
        {
            if(!staticInited) return;
            domainAllocator.Data.Dispose();
            StaticObjectRefStorage.Clear();
            OnDisposeStaticEvent?.Invoke();
            OnDisposeStaticEvent = null;
            OnWorldCreatingEvent = null;
            staticInited = false;
            lastFreeSlot = 0;
            lastWorldID = 0;
            worldCount = 0;
            SingletonRegistry.ResetAll();
            EntityPrefabMap.Dispose();
            //dbug.log(nameof(DisposeStatic), Color.green);
        }
    }
}
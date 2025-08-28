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
        private static World dummy;
        private static MemAllocator* allocator;
        private static readonly SharedStatic<MemoryList<World>> worlds = SharedStatic<MemoryList<World>>.GetOrCreate<World>();
        private static byte lastFreeSlot;
        private static int lastWorldID;
        private static bool staticInited;
        internal static void InitStatic()
        {
            if(staticInited) return;
            allocator = MemAllocator.New(sizeof(MemoryList<World>) + sizeof(World) * 4);
            worlds.Data = new MemoryList<World>(4, ref *allocator, true);
            staticInited = true;
        }

        public static ref World Get(int index)
        {
            if (allocator != null)
            {
                return ref worlds.Data.ElementAt(index);
            }
            return ref dummy;
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
            Component.Initialization();
            World world;
            var id = lastFreeSlot++;
            lastWorldID = id;
            world.UnsafeWorldPtr = WorldUnsafe.CreatePtr(id, WorldConfig.Default16384);
            worlds.Data[id] = world;

            return world;
        }

        public static World Create(WorldConfig config)
        {
            InitStatic();
            OnWorldCreatingEvent?.Invoke();
            Component.Initialization();
            World world;
            var id = lastFreeSlot++;
            lastWorldID = id;
            world.UnsafeWorldPtr = WorldUnsafe.CreatePtr(id, config);
            worlds.Data[id] = world;
            Debug.Log($"Created World {id}");
            return world;
        }

        public static void DisposeStatic()
        {
            MemAllocator.Destroy(allocator);
            ComponentTypeMap.Dispose();
            StaticObjectRefStorage.Clear();
            //ComponentTypeMap.Save();
            OnDisposeStaticEvent?.Invoke();
            OnDisposeStaticEvent = null;
            OnWorldCreatingEvent = null;
            staticInited = false;
            SingletonRegistry.ResetAll();
            dbug.log(nameof(DisposeStatic), Color.green);
        }
    }
}
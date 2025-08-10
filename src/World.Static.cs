using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs
{
    public unsafe partial struct World
    {
        private static MemAllocator _allocator;
        private static SharedStatic<MemoryList<World>> _worlds = SharedStatic<MemoryList<World>>.GetOrCreate<World>();
        //private static readonly World[] worlds = new World[4];
        private static byte lastFreeSlot;
        private static int lastWorldID;

        static World()
        {
            _allocator = new MemAllocator(sizeof(MemoryList<World>) + sizeof(World) * 4);
            _worlds.Data = new MemoryList<World>(4, ref _allocator, true);
        }
        
        public static ref World Get(int index) => ref _worlds.Data.ElementAt(index);

        public static bool HasActiveWorlds()
        {
            for (var i = 0; i < _worlds.Data.Length; i++)
            {
                if (_worlds.Data[i].IsAlive) return true;
            }

            return false;
        }

        internal static World* GetPtr(int index)
        {
            return _worlds.Data.ElementAtPtr(index);
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
            OnWorldCreatingEvent?.Invoke();
            Component.Initialization();
            World world;
            var id = lastFreeSlot++;
            lastWorldID = id;
            world.UnsafeWorldPtr = WorldUnsafe.CreatePtr(id, WorldConfig.Default16384);
            _worlds.Data[id] = world;

            return world;
        }

        public static World Create(WorldConfig config)
        {
            OnWorldCreatingEvent?.Invoke();
            Component.Initialization();
            World world;
            var id = lastFreeSlot++;
            lastWorldID = id;
            world.UnsafeWorldPtr = WorldUnsafe.CreatePtr(id, config);
            _worlds.Data[id] = world;
            Debug.Log($"Created World {id}");
            return world;
        }

        public static void DisposeStatic()
        {
            _allocator.Dispose();
            ComponentTypeMap.Dispose();
            StaticObjectRefStorage.Clear();
            //ComponentTypeMap.Save();
            OnDisposeStaticEvent?.Invoke();
            OnDisposeStaticEvent = null;
            OnWorldCreatingEvent = null;
            dbug.log(nameof(DisposeStatic), Color.green);
        }
    }
}
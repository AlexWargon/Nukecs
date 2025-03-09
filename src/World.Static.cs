using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Wargon.Nukecs
{
    public unsafe partial struct World
    {
        private static readonly World[] worlds = new World[4];
        private static int lastFreeSlot;
        private static int lastWorldID;
        public static ref World Get(int index) => ref worlds[index];

        public static bool HasActiveWorlds()
        {
            for (var i = 0; i < worlds.Length; i++)
            {
                if (worlds[i].IsAlive) return true;
            }

            return false;
        }

        internal static World* GetPtr(int index)
        {
            fixed (World* world = worlds)
            {
                return world + index;
            }
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
            worlds[id] = world;

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
            worlds[id] = world;
            Debug.Log($"Created World {id}");
            return world;
        }

        public static void DisposeStatic()
        {
            ComponentTypeMap.Dispose();
            //ComponentTypeMap.Save();
            OnDisposeStaticEvent?.Invoke();
            OnDisposeStaticEvent = null;
            OnWorldCreatingEvent = null;
            //WorldSystems.Dispose();
        }
    }
}
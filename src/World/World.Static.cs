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
        private unsafe struct EntityWorldTokens { public fixed ushort Values[MAX_WORLD_COUNT]; }
        // Retained across DisposeStatic, so recreating a world slot cannot revive handles.
        private static readonly SharedStatic<EntityWorldTokens> entityWorldTokens = SharedStatic<EntityWorldTokens>.GetOrCreate<EntityWorldTokens>();

        internal static ushort AcquireEntityWorldToken(byte slot)
        {
            fixed (ushort* values = entityWorldTokens.Data.Values) {
                var next = values[slot] + MAX_WORLD_COUNT;
                if (next > ushort.MaxValue - (MAX_WORLD_COUNT - 1))
                    throw new InvalidOperationException("Entity world tokens exhausted for this slot.");
                values[slot] = (ushort)next;
                return (ushort)(next | slot);
            }
        }

        internal static void ObserveEntityWorldToken(ushort token)
        {
            var slot = token & (MAX_WORLD_COUNT - 1);
            var epoch = (ushort)(token & ~(MAX_WORLD_COUNT - 1));
            fixed (ushort* values = entityWorldTokens.Data.Values)
                if (values[slot] < epoch) values[slot] = epoch;
        }

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
            // positional `true` here is lenAsCapacity (NOT clear) — the slots must be zeroed
            // or slot-aliveness checks (IsAlive) read uninitialized arena garbage
            worlds.Data = new MemoryList<World>(MAX_WORLD_COUNT, ref domainAllocator.Data, true, clear: true);
            for (var i = 0; i < MAX_WORLD_COUNT; i++) worlds.Data[i] = default;
            worldCount = 0;
            dummyWorld.Data = default;
            dummyWorld.Data.unsafeWorldPtr = ptr<WorldUnsafe>.NULL;
            Component.Initialization();

            staticInited = true;
        }
        public static int WorldCapacity => worlds.Data.Capacity;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref World Get(int index)
        {
            if(worlds.Data.IsCreated)
                return ref worlds.Data.ElementAt(index);
            return ref dummyWorld.Data;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ref World GetInternal(int index)
        {
            return ref worlds.Data.ElementAt(index);
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

        /// <summary>
        /// Finds a genuinely free world slot (round-robin from lastFreeSlot). lastFreeSlot
        /// alone is not enough: after disposing world A while world B is alive, the next two
        /// Create calls would hand out A's slot and then overwrite live B. Throws a clean
        /// error once all <see cref="MAX_WORLD_COUNT"/> slots are occupied by live worlds.
        /// </summary>
        private static byte AcquireWorldSlot()
        {
            for (var i = 0; i < MAX_WORLD_COUNT; i++)
            {
                var id = (byte)((lastFreeSlot + i) % MAX_WORLD_COUNT);
                if (!worlds.Data[id].IsAlive)
                {
                    lastFreeSlot = (byte)((id + 1) % MAX_WORLD_COUNT);
                    return id;
                }
            }
            throw new InvalidOperationException(
                $"[Nukecs] Cannot create world: the limit of {MAX_WORLD_COUNT} simultaneous worlds is reached. Dispose a world first.");
        }

        public static World Create()
        {
            InitStatic();
            OnWorldCreatingEvent?.Invoke();
            World world;
            var id = AcquireWorldSlot();
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
            var id = AcquireWorldSlot();
            lastWorldID = id;
            world.unsafeWorldPtr = WorldUnsafe.CreatePtr(id, config);
            worlds.Data[id] = world;
            world.UnsafeWorldRef.ManagedWorld = domainAllocator.Data.AllocatePtr<World>();
            world.UnsafeWorldRef.ManagedWorld.Ref = worlds.Data[id];
            //Debug.Log($"☢️[NUKECS] Created World {id}");
            worldCount++;
            return world;
        }
        public static World Load(WorldConfig config, byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var world = Create(config);
            var temporaryId = world.Id;
            var targetId = temporaryId;
            var allocatorHandler = world.UnsafeWorld->AllocatorHandler;
            var allocator = allocatorHandler.AllocatorWrapper.Allocator;
            var managedWorld = world.UnsafeWorld->ManagedWorld;
            var ecb = world.ECB;
            try {
                allocator.FastDeserialize(data);
                allocatorHandler.AllocatorWrapper.Allocator = allocator;
                world.unsafeWorldPtr.OnDeserialize(ref allocator);
                targetId = world.UnsafeWorld->Id;
                if (targetId >= MAX_WORLD_COUNT)
                    throw new InvalidOperationException("The saved world slot is invalid.");
                if (targetId != temporaryId && Get(targetId).IsAlive)
                    throw new InvalidOperationException("The saved world slot is occupied. Dispose that world before loading.");
                // Domain allocations are not part of a world's save. Keep the newly owned
                // managed wrapper instead of interpreting a saved domain-allocator offset.
                world.UnsafeWorld->ManagedWorld = managedWorld;
                if (targetId != temporaryId) Get(temporaryId) = default;
                world.CompleteDeserialization(ref allocator, ref allocatorHandler, ecb, targetId);
                lastWorldID = (byte)targetId;
                return world;
            }
            catch {
                // Deserialization may have replaced the initial arena. Release the owned
                // allocator/ECB directly rather than dereferencing its former world pointer.
                ecb.Dispose();
                domainAllocator.Data.Free(managedWorld.UntypedPointer);
                allocatorHandler.AllocatorWrapper.Allocator = allocator;
                allocatorHandler.Dispose();
                Get(temporaryId) = default;
                if (targetId != temporaryId && targetId < MAX_WORLD_COUNT
                    && Get(targetId).unsafeWorldPtr.cached == world.unsafeWorldPtr.cached)
                    Get(targetId) = default;
                lastFreeSlot = temporaryId;
                worldCount--;
                throw;
            }
        }
        public static void DisposeStatic()
        {
            WorldIoRequests.CancelAll();
            StaticObjectRefStorage.Clear();
            OnDisposeStaticEvent?.Invoke();
            OnDisposeStaticEvent = null;
            OnWorldCreatingEvent = null;
            staticInited = false;
            lastFreeSlot = 0;
            lastWorldID = 0;
            worldCount = 0;
            SingletonRegistry.ResetAll();
            WorldSystems.Dispose();
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

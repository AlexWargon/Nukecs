using System.Collections.Generic;
using Unity.Collections;

namespace Wargon.Nukecs
{
    public unsafe struct UnityAllocatorHandler
    {
        private AllocatorHelper<UnityAllocatorWrapper> allocatorHelper;

        public ref UnityAllocatorWrapper AllocatorWrapper
        {
            get => ref allocatorHelper.Allocator;
        }

        internal UnityAllocatorWrapper* AllocatorWrapperPtr => (UnityAllocatorWrapper*)Mem.AddressOf(ref allocatorHelper.Allocator);
        public AllocatorManager.AllocatorHandle AllocatorHandle => allocatorHelper.Allocator.Handle;

        /// <summary>Unity allocator over a new arena of <paramref name="sizeInBytes"/> that it owns.</summary>
        public UnityAllocatorHandler(long sizeInBytes)
        {
            this = default;
            allocatorHelper = new AllocatorHelper<UnityAllocatorWrapper>(Allocator.Persistent);
            AllocatorWrapper.Initialize(sizeInBytes);
            WarmUp();
        }

        /// <summary>Unity allocator over an existing arena; the arena is not disposed with it.</summary>
        public UnityAllocatorHandler(MemAllocator* arena)
        {
            this = default;
            allocatorHelper = new AllocatorHelper<UnityAllocatorWrapper>(Allocator.Persistent);
            AllocatorWrapper.InitializeBorrowed(arena);
            WarmUp();
        }

        private void WarmUp()
        {
            using var d = new NativeReference<int>(AllocatorWrapper.ToAllocator);
        }

        public void Dispose()
        {
            AllocatorWrapper.Dispose();
            allocatorHelper.Dispose();
        }
    }

    public static class WorldUnityAllocatorExtensions
    {
        private static readonly Dictionary<int, UnityAllocatorHandler> handlers = new();

        /// <summary>
        /// Unity allocator over the world's arena, for Unity containers that should live in it.
        /// Registered on first request and released when the world is disposed. Such containers
        /// keep raw pointers, so they are not valid after the world is loaded from a save.
        /// </summary>
        public static unsafe Allocator GetUnityAllocator(this World world)
        {
            if (!handlers.TryGetValue(world.Id, out var handler))
            {
                if (handlers.Count == 0) NukecsLifecycle.WorldDisposing += Release;
                handler = new UnityAllocatorHandler(world.AllocatorPtr);
                handlers[world.Id] = handler;
            }
            return handler.AllocatorWrapper.ToAllocator;
        }

        private static void Release(int worldId)
        {
            if (!handlers.TryGetValue(worldId, out var handler)) return;
            handler.Dispose();
            handlers.Remove(worldId);
            if (handlers.Count == 0) NukecsLifecycle.WorldDisposing -= Release;
        }
    }
}

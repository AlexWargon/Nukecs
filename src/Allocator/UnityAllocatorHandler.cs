using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    public struct UnityAllocatorHandler
    {
        // Use AllocatorHelper to help creating the example custom allocator
        private AllocatorHelper<UnityAllocatorWrapper> allocatorHelper;

        // Custom allocator property for accessibility
        public ref UnityAllocatorWrapper AllocatorWrapper
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref allocatorHelper.Allocator;
        }

        internal unsafe UnityAllocatorWrapper* AllocatorWrapperPtr => (UnityAllocatorWrapper*)UnsafeUtility.AddressOf(ref allocatorHelper.Allocator);
        public AllocatorManager.AllocatorHandle AllocatorHandle => allocatorHelper.Allocator.Handle;

        // Create the example custom allocator
        private unsafe void CreateCustomAllocator(AllocatorManager.AllocatorHandle backgroundAllocator,
            long initialValue)
        {
            // Allocate the custom allocator from backgroundAllocator and register the allocator
            allocatorHelper = new AllocatorHelper<UnityAllocatorWrapper>(backgroundAllocator);
            AllocatorWrapper.Initialize(initialValue);
        }

        // Dispose the custom allocator
        private void DisposeAllocator()
        {
            // Dispose the custom allocator
            var allocator = AllocatorWrapper.ToAllocator;
            AllocatorWrapper.Dispose();
            // Unregister the custom allocator and dispose it
            allocatorHelper.Dispose();
            dbug.log($"Allocator Disposed {(int)allocator}");
        }

        // Constructor of user structure
        public UnityAllocatorHandler(long sizeInBytes)
        {
            this = default;
            CreateCustomAllocator(Allocator.Persistent, sizeInBytes);
            dbug.log($"Allocator created with {sizeInBytes} bytes buffer");
        }
        
        // Dispose the user structure
        public void Dispose()
        {
            DisposeAllocator();
        }
    }
}
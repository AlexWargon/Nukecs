using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    public struct UnityAllocatorHandler
    {
        private AllocatorHelper<UnityAllocatorWrapper> allocatorHelper;

        public ref UnityAllocatorWrapper AllocatorWrapper
        {
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref allocatorHelper.Allocator;
        }

        internal unsafe UnityAllocatorWrapper* AllocatorWrapperPtr => (UnityAllocatorWrapper*)UnsafeUtility.AddressOf(ref allocatorHelper.Allocator);
        public AllocatorManager.AllocatorHandle AllocatorHandle => allocatorHelper.Allocator.Handle;
        private void CreateCustomAllocator(AllocatorManager.AllocatorHandle backgroundAllocator,
            long initialValue)
        {
            allocatorHelper = new AllocatorHelper<UnityAllocatorWrapper>(backgroundAllocator);
            AllocatorWrapper.Initialize(initialValue);
            using var d = new NativeReference<int>(AllocatorWrapper.ToAllocator);
        }
        private void DisposeAllocator()
        {
            //var allocator = AllocatorWrapper.ToAllocator;
            AllocatorWrapper.Dispose();
            allocatorHelper.Dispose();
        }
        public UnityAllocatorHandler(long sizeInBytes)
        {
            this = default;
            CreateCustomAllocator(Allocator.Persistent, sizeInBytes);
        }
        public void Dispose()
        {
            DisposeAllocator();
        }

    }
}
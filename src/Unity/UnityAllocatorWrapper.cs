using System;
using Unity.Burst;
using Unity.Collections;

namespace Wargon.Nukecs
{
    /// <summary>
    /// Unity custom allocator over a <see cref="MemAllocator"/>: either one it owns, or a world
    /// arena it only borrows.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct UnityAllocatorWrapper : AllocatorManager.IAllocator
    {
        private MemAllocator* allocator;
        private byte ownsAllocator;
        private AllocatorManager.AllocatorHandle m_handle;
        public AllocatorManager.TryFunction Function => AllocatorFunction;

        public AllocatorManager.AllocatorHandle Handle
        {
            get => m_handle;
            set => m_handle = value;
        }

        public UnityAllocatorWrapper(byte dumb)
        {
            allocator = null;
            ownsAllocator = 0;
            m_handle = default;
        }

        public ref MemAllocator Allocator => ref *allocator;
        public Allocator ToAllocator => m_handle.ToAllocator;
        public bool IsCustomAllocator => true;
        public bool IsAutoDispose => false;

        public void Initialize(long capacity)
        {
            allocator = (MemAllocator*)Mem.Malloc(sizeof(MemAllocator), Mem.AlignOf<MemAllocator>(), AllocatorHandle.Persistent);
            *allocator = new MemAllocator(capacity);
            ownsAllocator = 1;
        }

        public void InitializeBorrowed(MemAllocator* borrowed)
        {
            allocator = borrowed;
            ownsAllocator = 0;
        }

        public void Dispose()
        {
            if (ownsAllocator != 0 && allocator != null)
            {
                allocator->Dispose();
                Mem.Free(allocator, AllocatorHandle.Persistent);
            }
            allocator = null;
        }

        public int Try(ref AllocatorManager.Block block)
        {
            var error = AllocatorError.NO_ERRORS;
            if (block.Range.Pointer == IntPtr.Zero)
            {
                block.Range.Pointer = allocator->AllocateRaw(block.Bytes, ref error);
            }
            else
            {
                allocator->Free((byte*)block.Range.Pointer, ref error);
            }
            return error;
        }

        [BurstCompile(CompileSynchronously = true)]
        [AOT.MonoPInvokeCallback(typeof(AllocatorManager.TryFunction))]
        public static int AllocatorFunction(IntPtr allocatorState, ref AllocatorManager.Block block)
        {
            return ((UnityAllocatorWrapper*)allocatorState)->Try(ref block);
        }

        public MemAllocator* GetAllocatorPtr() => allocator;
    }
}

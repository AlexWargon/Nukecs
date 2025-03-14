using System;
using Unity.Burst;
using Unity.Collections;

namespace Wargon.Nukecs
{
    [BurstCompile(CompileSynchronously = true)]
    public unsafe struct UnityAllocatorWrapper : AllocatorManager.IAllocator
    {
        public SerializableMemoryAllocator Allocator;
        private AllocatorManager.AllocatorHandle m_handle;
        public AllocatorManager.TryFunction Function => AllocatorFunction;

        public AllocatorManager.AllocatorHandle Handle
        {
            get => m_handle;
            set => m_handle = value;
        }

        public UnityAllocatorWrapper(byte dumb)
        {
            Allocator = default;
            m_handle = default;
        }
        public Allocator ToAllocator => m_handle.ToAllocator;
        public bool IsCustomAllocator => true;
        public bool IsAutoDispose => false;

        public void Initialize(long capacity)
        {
            Allocator = new SerializableMemoryAllocator(capacity);
        }

        public void Dispose()
        {
            Allocator.Dispose();
        }

        public int Try(ref AllocatorManager.Block block)
        {
            var error = AllocatorError.NO_ERRORS;
            if (block.Range.Pointer == IntPtr.Zero)
            {
                block.Range.Pointer = Allocator.AllocateRaw(block.Bytes, ref error);
            }
            else
            {
                Allocator.Free((byte*)block.Range.Pointer, ref error);
            }
            //ShowError(error);
            return error;
        }
        // [BurstDiscard]
        // private void ShowError(int error)
        // {
        //     if (error != 0)
        //     {
        //         switch (error)
        //         {
        //             case AllocatorError.ERROR_ALLOCATOR_OUT_OF_MEMORY:
        //                 dbug.error($"Allocator out of memory.");
        //                 break;
        //             case AllocatorError.ERROR_ALLOCATOR_MAX_BLOCKS_REACHED:
        //                 dbug.error("Allocator max blocks reached.");
        //                 break;
        //         }
        //     }
        // }
        
        [BurstCompile(CompileSynchronously = true)]
        [AOT.MonoPInvokeCallback(typeof(AllocatorManager.TryFunction))]
        public static int AllocatorFunction(IntPtr allocatorState, ref AllocatorManager.Block block)
        {
            return ((UnityAllocatorWrapper*)allocatorState)->Try(ref block);
        }

        public SerializableMemoryAllocator* GetAllocatorPtr()
        {
            fixed (SerializableMemoryAllocator* ptr = &Allocator)
            {
                return ptr;
            }
        }
    }
}
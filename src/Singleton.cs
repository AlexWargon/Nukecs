using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
// ReSharper disable Unity.SharedStaticUnmanagedType
// ReSharper disable InconsistentNaming

namespace Wargon.Nukecs.Tests {

    /// <summary>
    /// Burst-compatible global singleton.
    /// The SharedStatic holds only a pointer + size (fixed layout), the value itself lives in
    /// a Persistent Malloc block. SharedStatic memory survives domain reloads and its size is
    /// fixed on first creation, so storing T inline would require an editor restart after
    /// every change of T's layout. Values are disposed on <see cref="SingletonRegistry.ResetAll"/>
    /// (World.DisposeStatic and before every assembly reload in the editor).
    /// </summary>
    [BurstCompile] 
    public unsafe struct Singleton<T> where T : unmanaged, IInit, IDisposable
    {
        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(SingletonRegistry.ResetDelegate))]
        private static void Reset()
        {
            ref var data = ref instance.Data;
            if (data.Value == null) return;
            // a block allocated for an older layout of T is freed without Dispose:
            // its fields cannot be interpreted with the current layout
            if (data.IsCreated != 0 && data.Size == sizeof(T) && data.OwnsValue != 0)
                data.Value->Dispose();
            UnsafeUtility.Free(data.Value, Allocator.Persistent);
            data = default;
        }
        
        private static readonly SharedStatic<Reference> instance = SharedStatic<Reference>.GetOrCreate<Singleton<T>>();
        public static ref T Instance
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ref var data = ref instance.Data;
                if (data.IsCreated == 0 || data.Size != sizeof(T))
                    Create();
                return ref *data.Value;
            }
        }

        public static bool IsCreated => instance.Data.IsCreated != 0 && instance.Data.Size == sizeof(T);

        public static void Set(ref T reference) {
            ref var data = ref Allocate();
            *data.Value = reference;
            data.IsCreated = 1;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Create()
        {
            ref var data = ref Allocate();
            *data.Value = new T();
            data.Value->Init();
            data.OwnsValue = 1;
            data.IsCreated = 1;
        }

        private static ref Reference Allocate()
        {
            ref var data = ref instance.Data;
            if (data.Value != null && data.Size == sizeof(T)) return ref data;
            if (data.Value != null)
                UnsafeUtility.Free(data.Value, Allocator.Persistent); // stale block from an older layout
            data = default;
            data.Value = (T*)UnsafeUtility.Malloc(sizeof(T), UnsafeUtility.AlignOf<T>(), Allocator.Persistent);
            UnsafeUtility.MemClear(data.Value, sizeof(T));
            data.Size = sizeof(T);
            RegisterReset();
            return ref data;
        }

        [BurstDiscard]
        private static void RegisterReset()
        {
            var fnPtr = BurstCompiler.CompileFunctionPointer<SingletonRegistry.ResetDelegate>(Reset);
            SingletonRegistry.Register(fnPtr.Value);
        }

        // Layout must not depend on T: SharedStatic size is fixed for the editor session.
        private struct Reference
        {
            internal T* Value;
            internal int Size;
            internal byte IsCreated;
            internal byte OwnsValue; // created through Instance (Init called) -> Dispose on reset
        }
    }

    public interface IInit {
        void Init();
    }
    
    [BurstCompile] 
    public struct SingletonRegistry
    {
        private static readonly SharedStatic<UnsafeList<IntPtr>> resetFunctions = SharedStatic<UnsafeList<IntPtr>>.GetOrCreate<SingletonRegistry>();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void ResetDelegate();

        public static void ResetAll()
        {
            if (resetFunctions.Data.IsCreated)
            {
                for (int i = 0; i < resetFunctions.Data.Length; i++)
                {
                    var fn = new FunctionPointer<ResetDelegate>(resetFunctions.Data[i]);
                    fn.Invoke();
                }
                resetFunctions.Data.Dispose();
                resetFunctions.Data = default;
            }
        }

        internal static void Register(IntPtr resetPtr)
        {
            if (!resetFunctions.Data.IsCreated)
            {
                resetFunctions.Data = new UnsafeList<IntPtr>(4, Allocator.Persistent);
            }
            resetFunctions.Data.Add(resetPtr);
        }
    }
}

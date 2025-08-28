using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Wargon.Nukecs.Tests {
    [BurstCompile] 
    public struct Singleton<T> where T : unmanaged, IInit, IDisposable
    {
        [BurstCompile]
        [AOT.MonoPInvokeCallback(typeof(SingletonRegistry.ResetDelegate))]
        private static void Reset()
        {
            if (instance.Data.IsCreated)
            {
                instance.Data.Value.Dispose();
                instance.Data = default;
            }
            //dbug.log(typeof(T).Name + " reseted", Color.green);
        }
        
        private static readonly SharedStatic<Reference> instance = SharedStatic<Reference>.GetOrCreate<Singleton<T>>();
        public static ref T Instance
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (instance.Data.IsCreated == false)
                {
                    instance.Data.Value = new T();
                    instance.Data.Value.Init();
                    instance.Data.IsCreated = true;
                    var fnPtr = BurstCompiler.CompileFunctionPointer<SingletonRegistry.ResetDelegate>(Reset);
                    SingletonRegistry.Register(fnPtr.Value);
                }

                return ref instance.Data.Value;
            }
        }

        public static void Set(ref T reference) {
            instance.Data.Value = reference;
            instance.Data.IsCreated = true;
        }

        private struct Reference
        {
            internal T Value;
            internal bool IsCreated;
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
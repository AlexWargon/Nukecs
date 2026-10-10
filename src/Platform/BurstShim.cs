// Outside Unity there is no Burst: this file provides the subset of the Burst API the core
// uses, with plain .NET semantics, so the core source compiles unchanged. In Unity the real
// types are used and this file is empty.
#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Unity.Burst
{
    public enum OptimizeFor
    {
        Default = 0,
        Performance = 1,
        Size = 2,
        FastCompilation = 3,
        Balanced = 4
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Assembly)]
    public sealed class BurstCompileAttribute : Attribute
    {
        public bool CompileSynchronously { get; set; }
        public OptimizeFor OptimizeFor { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public sealed class BurstDiscardAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.Struct | AttributeTargets.Method)]
    public sealed class NoAliasAttribute : Attribute { }

    /// <summary>Static data shared per (T, context) key; plain process-wide memory without Burst.</summary>
    public readonly unsafe struct SharedStatic<T> where T : struct
    {
        private readonly void* _buffer;

        private SharedStatic(void* buffer) => _buffer = buffer;

        public ref T Data
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.AsRef<T>(_buffer);
        }

        public void* UnsafeDataPointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer;
        }

        public static SharedStatic<T> GetOrCreate<TContext>(uint alignment = 0) =>
            new SharedStatic<T>(Storage<TContext, TContext>.Get(alignment));

        public static SharedStatic<T> GetOrCreate<TContext, TSubContext>(uint alignment = 0) =>
            new SharedStatic<T>(Storage<TContext, TSubContext>.Get(alignment));

        private static class Storage<TContext, TSubContext>
        {
            private static readonly object gate = new object();
            private static void* buffer;

            internal static void* Get(uint alignment)
            {
                if (buffer != null) return buffer;
                lock (gate)
                {
                    if (buffer == null)
                        buffer = AllocateZeroed(Unsafe.SizeOf<T>(), Math.Max(alignment, 16u));
                    return buffer;
                }
            }
        }

        // Never freed, like Burst's shared statics within a domain.
        private static void* AllocateZeroed(int size, uint alignment)
        {
            var raw = (byte*)Marshal.AllocHGlobal(size + (int)alignment);
            var aligned = (byte*)(((ulong)raw + alignment - 1) & ~(ulong)(alignment - 1));
            Unsafe.InitBlockUnaligned(aligned, 0, (uint)size);
            return aligned;
        }
    }

    /// <summary>
    /// Function pointer whose <see cref="Invoke"/> returns the original managed delegate when the
    /// pointer came from <see cref="BurstCompiler.CompileFunctionPointer{T}"/> (no marshalling per
    /// call). Other pointers are converted once and cached.
    /// </summary>
    public readonly struct FunctionPointer<T>
    {
        private readonly IntPtr _ptr;

        public FunctionPointer(IntPtr ptr) => _ptr = ptr;

        public IntPtr Value => _ptr;

        public bool IsCreated => _ptr != IntPtr.Zero;

        public T Invoke
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (T)(object)FunctionPointerRegistry.Resolve(_ptr, typeof(T));
        }
    }

    internal static class FunctionPointerRegistry
    {
        private static readonly ConcurrentDictionary<IntPtr, Delegate> delegates = new ConcurrentDictionary<IntPtr, Delegate>();

        // Keeps the delegate alive so its native thunk stays valid for code that stores Value.
        internal static IntPtr Register(Delegate function)
        {
            var ptr = Marshal.GetFunctionPointerForDelegate(function);
            delegates[ptr] = function;
            return ptr;
        }

        internal static Delegate Resolve(IntPtr ptr, Type delegateType)
        {
            if (delegates.TryGetValue(ptr, out var function)) return function;
            return delegates.GetOrAdd(ptr, p => Marshal.GetDelegateForFunctionPointer(p, delegateType));
        }
    }

    public static class BurstCompiler
    {
        public static bool IsEnabled => false;

        public static FunctionPointer<T> CompileFunctionPointer<T>(T delegateMethod) where T : class =>
            new FunctionPointer<T>(FunctionPointerRegistry.Register((Delegate)(object)delegateMethod));
    }
}

namespace AOT
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MonoPInvokeCallbackAttribute : Attribute
    {
        public MonoPInvokeCallbackAttribute(Type type) { }
    }
}
#endif

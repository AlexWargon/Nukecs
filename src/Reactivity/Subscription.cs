using System;
using Unity.Burst;

namespace Wargon.Nukecs.Reactivity
{
    [Flags]
    public enum ReactOptions : byte
    {
        None = 0,
        /// <summary>Auto-unsubscribe after first dispatch.</summary>
        Once = 1,
        /// <summary>Invoke the callback synchronously on subscribe with the current value.</summary>
        TriggerImmediately = 2,
        /// <summary>
        /// Reserved flag — set automatically when a Burst function pointer is supplied
        /// via <c>OnChangeBurst</c>. The dispatcher invokes it without a managed transition.
        /// </summary>
        IsBurst = 4,
    }

    /// <summary>
    /// One reactive subscription. Holds either a managed <see cref="ReactDelegate{T}"/>
    /// or a pinned Burst function pointer (via <see cref="UntypedUnmanagedDelegate"/>).
    /// A subscription is identified by its <see cref="Token"/> (returned to the user
    /// for <c>OffChange</c>).
    /// </summary>
    internal sealed class Subscription<T> : IDisposable where T : unmanaged, IComponent
    {
        public long Token;
        public int EntityId;             // -1 = world-level subscription
        public ReactDelegate<T> Managed;
        // For Burst subscriptions: non-generic delegate retained for managed invocation.
        public ReactDelegateBurst BurstManaged;
        public IntPtr BurstFnPtr;        // function pointer (0 if managed)
        private UntypedUnmanagedDelegate _burstHandle;   // owns GCHandle for non-Burst thunk
        private bool _usingBurstCompiledPtr;             // true if BurstFnPtr came from BurstCompiler

        public ReactFilter<T> ManagedFilter;
        public ReactFilterBurst BurstManagedFilter;
        public IntPtr FilterFnPtr;       // function pointer to ReactFilterBurst (0 if none)
        private UntypedUnmanagedDelegate _filterHandle;
        private bool _usingBurstCompiledFilter;

        public ReactOptions Options;

        /// <summary>
        /// Set when <see cref="ReactOptions.TriggerImmediately"/> was requested at subscribe
        /// time but the component was not yet on the entity (deferred Add via ECB).
        /// The check system consumes this on first observation of T and enqueues
        /// the entity for dispatch, so the initial trigger fires on the next OnUpdate
        /// (after ECB playback) instead of synchronously.
        /// </summary>
        public bool TriggerPending;

        public bool IsBurst => (Options & ReactOptions.IsBurst) != 0;
        public bool IsOnce => (Options & ReactOptions.Once) != 0;

        public void SetBurst(ReactDelegateBurst cb)
        {
            BurstManaged = cb;
            BurstFnPtr = CompileOrMarshal(cb, out _usingBurstCompiledPtr, out _burstHandle);
        }

        /// <summary>
        /// Attach a managed filter (any delegate). Invoked from the main-thread
        /// dispatcher via normal managed call.
        /// </summary>
        public void SetManagedFilter(ReactFilter<T> filter)
        {
            ManagedFilter = filter;
        }

        /// <summary>
        /// Attach a Burst filter — compiles via BurstCompiler if the underlying
        /// method is <c>[BurstCompile]</c>, otherwise falls back to a managed thunk.
        /// </summary>
        public void SetBurstFilter(ReactFilterBurst filter)
        {
            BurstManagedFilter = filter;
            FilterFnPtr = CompileOrMarshal(filter, out _usingBurstCompiledFilter, out _filterHandle);
        }

        /// <summary>
        /// Compile a delegate to a function pointer via BurstCompiler if its underlying
        /// method is <c>[BurstCompile]</c> (real native code, no managed transition).
        /// Otherwise fall back to <c>Marshal.GetFunctionPointerForDelegate</c> (managed
        /// thunk — callable but executed as managed code, not Burst).
        /// </summary>
        private static IntPtr CompileOrMarshal<TDel>(TDel cb, out bool isBurstCompiled, out UntypedUnmanagedDelegate handle) where TDel : Delegate
        {
            var method = cb.Method;
            var canBurstCompile = method != null
                                  && method.IsStatic
                                  && method.GetCustomAttributes(typeof(BurstCompileAttribute), false).Length > 0;
            if (canBurstCompile)
            {
                try
                {
                    // BurstCompiler.CompileFunctionPointer<T> produces a real native
                    // function pointer (compiled by Burst). Requires a non-generic
                    // delegate type — ReactDelegateBurst / ReactFilterBurst qualify.
                    var fp = BurstCompiler.CompileFunctionPointer(cb);
                    isBurstCompiled = true;
                    handle = default;
                    return fp.Value;
                }
                catch
                {
                    // Burst JIT not available (e.g. certain Editor configurations,
                    // disabled Burst). Fall through to managed thunk.
                }
            }

            isBurstCompiled = false;
            handle = UntypedUnmanagedDelegate.Create(cb);
            return handle.Ptr;
        }

        public void Dispose()
        {
            if (BurstFnPtr != IntPtr.Zero)
            {
                if (!_usingBurstCompiledPtr) _burstHandle.Dispose();
                BurstFnPtr = IntPtr.Zero;
            }
            if (FilterFnPtr != IntPtr.Zero)
            {
                if (!_usingBurstCompiledFilter) _filterHandle.Dispose();
                FilterFnPtr = IntPtr.Zero;
            }
            Managed = null;
            BurstManaged = null;
            ManagedFilter = null;
            BurstManagedFilter = null;
            TriggerPending = false;
        }
    }
}

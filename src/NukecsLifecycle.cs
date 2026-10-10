using System;
using System.Collections.Generic;

namespace Wargon.Nukecs
{
    /// <summary>
    /// Process-level signals the core receives from its host (Nukecs.Unity connects them to Unity).
    /// Main thread only.
    /// </summary>
    public static class NukecsLifecycle
    {
        private static readonly List<Action> quittingHandlers = new();
        private static Action<Action> subscribeQuitting;
        private static Action<Action> unsubscribeQuitting;

        /// <summary>
        /// Application shutdown. Once a host connects its own quitting event, handlers are
        /// forwarded to it directly, so they keep that event's ordering.
        /// </summary>
        public static event Action Quitting
        {
            add
            {
                if (subscribeQuitting != null) subscribeQuitting(value);
                else quittingHandlers.Add(value);
            }
            remove
            {
                if (unsubscribeQuitting != null) unsubscribeQuitting(value);
                else quittingHandlers.Remove(value);
            }
        }

        /// <summary>Connects a host quitting event; handlers registered earlier move to it.</summary>
        public static void SetQuittingSource(Action<Action> subscribe, Action<Action> unsubscribe)
        {
            subscribeQuitting = subscribe;
            unsubscribeQuitting = unsubscribe;
            foreach (var handler in quittingHandlers)
                subscribe(handler);
            quittingHandlers.Clear();
        }

        /// <summary>For hosts without their own quitting event.</summary>
        public static void NotifyQuitting()
        {
            foreach (var handler in quittingHandlers.ToArray())
                handler();
        }

        /// <summary>
        /// Raised near the end of <see cref="World.DisposeStatic"/>. Unlike World's dispose-static
        /// callbacks, subscriptions survive the call.
        /// </summary>
        public static event Action StaticDisposed;

        internal static void RaiseStaticDisposed() => StaticDisposed?.Invoke();
    }
}

using System.Collections.Generic;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Tracks the ReactiveCheckJob handle per world so that dispatch systems can
    /// wait on ONLY the check job — not ALL pending jobs (which state.Dependencies.Complete() would do).
    /// </summary>
    internal static class ReactiveJobSync
    {
        private static readonly Dictionary<int, NJobHandle> CheckHandles = new();
        private static readonly object Lock = new();

        public static void SetCheckHandle(int worldId, NJobHandle handle)
        {
            lock (Lock) CheckHandles[worldId] = handle;
        }

        /// <summary>Complete ONLY the check job for this world (not all dependencies).</summary>
        public static void CompleteCheck(int worldId)
        {
            NJobHandle h;
            lock (Lock)
            {
                if (!CheckHandles.TryGetValue(worldId, out h)) return;
            }
            h.Complete();
        }
    }
}

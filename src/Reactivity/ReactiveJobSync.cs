using System.Collections.Generic;
using Unity.Jobs;

namespace Wargon.Nukecs.Reactivity
{
    /// <summary>
    /// Tracks the ReactiveCheckJob handle per world so that dispatch systems can
    /// wait on ONLY the check job — not ALL pending jobs (which state.Dependencies.Complete() would do).
    /// </summary>
    internal static class ReactiveJobSync
    {
        private static readonly Dictionary<int, JobHandle> CheckHandles = new();
        private static readonly object Lock = new();

        public static void SetCheckHandle(int worldId, JobHandle handle)
        {
            lock (Lock) CheckHandles[worldId] = handle;
        }

        /// <summary>Complete ONLY the check job for this world (not all dependencies).</summary>
        public static void CompleteCheck(int worldId)
        {
            JobHandle h;
            lock (Lock)
            {
                if (!CheckHandles.TryGetValue(worldId, out h)) return;
            }
            h.Complete();
        }
    }
}

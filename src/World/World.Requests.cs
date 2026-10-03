using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wargon.Nukecs
{
    public partial struct World
    {
        /// <summary>Queue file saving before the world's next primary Systems.OnUpdate.</summary>
        public Task RequestSave(string path) => WorldIoRequests.Enqueue(this, path, false);

        /// <summary>Queue file loading before the world's next primary Systems.OnUpdate.</summary>
        public Task<World> RequestLoad(string path) => WorldIoRequests.Enqueue(this, path, true);

        /// <summary>Save immediately after completing the world's registered jobs.</summary>
        public void Save(string path) => SaveToFile(path);
    }

    // Managed file-I/O adapter. Queues deliberately live outside the serialized arena;
    // no WorldUnsafe layout changes or managed state in Burst system parameters.
    internal static class WorldIoRequests
    {
        private sealed class Request
        {
            internal string Path;
            internal bool Load;
            internal TaskCompletionSource<World> Completion =
                new TaskCompletionSource<World>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        private static readonly object Gate = new object();
        private static readonly Queue<Request>[] Pending = new Queue<Request>[World.MAX_WORLD_COUNT];
        private static readonly long[] Lifetimes = new long[World.MAX_WORLD_COUNT];

        internal static long Lifetime(int id) { lock (Gate) return Lifetimes[id]; }

        internal static Task<World> Enqueue(World world, string path, bool load)
        {
            if (!world.IsAlive) throw new InvalidOperationException("The world is disposed.");
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A file path is required.", nameof(path));
            var request = new Request { Path = path, Load = load };
            lock (Gate) {
                var id = world.Id;
                if (Pending[id] == null) Pending[id] = new Queue<Request>();
                Pending[id].Enqueue(request);
            }
            return request.Completion.Task;
        }

        internal static void Process(ref World world)
        {
            Queue<Request> batch;
            int id;
            long lifetime;
            lock (Gate) {
                id = world.Id;
                lifetime = Lifetimes[id];
                batch = Pending[id];
                if (batch == null || batch.Count == 0) return;
                Pending[id] = null;
            }
            // Previous OnUpdate completed its jobs. Process a detached batch so requests
            // issued by load callbacks wait for the next update instead of recursing.
            while (batch.Count != 0) {
                var request = batch.Dequeue();
                if (Lifetime(id) != lifetime) {
                    request.Completion.TrySetCanceled();
                    continue;
                }
                try {
                    if (request.Load) world.LoadFromFileCore(request.Path, false);
                    else world.SaveToFileCore(request.Path, false);
                    request.Completion.TrySetResult(world);
                }
                catch (Exception error) { request.Completion.TrySetException(error); }
            }
        }

        internal static void Cancel(int id)
        {
            Queue<Request> batch;
            lock (Gate) { batch = Pending[id]; Pending[id] = null; Lifetimes[id]++; }
            if (batch == null) return;
            while (batch.Count != 0) batch.Dequeue().Completion.TrySetCanceled();
        }

        internal static void CancelAll()
        {
            for (var id = 0; id < Pending.Length; id++) Cancel(id);
        }
    }
}

using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Wargon.Nukecs
{
    public unsafe class DelegateSystemRunner : ISystemRunner
    {
        private readonly Action<IntPtr, IntPtr, IntPtr> _execute;
        private readonly string _name;
        private readonly Threads _mode;
        private ECBJob _ecbJob;
        private int _queryId;

        public DelegateSystemRunner(Action<IntPtr, IntPtr, IntPtr> execute, string name, Threads mode, int queryId = -1)
        {
            _execute = execute;
            _name = name;
            _mode = mode;
            _ecbJob = default;
            _queryId = queryId;
        }

        private QueryUnsafe* GetQueryPtr(World.WorldUnsafe* world)
        {
            if (_queryId < 0) return null;
            var q = world->queries.ElementAt(_queryId);
            return q.Ptr;
        }

        public JobHandle Schedule(UpdateContext updateContext, ref State state)
        {
            var worldPtr = state.World.UnsafeWorld;
            var statePtr = (IntPtr)Unsafe.AsPointer(ref state);
            var queryPtr = (IntPtr)GetQueryPtr(worldPtr);
            _execute((IntPtr)worldPtr, statePtr, queryPtr);

            if (_mode == Threads.Main)
            {
                if (worldPtr->ECB.HasCommands)
                {
                    _ecbJob.ECB = worldPtr->ECB;
                    _ecbJob.world = state.World;
                    _ecbJob.Execute();
                }
            }
            else
            {
                _ecbJob.ECB = worldPtr->ECB;
                _ecbJob.world = state.World;
                _ecbJob.Run();
            }

            return state.Dependencies;
        }

        public void Run(ref State state)
        {
            var worldPtr = state.World.UnsafeWorld;
            var statePtr = (IntPtr)Unsafe.AsPointer(ref state);
            var queryPtr = (IntPtr)GetQueryPtr(worldPtr);
            _execute((IntPtr)worldPtr, statePtr, queryPtr);
            worldPtr->ECB.Playback(ref state.World);
        }

        public string Name => _name;
    }
}

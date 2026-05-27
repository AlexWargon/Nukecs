using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngineInternal;
using Wargon.Nukecs.Collections;
using static Wargon.Nukecs.UnsafeStatic;

namespace Wargon.Nukecs
{
    internal struct EventsStorage
    {
        private HashMap<int, ptr> _events;
        private const int DEFAULT_SIZE = 16;

        public void OnDeserialize(ref MemAllocator allocator)
        {
            _events.OnDeserialize(ref allocator);
        }
        internal EventsStorage(ref UnityAllocatorHandler allocator)
        {
            _events = new HashMap<int, ptr>(DEFAULT_SIZE, ref allocator);
        }
        public bool Has<TEvents>()
        {
            return _events.ContainsKey(typeof(TEvents).GetHashCode());
        }

        public unsafe void ClearAll()
        {
            foreach (var kvPair in _events)
            {
                as_ref<ClearListProxy>(kvPair.Value.cached).Clear();
            }
        }
        public ptr<TEvents> Get<TEvents>(ref ptr<World.WorldUnsafe> world) where TEvents : unmanaged, ISystemParam
        {
            var hash = typeof(TEvents).GetHashCode();
            if (_events.TryGetValue(hash, out var ptr))
            {
                return ptr.AsTyped<TEvents>();
            }
            
            var events = default(TEvents);
            events.Init(ref world);
            var typedPtr = world.Ref._allocate_ptr<TEvents>();
            typedPtr.Ref = events;
            ptr = typedPtr.UntypedPointer;
            _events.Add(hash, ptr);
            return typedPtr;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct ClearEventsProxy
    {
        private ClearListProxy _list;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _list.Clear();
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Events<TEvent> : ISystemParam where TEvent : unmanaged
    {
        private MemoryList<TEvent> _list;
        private World.WorldUnsafe* _world;
        private Spinner _spinner;
        public SystemParamMetaType MetaType => SystemParamMetaType.Events;
        public Events(int capacity, World.WorldUnsafe* world)
        {
            _list = new MemoryList<TEvent>(capacity, ref world->AllocatorRef);
            _world = world;
            _spinner = default;
        }
        
        public int Count => _list.Length;

        public void Add(TEvent item)
        {
            _list.Add(item, ref _world->AllocatorRef);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddPar(in TEvent item)
        {
            _spinner.Acquire();
            if (_list.length >= _list.capacity)
            {
                var newCap = _list.capacity == 0 ? 256 : _list.capacity * 2;
                _list.Resize(newCap, ref _world->AllocatorRef);
            }
            _list.Ptr[_list.length++] = item;
            _spinner.Release();
        }

        public void EnsureCapacity(int count)
        {
            if (_list.length + count > _list.capacity)
                _list.Resize(_list.length + count, ref _world->AllocatorRef);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EventsParallelReader<TEvent> GetParallelReader()
        {
            return new EventsParallelReader<TEvent>(_list.Ptr, _list.Length);
        }

        public void Clear()
        {
            _list.Clear();
        }
        public MemoryList<TEvent>.Enumerator GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            this = new Events<TEvent>(256, world.Ptr);
        }

        public void Update(ref World world, IntPtr data)
        {

        }
    }

    public readonly unsafe ref struct EventsParallelReader<TEvent> where TEvent : unmanaged
    {
        private readonly TEvent* _ptr;
        public readonly int Length;

        public EventsParallelReader(TEvent* ptr, int length)
        {
            _ptr = ptr;
            Length = length;
        }

        public ref TEvent this[int index] => ref _ptr[index];

        public Enumerator GetEnumerator() => new Enumerator { Ptr = _ptr, Length = Length, Index = -1 };

        public ref struct Enumerator
        {
            internal TEvent* Ptr;
            internal int Length;
            internal int Index;
            public bool MoveNext() => ++Index < Length;
            public ref TEvent Current => ref Ptr[Index];
        }
    }
}
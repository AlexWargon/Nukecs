using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Wargon.Nukecs.Collections;
using static Wargon.Nukecs.UnsafeStatic;

namespace Wargon.Nukecs
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct Events<TEvent> : ISystemParam where TEvent : unmanaged
    {
        private MemoryList<TEvent> _list;
        private Spinner _spinner;
        private byte _worldId;
        private Range _range;
        public Range Range => _range;
        private ref World World
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref World.GetInternal(_worldId);
        }
        
        public SystemParamMetaType MetaType => SystemParamMetaType.Events;
        public Events(int capacity, World.WorldUnsafe* world)
        {
            _list = new MemoryList<TEvent>(capacity, ref world->AllocatorRef);
            _worldId = world->Id;
            _spinner = default;
            _range = default;
        }
        
        public int Count => _list.Length;

        public void Add(TEvent item)
        {
            _list.Add(item, ref World.AllocatorRef);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddPar(in TEvent item)
        {
            _spinner.Acquire();
            if (_list.length >= _list.capacity)
            {
                var newCap = _list.capacity == 0 ? 256 : _list.capacity * 2;
                _list.Resize(newCap, ref World.AllocatorRef);
            }
            _list.Ptr[_list.length++] = item;
            _spinner.Release();
        }

        public void EnsureCapacity(int count)
        {
            if (_list.length + count > _list.capacity)
                _list.Resize(_list.length + count, ref World.AllocatorRef);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public EventsParallelReader<TEvent> ReadPar()
        {
            return new EventsParallelReader<TEvent>(_list.Ptr, _list.Length);
        }

        public void Clear()
        {
            _list.Clear();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RangeEnumerator GetEnumerator()
        {
            return new RangeEnumerator(_list.Ptr, _range);
        }

        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            this = new Events<TEvent>(4096, world.Ptr);
        }

        public void Update(ref World world, IntPtr data)
        {
            _range = data == IntPtr.Zero 
                ? new Range(0, _list.Length) 
                : *(Range*)data;
        }

        public ref struct RangeEnumerator
        {
            internal TEvent* ptr;
            internal int start;
            internal int end;
            internal int index;

            public RangeEnumerator(TEvent* ptr, Range range)
            {
                this.ptr = ptr;
                this.start = range.start;
                this.end = range.end;
                this.index = range.start - 1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => ++index < end;

            public ref TEvent Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref ptr[index];
            }
        }
    }

    public readonly unsafe ref struct EventsParallelReader<TEvent> where TEvent : unmanaged
    {
        private readonly TEvent* _ptr;
        // ReSharper disable once InconsistentNaming
        public readonly int Length;

        public EventsParallelReader(TEvent* ptr, int length)
        {
            _ptr = ptr;
            Length = length;
        }

        public ref TEvent this[int index] => ref _ptr[index];

        public Enumerator GetEnumerator() => new () { ptr = _ptr, length = Length, index = -1 };

        public ref struct Enumerator
        {
            internal TEvent* ptr;
            internal int length;
            internal int index;
            public bool MoveNext() => ++index < length;
            public ref TEvent Current => ref ptr[index];
        }
    }
    
    internal struct EventsStorage
    {
        private HashMap<int, ptr> _events;
        private const int DEFAULT_SIZE = 16;
        internal EventsStorage(ref UnityAllocatorHandler allocator)
        {
            _events = new HashMap<int, ptr>(DEFAULT_SIZE, ref allocator);
        }
        public unsafe void OnDeserialize(ref MemAllocator allocator)
        {
            _events.OnDeserialize(ref allocator);
            foreach (var kvPair in _events)
            {
                kvPair.Value.OnDeserialize(ref allocator);
                as_ref<OnDeserializeListProxy>(kvPair.Value.cached).OnDeserialize(ref allocator);
            }
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
}
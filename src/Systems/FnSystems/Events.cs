using System;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs
{
    public unsafe struct Events<TEvent> : ISystemParam where TEvent : unmanaged
    {
        private MemoryList<TEvent> _list;
        private World.WorldUnsafe* _world;

        public Events(int capacity, World.WorldUnsafe* world)
        {
            _list = new MemoryList<TEvent>(capacity, ref world->AllocatorRef);
            _world = world;
        }
        
        public void Add(TEvent item)
        {
            _list.Add(item, ref _world->AllocatorRef);
        }

        public void Clear()
        {
            _list.Clear();
        }
        public MemoryList<TEvent>.Enumerator GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        public SystemParamMetaType MetaType => SystemParamMetaType.Events;
        public void Init(ref ptr<World.WorldUnsafe> world)
        {
            _list = new MemoryList<TEvent>(256, ref world.Ref.AllocatorRef);
            _world = world.Ptr;
        }

        public void Update(ref World world, IntPtr data)
        {

        }

        IntPtr ISystemParam.GetData() => IntPtr.Zero;
        bool ISystemParam.TryGetQuery(out ptr<QueryUnsafe> query)
        {
            query = default;
            return false;
        }
    }
}
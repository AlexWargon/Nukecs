using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Wargon.Nukecs.Collections;
using static Wargon.Nukecs.UnsafeStatic;

#pragma warning disable CS0162 // Unreachable code detected
namespace Wargon.Nukecs
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct GenericPool
    {
        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => !UnsafeBufferPtr.IsDefault;
        }

        public ComponentPoolUntyped* UnsafeBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => UnsafeBufferPtr.Ptr;
        }

        internal ptr<ComponentPoolUntyped> UnsafeBufferPtr;
        public int Count => 0;

        public void OnDeserialize(ref MemAllocator allocator)
        {
            UnsafeBufferPtr.OnDeserialize(ref allocator);
            UnsafeBufferPtr.Ref.OnDeserialization(ref allocator);
        }
        internal static GenericPool Create<T>(int size, ref ptr<World.WorldUnsafe> world)
            where T : unmanaged
        {
            return new GenericPool
            {
                UnsafeBufferPtr = ComponentPoolUntyped.Create<T>(size, ref world)
            };
        }

        internal static GenericPool Create(in ComponentTypeData typeData, int size, ref ptr<World.WorldUnsafe> world)
        {
            return new GenericPool
            {
                UnsafeBufferPtr = ComponentPoolUntyped.Create(size, ref world, in typeData)
            };
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetRef<T>(int index) where T : unmanaged
        {
            return ref UnsafeBufferPtr.Ref.Get<T>(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* UnsafeGetPtr(int index)
        {
            return UnsafeBufferPtr.Ref.GetPtr(index);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set<T>(int index, in T value) where T : unmanaged
        {
            UnsafeBufferPtr.Ref.Add(index, in value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index)
        {
        }

        public ref T GetSingleton<T>() where T : unmanaged
        {
            return ref UnsafeBufferPtr.Ref.Get<T>(0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPtr(int index, byte* value)
        {
            UnsafeBufferPtr.Ref.AddPtr(index, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteData(int index, byte* src, int size)
        {
            UnsafeBufferPtr.Ref.WriteData(index, src, size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytes(int index, byte[] value)
        {
            UnsafeBufferPtr.Ref.WriteBytes(index, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytesUnsafe(int index, byte* value, int sizeInBytes)
        {
            UnsafeBufferPtr.Ref.WriteBytesUnsafe(index, value, sizeInBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetArraySlot(int index)
        {
            return UnsafeBufferPtr.Ref.GetArraySlot(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddObject(int index, IComponent component)
        {
            UnsafeBufferPtr.Ref.AddObject(index, component);
        }

        public IComponent GetObject(int index)
        {
            return UnsafeBufferPtr.Ref.GetObject(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetObject(int index, IComponent component)
        {
            UnsafeBufferPtr.Ref.SetObject(index, component);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int index)
        {
            UnsafeBufferPtr.Ref.Remove(index);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DisposeComponent(int index)
        {
            UnsafeBufferPtr.Ref.DisposeComponent(index);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Copy(int source, int destination)
        {
            UnsafeBufferPtr.Ref.Copy(source, destination);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckResize(int index)
        {
        }

        // public byte[] Serialize()
        // {
        //     return new Span<byte>(UnsafeBuffer->buffer, UnsafeBuffer->componentTypeData.size * UnsafeBuffer->capacity).ToArray();
        // }
        //
        // public byte[] Serialize(int entity)
        // {
        //     return new Span<byte>(UnsafeGetPtr(entity), UnsafeBuffer->componentTypeData.size).ToArray();
        // }
        // public void Deserialize(byte[] data)
        // {
        //     fixed (byte* ptr = data)
        //     {
        //         UnsafeUtility.MemCpy(UnsafeBuffer->buffer, ptr, data.Length);
        //     }
        // }
    }

    // public readonly unsafe struct ComponentPool<T> where T : unmanaged {
    //     [NativeDisableUnsafePtrRestriction]
    //     private readonly T* _buffer;
    //
    //     internal ComponentPool(void* buffer) {
    //         _buffer = (T*) buffer;
    //     }
    //     
    //     [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //     public ref T Get(int index) {
    //         return ref _buffer[index];
    //     }
    // }

    public static unsafe class GenericPoolExtensions
    {
        public static ComponentPool<T> AsComponentPool<T>(in this GenericPool genericPool) where T : unmanaged
        {
            return new ComponentPool<T>(genericPool.UnsafeBufferPtr.Ptr);
        }

        public static AspectData<T> AsAspectData<T>(in this GenericPool genericPool) where T : unmanaged
        {
            return new AspectData<T>
            {
                PoolOwner = genericPool.UnsafeBufferPtr.Ptr
            };
        }
    }

    public unsafe struct ComponentPool<T> where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        public ComponentPoolUntyped* poolPtr;
        public ptr<World.WorldUnsafe> world;
        public ComponentTypeData data;

        internal ComponentPool(ComponentPoolUntyped* pool)
        {
            poolPtr = pool;
            world = pool->world;
            data = pool->componentTypeData;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Get(int index)
        {
            var chunkIndex = index / Chunk.MAX_CHUNK_SIZE;
            var componentIndex = index % Chunk.MAX_CHUNK_SIZE;
            ref var chunk = ref poolPtr->Chunks.ElementAt(chunkIndex);
            return ref get_ref_element<T>(chunk.buffer.Ptr, componentIndex);
        }


        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // public ref T Get(int index)
        // {
        //     var chunkIndex = index / Chunk.MAX_CHUNK_SIZE;
        //     var componentIndex = index % Chunk.MAX_CHUNK_SIZE;
        //     ref var chunk = ref chunks.ElementAt(chunkIndex);
        //     // if (chunk.isCreated == 0)
        //     // {
        //     //     //dbug.log($"is array element : {data.IsArrayElement}", Color.yellow);
        //     //     var size = data.IsArrayElement ? data.size * ComponentArray.DEFAULT_MAX_CAPACITY : data.size;
        //     //     chunk.buffer = world.Ref.AllocatorRef.AllocatePtr<byte>(Chunk.MAX_CHUNK_SIZE * size);
        //     //     mem_clear(chunk.buffer.cached, Chunk.MAX_CHUNK_SIZE * size);
        //     //     chunk.isCreated = 1;
        //     // }
        //     return ref get_ref_element<T>(chunk.buffer.Ptr, componentIndex);
        // }
    }

    public struct Chunk
    {
        public ptr<byte> buffer;
        public byte isCreated;

        public const int MAX_CHUNK_SIZE = 64;
        public const int CHUNK_INDEX_BITSFIFT = 6;
        public const int COMPONENT_INDEX_BITSHIFT = 63;
        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => isCreated == 1;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => isCreated = value ? (byte)1 : (byte)0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T GetRef<T>(Chunk* chunks, int index) where T : unmanaged
        {
            var chunkIndex = index / MAX_CHUNK_SIZE;
            var componentIndex = index % MAX_CHUNK_SIZE;
            ref var page = ref chunks[chunkIndex];
            return ref get_ref_element<T>(page.buffer.Ptr, componentIndex);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T* GetPtr<T>(Chunk* chunks, int index) where T : unmanaged
        {
            var chunkIndex = index / MAX_CHUNK_SIZE;
            var componentIndex = index % MAX_CHUNK_SIZE;
            ref var page = ref chunks[chunkIndex];
            return get_element_ptr<T>(page.buffer.Ptr, componentIndex);
        }
        
        public void OnDeserialize(ref MemAllocator allocator)
        {
            buffer.OnDeserialize(ref allocator);
        }
    }

    public struct EntityChunkInfo
    {
        public int chunk;
        public int component;
    }

    public unsafe struct ComponentPoolUntyped
    {
        public MemoryList<Chunk> Chunks;
        public ptr<World.WorldUnsafe> world;
        public int componentSize;
        public ComponentTypeData componentTypeData;
        internal Spinner chunkLock;

        public void OnDeserialization(ref MemAllocator allocator) {
            var idx = componentTypeData.index;
            componentTypeData = ComponentTypeMap.GetComponentType(idx);
            Chunks.OnDeserialize(ref allocator);
            foreach (ref var chunk in Chunks)
            {
                if (chunk.isCreated == 1)
                {
                    chunk.OnDeserialize(ref allocator);
                }
            }
            world.OnDeserialize(ref allocator);
            FixPtrComponents(ref allocator);
        }

        private void FixPtrComponents(ref MemAllocator allocator) {
            if (!componentTypeData.isArray) return;
            var elementsSize = componentTypeData.size;
            foreach (ref var chunk in Chunks) {
                if (chunk.isCreated != 1) continue;
                for (var i = 0; i < Chunk.MAX_CHUNK_SIZE; i++)
                {
                    var ptr = chunk.buffer.Ptr + i * elementsSize;
                    ComponentArrayData.Restore(ptr, ref allocator);
                    dbug.log($"resoterd {componentTypeData.ManagedType.Name}");
                    //fixMethod.Invoke(null, new object[] { (IntPtr)(chunk.buffer.Ptr + i * componentTypeData.size), allocator });
                }
            }
        }

        public static ptr<ComponentPoolUntyped> Create<T>(int size, ref ptr<World.WorldUnsafe> world)
            where T : unmanaged
        {
            var ptr = world.Ref.AllocatorRef.AllocatePtr<ComponentPoolUntyped>();
            ptr.Ref.chunkLock = default;
            ptr.Ref.Chunks = new MemoryList<Chunk>(
                capacity:size / Chunk.MAX_CHUNK_SIZE, 
                allocator:ref world.Ref.AllocatorRef, 
                clear:true, 
                lenAsCapacity:true);
            
            ptr.Ref.componentTypeData = ComponentType<T>.Data;
            ptr.Ref.componentSize = ptr.Ref.componentTypeData.size;
            ptr.Ref.world = world;
            return ptr;
        }

        public static ptr<ComponentPoolUntyped> Create(int size, ref ptr<World.WorldUnsafe> world,
            in ComponentTypeData data)
        {
            var ptr = world.Ref.AllocatorRef.AllocatePtr<ComponentPoolUntyped>();
            ptr.Ref.chunkLock = default;
            ptr.Ref.Chunks =
                new MemoryList<Chunk>(size / Chunk.MAX_CHUNK_SIZE, ref world.Ref.AllocatorRef, clear: true, 
                    lenAsCapacity:true);
            ptr.Ref.componentSize = data.size;
            ptr.Ref.componentTypeData = data;
            ptr.Ref.world = world;
            return ptr;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Chunk GetChunk(int entity)
        {
            var chunkIndex = entity / Chunk.MAX_CHUNK_SIZE;

            if (chunkIndex < Chunks.capacity)
            {
                ref var chunk = ref Chunks.ElementAt(chunkIndex);
                if (chunk.isCreated == 1) return ref chunk;
            }

            chunkLock.Acquire();
            try {
                if (chunkIndex >= Chunks.capacity)
                {
                    var newCapacity = Chunks.capacity * 2;
                    if (newCapacity <= chunkIndex) newCapacity = chunkIndex + 1;
                    Chunks.Resize(newCapacity, ref world.Ref.AllocatorRef);
                }
                ref var lockedChunk = ref Chunks.ElementAt(chunkIndex);
                if (lockedChunk.isCreated == 0)
                {
                    var size = componentTypeData.IsArrayElement
                        ? componentTypeData.size * ComponentArray.DEFAULT_MAX_CAPACITY
                        : componentTypeData.size;
                    lockedChunk.buffer = world.Ref.AllocatorRef.AllocatePtr<byte>(Chunk.MAX_CHUNK_SIZE * size);
                    mem_clear(lockedChunk.buffer.cached, Chunk.MAX_CHUNK_SIZE * size);
                    lockedChunk.isCreated = 1;
                }
                return ref lockedChunk;
            }
            finally {
                chunkLock.Release();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetArraySlot(int entity)
        {
            ref var chunk = ref GetChunk(entity);
            var componentIndex = entity % Chunk.MAX_CHUNK_SIZE;
            var slotSize = componentTypeData.size * ComponentArray.DEFAULT_MAX_CAPACITY;
            return chunk.buffer.Ptr + componentIndex * slotSize;
        }

        public int GetComponentIndex(int entity)
        {
            return entity % Chunk.MAX_CHUNK_SIZE;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add<T>(int entity, in T data) where T : unmanaged
        {
            var componentIndex = entity % Chunk.MAX_CHUNK_SIZE;
            ref var chunk = ref GetChunk(entity);
            write_element(chunk.buffer.cached, componentIndex, data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddPtr(int entity, byte* data)
        {
            var componentIndex = entity % Chunk.MAX_CHUNK_SIZE;
            ref var chunk = ref GetChunk(entity);
            memcpy(chunk.buffer.Ptr + componentIndex * componentTypeData.size, data, componentTypeData.size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteData(int entity, byte* src, int size)
        {
            if (!componentTypeData.isTag)
            {
                var componentIndex = entity % Chunk.MAX_CHUNK_SIZE;
                ref var chunk = ref GetChunk(entity);
                memcpy(chunk.buffer.Ptr + componentIndex * componentTypeData.size, src, size);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Get<T>(int entity) where T : unmanaged
        {
            var chunkIndex = entity / Chunk.MAX_CHUNK_SIZE;
            var componentIndex = entity % Chunk.MAX_CHUNK_SIZE;
            ref var page = ref Chunks[chunkIndex];
            return ref get_ref_element<T>(page.buffer.Ptr, componentIndex);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetPtr(int entity)
        {
            var componentIndex = entity % Chunk.MAX_CHUNK_SIZE;
            ref var page = ref GetChunk(entity);
            return page.buffer.Ptr + componentIndex * componentTypeData.size;
        }

        public void WriteBytes(int entity, byte[] value)
        {
            if (!componentTypeData.isTag)
            {
                if (entity < 0) throw new IndexOutOfRangeException($"Index {entity} is out of range for GenericPool.");
                ref var chunk = ref GetChunk(entity);
                var componentIndex = entity % Chunk.MAX_CHUNK_SIZE;
                fixed (byte* ptr = value)
                {
                    memcpy(chunk.buffer.cached + componentIndex * componentTypeData.size, ptr, componentTypeData.size);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytesUnsafe(int entity, byte* value, int sizeInBytes)
        {
            if (!componentTypeData.isTag)
            {
                if (entity < 0) throw new IndexOutOfRangeException($"Index {entity} is out of range for GenericPool.");
                ref var chunk = ref GetChunk(entity);
                var componentIndex = entity % Chunk.MAX_CHUNK_SIZE;
                memcpy(chunk.buffer.cached + componentIndex * componentTypeData.size, value, componentTypeData.size);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddObject(int entity, IComponent component)
        {
            if (!componentTypeData.isTag)
            {
                if (entity < 0) throw new IndexOutOfRangeException($"Index {entity} is out of range for GenericPool.");
                ref var chunk = ref GetChunk(entity);
                var componentIndex = entity % Chunk.MAX_CHUNK_SIZE;
                ComponentHelpers.Write(chunk.buffer.cached, componentIndex, componentTypeData.size,
                    componentTypeData.index, component);
            }
        }

        public IComponent GetObject(int entity)
        {
            ref var chunk = ref GetChunk(entity);
            var componentIndex = entity % Chunk.MAX_CHUNK_SIZE;
            return ComponentHelpers.Read(chunk.buffer.cached, componentIndex, componentTypeData.size,
                componentTypeData.index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetObject(int entity, IComponent component)
        {
            if (!componentTypeData.isTag)
            {
                if (entity < 0) throw new IndexOutOfRangeException($"Index {entity} is out of range for GenericPool.");
                ref var chunk = ref GetChunk(entity);
                var componentIndex = entity % Chunk.MAX_CHUNK_SIZE;
                ComponentHelpers.Write(chunk.buffer.cached, componentIndex, componentTypeData.size,
                    componentTypeData.index, component);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int entity)
        {
            ref var chunk = ref GetChunk(entity);
            var componentIndex = entity % Chunk.MAX_CHUNK_SIZE;
            if (componentTypeData.isDisposable) DisposeComponent(componentIndex, ref chunk);
            if (!componentTypeData.isTag)
            {
                mem_clear(
                    chunk.buffer.cached + componentIndex * 
                    componentTypeData.size, componentTypeData.size);
                // memcpy(chunk.buffer.cached + componentIndex * componentTypeData.size, 
                //     componentTypeData.defaultValue, componentTypeData.size);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DisposeComponent(int index)
        {
            ref var chunk = ref GetChunk(index);
            var componentIndex = index % Chunk.MAX_CHUNK_SIZE;
            componentTypeData.DisposeFn().Invoke(chunk.buffer.cached, componentIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DisposeComponent(int index, ref Chunk chunk)
        {
            componentTypeData.DisposeFn().Invoke(chunk.buffer.cached, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyComponent(int fromEnt, int toEntity, byte* fromBuffer, byte* toBuffer, int fromC, int toC)
        {
            componentTypeData.CopyFn().Invoke(fromBuffer, toBuffer, fromEnt, toEntity, fromC, toC);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Copy(int source, int destination)
        {
            if (!componentTypeData.isTag)
            {
                var srcIndex = source % Chunk.MAX_CHUNK_SIZE;
                var destIndex = destination % Chunk.MAX_CHUNK_SIZE;
                ref var srcChunk = ref GetChunk(source);
                ref var destChunk = ref GetChunk(destination);
                
                if (componentTypeData.isCopyable)
                    CopyComponent(source, destination,
                        srcChunk.buffer.cached, destChunk.buffer.cached, srcIndex, destIndex);
                else
                {
                    memcpy(destChunk.buffer.cached + destIndex * componentTypeData.size,
                        srcChunk.buffer.cached + srcIndex * componentTypeData.size,
                        componentTypeData.size);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void FillContiguous<T>(byte* buf, int compStart, int count, in T data) where T : unmanaged
        {
            new Span<T>(buf + compStart * sizeof(T), count).Fill(data);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BatchAdd(int start, int end)
        {
            if (componentTypeData.isTag) return;
            var entity = start;
            while (entity < end)
            {
                var compStart = entity & (Chunk.MAX_CHUNK_SIZE - 1);
                var count = Chunk.MAX_CHUNK_SIZE - compStart;
                if (entity + count > end) count = end - entity;
                ref var chunk = ref GetChunk(entity);
                mem_clear(
                    chunk.buffer.cached + compStart * componentTypeData.size,
                    count * componentTypeData.size);
                entity += count;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BatchAdd<T>(int start, int end, in T data) where T : unmanaged
        {
            if (componentTypeData.isTag) return;
            var entity = start;
            while (entity < end)
            {
                var compStart = entity & (Chunk.MAX_CHUNK_SIZE - 1);
                var count = Chunk.MAX_CHUNK_SIZE - compStart;
                if (entity + count > end) count = end - entity;
                ref var chunk = ref GetChunk(entity);
                FillContiguous(chunk.buffer.cached, compStart, count, in data);
                entity += count;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BatchAdd<T>(int* entities, int count, in T data) where T : unmanaged
        {
            if (componentTypeData.isTag) return;
            var curChunkIdx = -1;
            byte* curBuf = null;
            for (var i = 0; i < count; i++)
            {
                var entity = entities[i];
                var chunkIndex = entity >> Chunk.CHUNK_INDEX_BITSFIFT;
                if (chunkIndex != curChunkIdx)
                {
                    ref var chunk = ref GetChunk(entity);
                    curBuf = chunk.buffer.cached;
                    curChunkIdx = chunkIndex;
                }
                write_element(curBuf, entity & (Chunk.MAX_CHUNK_SIZE - 1), data);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BatchAdd(int* entities, int count)
        {
            if (componentTypeData.isTag) return;
            var curChunkIdx = -1;
            byte* curBuf = null;
            for (var i = 0; i < count; i++)
            {
                var entity = entities[i];
                var chunkIndex = entity >> Chunk.CHUNK_INDEX_BITSFIFT;
                if (chunkIndex != curChunkIdx)
                {
                    ref var chunk = ref GetChunk(entity);
                    curBuf = chunk.buffer.cached;
                    curChunkIdx = chunkIndex;
                }
                var compIndex = entity & (Chunk.MAX_CHUNK_SIZE - 1);
                mem_clear(curBuf + compIndex * componentTypeData.size, componentTypeData.size);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BatchRemove(int start, int end)
        {
            var entity = start;
            while (entity < end)
            {
                var compStart = entity & (Chunk.MAX_CHUNK_SIZE - 1);
                var count = Chunk.MAX_CHUNK_SIZE - compStart;
                if (entity + count > end) count = end - entity;
                ref var chunk = ref GetChunk(entity);
                if (componentTypeData.isDisposable)
                {
                    for (var c = 0; c < count; c++)
                        componentTypeData.DisposeFn().Invoke(chunk.buffer.cached, compStart + c);
                }
                if (!componentTypeData.isTag)
                {
                    mem_clear(
                        chunk.buffer.cached + compStart * componentTypeData.size,
                        count * componentTypeData.size);
                }
                entity += count;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BatchRemove(int* entities, int count)
        {
            var curChunkIdx = -1;
            byte* curBuf = null;
            for (var i = 0; i < count; i++)
            {
                var entity = entities[i];
                var chunkIndex = entity >> Chunk.CHUNK_INDEX_BITSFIFT;
                var compIndex = entity & (Chunk.MAX_CHUNK_SIZE - 1);
                if (chunkIndex != curChunkIdx)
                {
                    ref var chunk = ref GetChunk(entity);
                    curBuf = chunk.buffer.cached;
                    curChunkIdx = chunkIndex;
                }
                if (componentTypeData.isDisposable)
                    componentTypeData.DisposeFn().Invoke(curBuf, compIndex);
                if (!componentTypeData.isTag)
                    mem_clear(curBuf + compIndex * componentTypeData.size, componentTypeData.size);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BatchSet<T>(int start, int end, in T data) where T : unmanaged
        {
            if (componentTypeData.isTag) return;
            var entity = start;
            while (entity < end)
            {
                var compStart = entity & (Chunk.MAX_CHUNK_SIZE - 1);
                var count = Chunk.MAX_CHUNK_SIZE - compStart;
                if (entity + count > end) count = end - entity;
                ref var chunk = ref GetChunk(entity);
                FillContiguous(chunk.buffer.cached, compStart, count, in data);
                entity += count;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BatchSet<T>(int* entities, int count, in T data) where T : unmanaged
        {
            if (componentTypeData.isTag) return;
            var curChunkIdx = -1;
            byte* curBuf = null;
            for (var i = 0; i < count; i++)
            {
                var entity = entities[i];
                var chunkIndex = entity >> Chunk.CHUNK_INDEX_BITSFIFT;
                if (chunkIndex != curChunkIdx)
                {
                    ref var chunk = ref GetChunk(entity);
                    curBuf = chunk.buffer.cached;
                    curChunkIdx = chunkIndex;
                }
                write_element(curBuf, entity & (Chunk.MAX_CHUNK_SIZE - 1), data);
            }
        }

    }
}
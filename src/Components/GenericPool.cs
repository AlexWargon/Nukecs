using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
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
            get => (IntPtr)UnsafeBuffer != IntPtr.Zero;
        }

        internal ComponentPoolUntyped* UnsafeBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => unsafeBufferPtr.Ptr;
        }

        internal ptr<ComponentPoolUntyped> unsafeBufferPtr;
        public int Count => 0;

        internal static GenericPool Create<T>(int size, ref ptr<World.WorldUnsafe> world)
            where T : unmanaged, IComponent
        {
            return new GenericPool
            {
                unsafeBufferPtr = ComponentPoolUntyped.Create<T>(size, ref world)
            };
        }

        internal static GenericPool Create(in ComponentTypeData typeData, int size, ref ptr<World.WorldUnsafe> world)
        {
            return new GenericPool
            {
                unsafeBufferPtr = ComponentPoolUntyped.Create(size, ref world, in typeData)
            };
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct GenericPoolUnsafe
        {
            [NativeDisableUnsafePtrRestriction] internal byte* buffer;
            internal int count;
            internal int capacity;
            internal ComponentTypeData componentTypeData;
            internal Allocator allocator;
            internal World.WorldUnsafe* worldPtr;
            internal ptr_offset bufferPtr;

            internal static GenericPoolUnsafe* CreateBuffer<T>(int size, World.WorldUnsafe* world) where T : unmanaged
            {
                ref var allocator = ref world->AllocatorRef;
                var ptrRef = allocator.AllocatePtr<GenericPoolUnsafe>();
                var ptr = ptrRef.Ptr;
                var type = ComponentType<T>.Data;
                *ptr = new GenericPoolUnsafe
                {
                    capacity = size,
                    count = 0,
                    allocator = world->Allocator,
                    componentTypeData = type,
                    worldPtr = world
                };
                ptr->bufferPtr = world->AllocatorRef.AllocateRaw(size * type.size);
                ptr->buffer = ptr->bufferPtr.AsPtr<byte>(ref world->AllocatorRef);
                UnsafeUtility.MemClear(ptr->buffer, size * type.size);
                return ptr;
            }

            internal static ptr<GenericPoolUnsafe> CreateBufferPtr<T>(int size, World.WorldUnsafe* world)
                where T : unmanaged
            {
                ref var allocator = ref world->AllocatorRef;
                var ptrRef = allocator.AllocatePtr<GenericPoolUnsafe>();
                var ptr = ptrRef.Ptr;
                var type = ComponentType<T>.Data;
                *ptr = new GenericPoolUnsafe
                {
                    capacity = size,
                    count = 0,
                    allocator = world->Allocator,
                    componentTypeData = type,
                    worldPtr = world
                };
                ptr->bufferPtr = world->AllocatorRef.AllocateRaw(size * type.size);
                ptr->buffer = ptr->bufferPtr.AsPtr<byte>(ref world->AllocatorRef);
                UnsafeUtility.MemClear(ptr->buffer, size * type.size);
                return ptrRef;
            }

            internal static GenericPoolUnsafe* CreateBuffer(ComponentTypeData typeData, int size,
                World.WorldUnsafe* world)
            {
                ref var allocator = ref world->AllocatorRef;
                var ptrRef = allocator.AllocatePtr<GenericPoolUnsafe>();
                var ptr = ptrRef.Ptr;
                size = typeData.isTag ? 1 : size;
                *ptr = new GenericPoolUnsafe
                {
                    capacity = size,
                    count = 0,
                    allocator = world->Allocator,
                    componentTypeData = typeData,
                    worldPtr = world
                };
                ptr->bufferPtr = world->AllocatorRef.AllocateRaw(size * typeData.size);
                ptr->buffer = ptr->bufferPtr.AsPtr<byte>(ref world->AllocatorRef);
                UnsafeUtility.MemClear(ptr->buffer, typeData.size * size);
                return ptr;
            }

            internal static ptr<GenericPoolUnsafe> CreateBufferPtr(ComponentTypeData typeData, int size,
                World.WorldUnsafe* world)
            {
                ref var allocator = ref world->AllocatorRef;
                var ptrRef = allocator.AllocatePtr<GenericPoolUnsafe>();
                var ptr = ptrRef.Ptr;
                size = typeData.isTag ? 1 : size;
                *ptr = new GenericPoolUnsafe
                {
                    capacity = size,
                    count = 0,
                    allocator = world->Allocator,
                    componentTypeData = typeData,
                    worldPtr = world
                };
                ptr->bufferPtr = world->AllocatorRef.AllocateRaw(size * typeData.size);
                ptr->buffer = ptr->bufferPtr.AsPtr<byte>(ref world->AllocatorRef);
                UnsafeUtility.MemClear(ptr->buffer, typeData.size * size);
                return ptrRef;
            }


            public readonly ref T GetRef<T>(int index) where T : unmanaged
            {
                return ref ((T*)buffer)[index];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetRef<T>(int index) where T : unmanaged
        {
            return ref unsafeBufferPtr.Ref.Get<T>(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* UnsafeGetPtr(int index)
        {
            return unsafeBufferPtr.Ref.GetPtr(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set<T>(int index, in T value) where T : unmanaged
        {
            unsafeBufferPtr.Ref.Add(index, in value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index)
        {
        }

        public ref T GetSingleton<T>() where T : unmanaged
        {
            return ref unsafeBufferPtr.Ref.Get<T>(0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPtr(int index, byte* value)
        {
            unsafeBufferPtr.Ref.AddPtr(index, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytes(int index, byte[] value)
        {
            unsafeBufferPtr.Ref.WriteBytes(index, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytesUnsafe(int index, byte* value, int sizeInBytes)
        {
            unsafeBufferPtr.Ref.WriteBytesUnsafe(index, value, sizeInBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddObject(int index, IComponent component)
        {
            unsafeBufferPtr.Ref.AddObject(index, component);
        }

        public IComponent GetObject(int index)
        {
            return unsafeBufferPtr.Ref.GetObject(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetObject(int index, IComponent component)
        {
            unsafeBufferPtr.Ref.SetObject(index, component);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int index)
        {
            unsafeBufferPtr.Ref.Remove(index);
        }
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public void DisposeComponent(int index)
        {
            unsafeBufferPtr.Ref.DisposeComponent(index);
        }
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public void Copy(int source, int destination)
        {
            unsafeBufferPtr.Ref.Copy(source, destination);
        }
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif


        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            return new ComponentPool<T>(ref genericPool.unsafeBufferPtr.Ref);
        }

        public static AspectData<T> AsAspectData<T>(in this GenericPool genericPool) where T : unmanaged, IComponent
        {
            return new AspectData<T>
            {
                Buffer = genericPool.UnsafeBuffer->chunks.Ptr
            };
        }
    }

    public unsafe struct ComponentPool<T> where T : unmanaged
    {
        public MemoryList<Chunk> chunks;
        public ptr<World.WorldUnsafe> world;
        public ComponentTypeData data;

        internal ComponentPool(ref ComponentPoolUntyped pool)
        {
            chunks = pool.chunks;
            world = pool.world;
            data = pool.componentTypeData;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Get(int index)
        {
            var chunkIndex = index / Chunk.MAX_CHUNK_SIZE;
            var componentIndex = index % Chunk.MAX_CHUNK_SIZE;
            ref var chunk = ref chunks.ElementAt(chunkIndex);
            // if (chunk.isCreated == 0)
            // {
            //     //dbug.log($"is array element : {data.IsArrayElement}", Color.yellow);
            //     var size = data.IsArrayElement ? data.size * ComponentArray.DEFAULT_MAX_CAPACITY : data.size;
            //     chunk.buffer = world.Ref.AllocatorRef.AllocatePtr<byte>(Chunk.MAX_CHUNK_SIZE * size);
            //     mem_clear(chunk.buffer.cached, Chunk.MAX_CHUNK_SIZE * size);
            //     chunk.isCreated = 1;
            // }
            return ref get_ref_element<T>(chunk.buffer.Ptr, componentIndex);
        }
    }

    public struct Chunk
    {
        public ptr<byte> buffer;
        public byte isCreated;

        public const int MAX_CHUNK_SIZE = 64;

        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => isCreated == 1;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => isCreated = value ? (byte)1 : (byte)0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref T GetRef<T>(Chunk* chunks, int index) where T : unmanaged, IComponent
        {
            var chunkIndex = index / MAX_CHUNK_SIZE;
            var componentIndex = index % MAX_CHUNK_SIZE;
            ref var page = ref chunks[chunkIndex];

            return ref get_ref_element<T>(page.buffer.Ptr, componentIndex);
        }
    }

    public struct EntityChunkInfo
    {
        public int chunk;
        public int component;
    }

    public unsafe struct ComponentPoolUntyped
    {
        public MemoryList<Chunk> chunks;
        public ptr<World.WorldUnsafe> world;
        public int componentSize;
        public ComponentTypeData componentTypeData;

        public void OnDeserialization(ref MemAllocator allocator)
        {
            chunks.OnDeserialize(ref allocator);
            world.OnDeserialize(ref allocator);
        }

        public static ptr<ComponentPoolUntyped> Create<T>(int size, ref ptr<World.WorldUnsafe> world)
            where T : unmanaged, IComponent
        {
            var ptr = world.Ref.AllocatorRef.AllocatePtr<ComponentPoolUntyped>();
            ptr.Ref.chunks =
                new MemoryList<Chunk>(size / Chunk.MAX_CHUNK_SIZE, ref world.Ref.AllocatorRef, clear: true);
            ptr.Ref.componentTypeData = ComponentType<T>.Data;
            ptr.Ref.componentSize = ptr.Ref.componentTypeData.size;
            ptr.Ref.world = world;
            return ptr;
        }

        public static ptr<ComponentPoolUntyped> Create(int size, ref ptr<World.WorldUnsafe> world,
            in ComponentTypeData data)
        {
            var ptr = world.Ref.AllocatorRef.AllocatePtr<ComponentPoolUntyped>();
            ptr.Ref.chunks =
                new MemoryList<Chunk>(size / Chunk.MAX_CHUNK_SIZE, ref world.Ref.AllocatorRef, clear: true);
            ptr.Ref.componentSize = data.size;
            ptr.Ref.componentTypeData = data;
            ptr.Ref.world = world;
            return ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Chunk GetChunk(int entity)
        {
            var chunkIndex = entity / Chunk.MAX_CHUNK_SIZE;

            if (chunkIndex > chunks.capacity) chunks.Resize(chunks.capacity * 2, ref world.Ref.AllocatorRef);
            ref var chunk = ref chunks.ElementAt(chunkIndex);
            if (chunk.isCreated == 0)
            {
                //dbug.log($"is array element : {componentTypeData.IsArrayElement}", Color.yellow);
                var size = componentTypeData.IsArrayElement
                    ? componentTypeData.size * ComponentArray.DEFAULT_MAX_CAPACITY
                    : componentTypeData.size;
                chunk.buffer = world.Ref.AllocatorRef.AllocatePtr<byte>(Chunk.MAX_CHUNK_SIZE * size);
                mem_clear(chunk.buffer.cached, Chunk.MAX_CHUNK_SIZE * size);
                chunk.isCreated = 1;
            }

            return ref chunk;
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
        public ref T Get<T>(int entity) where T : unmanaged
        {
            var chunkIndex = entity / Chunk.MAX_CHUNK_SIZE;
            var componentIndex = entity % Chunk.MAX_CHUNK_SIZE;
            ref var page = ref chunks[chunkIndex];
            return ref get_ref_element<T>(page.buffer.Ptr, componentIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* GetPtr(int entity)
        {
            var chunkIndex = entity / Chunk.MAX_CHUNK_SIZE;
            var componentIndex = entity % Chunk.MAX_CHUNK_SIZE;
            ref var page = ref chunks[chunkIndex];
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
                memcpy(chunk.buffer.cached + componentIndex * componentTypeData.size, componentTypeData.defaultValue,
                    componentTypeData.size);
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
        private void CopyComponent(int from, int to, byte* fromBuffer, byte* toBuffer)
        {
            componentTypeData.CopyFn().Invoke(fromBuffer, toBuffer, from, to);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Copy(int source, int destination)
        {
            if (!componentTypeData.isTag)
            {
                ref var srcChunk = ref GetChunk(source);
                var srcIndex = source % Chunk.MAX_CHUNK_SIZE;
                
                ref var destChunk = ref GetChunk(destination);
                var destIndex = destination % Chunk.MAX_CHUNK_SIZE;
                
                if (componentTypeData.isCopyable)
                    CopyComponent(source, destination,
                        srcChunk.buffer.cached, destChunk.buffer.cached);
                else
                    memcpy(destChunk.buffer.cached + destIndex * componentTypeData.size,
                        srcChunk.buffer.cached + srcIndex * componentTypeData.size,
                        componentTypeData.size);
            }
        }

        public ComponentPool<T> AsComponentPool<T>() where T : unmanaged, IComponent
        {
            return new ComponentPool<T>(ref this);
        }
    }
}
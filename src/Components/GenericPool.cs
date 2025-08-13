using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using Wargon.Nukecs.Collections;
using static Wargon.Nukecs.UnsafeStatic;
namespace Wargon.Nukecs {
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct GenericPool {
        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (IntPtr)UnsafeBuffer != IntPtr.Zero;
        }

        internal GenericPoolUnsafe* UnsafeBuffer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => unsafeBufferPtr.Ptr;
        }
        internal ptr<GenericPoolUnsafe> unsafeBufferPtr;
        public int Count => UnsafeBuffer->count;
        
        internal static GenericPool Create<T>(int size, World.WorldUnsafe* world) where T : unmanaged {
            return new GenericPool {
                unsafeBufferPtr = GenericPoolUnsafe.CreateBufferPtr<T>(size, world),
            };
        }

        internal static GenericPool Create(ComponentType type, int size, World.WorldUnsafe* world) {
            return new GenericPool {
                unsafeBufferPtr = GenericPoolUnsafe.CreateBufferPtr(type, size, world)
            };
        }

        public T* GetBuffer<T>() where T : unmanaged, IComponent
        {
            return (T*)UnsafeBuffer->buffer;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct GenericPoolUnsafe {
            [NativeDisableUnsafePtrRestriction] internal byte* buffer;
            internal int count;
            internal int capacity;
            internal ComponentType ComponentType;
            internal Allocator allocator;
            internal World.WorldUnsafe* worldPtr;
            internal ptr_offset bufferPtr;
            
            internal static GenericPoolUnsafe* CreateBuffer<T>(int size, World.WorldUnsafe* world) where T : unmanaged
            {
                ref var allocator = ref world->AllocatorRef;
                var ptrRef = allocator.AllocatePtr<GenericPoolUnsafe>();
                var ptr = ptrRef.Ptr;
                var type = ComponentType<T>.Data;
                *ptr = new GenericPoolUnsafe {
                    capacity = size,
                    count = 0,
                    allocator = world->Allocator,
                    ComponentType = type,
                    worldPtr = world
                };
                ptr->bufferPtr = world->AllocatorRef.AllocateRaw(size * type.size);
                ptr->buffer = ptr->bufferPtr.AsPtr<byte>(ref world->AllocatorRef);
                UnsafeUtility.MemClear(ptr->buffer,size * type.size);
                return ptr;
            }

            internal static ptr<GenericPoolUnsafe> CreateBufferPtr<T>(int size, World.WorldUnsafe* world) where T : unmanaged
            {
                ref var allocator = ref world->AllocatorRef;
                var ptrRef = allocator.AllocatePtr<GenericPoolUnsafe>();
                var ptr = ptrRef.Ptr;
                var type = ComponentType<T>.Data;
                *ptr = new GenericPoolUnsafe {
                    capacity = size,
                    count = 0,
                    allocator = world->Allocator,
                    ComponentType = type,
                    worldPtr = world
                };
                ptr->bufferPtr = world->AllocatorRef.AllocateRaw(size * type.size);
                ptr->buffer = ptr->bufferPtr.AsPtr<byte>(ref world->AllocatorRef);
                UnsafeUtility.MemClear(ptr->buffer,size * type.size);
                return ptrRef;
            }
            
            internal static GenericPoolUnsafe* CreateBuffer(ComponentType type, int size, World.WorldUnsafe* world) {
                
                ref var allocator = ref world->AllocatorRef;
                var ptrRef = allocator.AllocatePtr<GenericPoolUnsafe>();
                var ptr = ptrRef.Ptr;
                size = type.isTag ? 1 : size;
                *ptr = new GenericPoolUnsafe {
                    capacity = size,
                    count = 0,
                    allocator = world->Allocator,
                    ComponentType = type,
                    worldPtr = world
                };
                ptr->bufferPtr = world->AllocatorRef.AllocateRaw(size * type.size);
                ptr->buffer = ptr->bufferPtr.AsPtr<byte>(ref world->AllocatorRef);
                UnsafeUtility.MemClear(ptr->buffer, type.size * size);
                return ptr;
            }
            
            internal static ptr<GenericPoolUnsafe> CreateBufferPtr(ComponentType type, int size, World.WorldUnsafe* world) {
                
                ref var allocator = ref world->AllocatorRef;
                var ptrRef = allocator.AllocatePtr<GenericPoolUnsafe>();
                var ptr = ptrRef.Ptr;
                size = type.isTag ? 1 : size;
                *ptr = new GenericPoolUnsafe {
                    capacity = size,
                    count = 0,
                    allocator = world->Allocator,
                    ComponentType = type,
                    worldPtr = world
                };
                ptr->bufferPtr = world->AllocatorRef.AllocateRaw(size * type.size);
                ptr->buffer = ptr->bufferPtr.AsPtr<byte>(ref world->AllocatorRef);
                UnsafeUtility.MemClear(ptr->buffer, type.size * size);
                return ptrRef;
            }
            
            
            public readonly ref T GetRef<T>(int index) where T : unmanaged {
                return ref ((T*)buffer)[index];
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckValid(int index)
        {
            //if (UnsafeBuffer == null) throw new NullReferenceException("Buffer is null!");
            if (index < 0 || index >= UnsafeBuffer->capacity) {
                throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool[{ComponentTypeMap.GetType(UnsafeBuffer->ComponentType.index).Name}] with capacity {UnsafeBuffer->capacity}.");
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetRef<T>(int index) where T : unmanaged
        {
            CheckValid(index);
            return ref ((T*)unsafeBufferPtr.Ptr->buffer)[index];
            //return ref *(T*) (impl->buffer + index * impl->elementSize);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* UnsafeGetPtr(int index)
        {
            return unsafeBufferPtr.Ptr->buffer + index * unsafeBufferPtr.Ptr->ComponentType.size;
        }

        public ref T UnsafeGetRef<T>(int index, byte[] buffer) where T : unmanaged
        {
            CheckValid(index);
            return ref unsafeBufferPtr.Ptr->bufferPtr.AsPtr<T>(buffer)[index];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set<T>(int index, in T value) where T : unmanaged
        {
            if (!UnsafeBuffer->ComponentType.isTag) {
                if (index < 0) {
                    throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {UnsafeBuffer->capacity}.");
                }
                CheckResize<T>(index);
                ((T*) unsafeBufferPtr.Ptr->buffer)[index] = value;
            }
            unsafeBufferPtr.Ptr->count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index) {
            unsafeBufferPtr.Ptr->count++;
        }

        public ref T GetSingleton<T>() where T : unmanaged {
            return ref ((T*)unsafeBufferPtr.Ptr->buffer)[0];
        }
        
        public void SetSingleton<T>(in T value) where T : unmanaged{
            ((T*)unsafeBufferPtr.Ptr->buffer)[0] = value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPtr(int index, void* value) {
            if (!unsafeBufferPtr.Ptr->ComponentType.isTag) {
                if (index < 0) {
                    throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {UnsafeBuffer->capacity}.");
                }
                CheckResize(index);
                UnsafeUtility.MemCpy(unsafeBufferPtr.Ptr->buffer + index * unsafeBufferPtr.Ptr->ComponentType.size, value, unsafeBufferPtr.Ptr->ComponentType.size);
            }
            unsafeBufferPtr.Ptr->count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytes(int index, byte[] value) {
            if (!UnsafeBuffer->ComponentType.isTag) {
                if (index < 0) {
                    throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {UnsafeBuffer->capacity}.");
                }
                CheckResize(index);
                fixed (byte* ptr = value) {
                    UnsafeUtility.MemCpy(unsafeBufferPtr.Ptr->buffer + index * unsafeBufferPtr.Ptr->ComponentType.size, ptr, unsafeBufferPtr.Ptr->ComponentType.size);
                }
            }
            UnsafeBuffer->count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytesUnsafe(int index, byte* value, int sizeInBytes) {
            if (!UnsafeBuffer->ComponentType.isTag) {
                if (index < 0) {
                    throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {UnsafeBuffer->capacity}.");
                }
                CheckResize(index);
                UnsafeUtility.MemCpy(unsafeBufferPtr.Ptr->buffer + index * unsafeBufferPtr.Ptr->ComponentType.size, value, sizeInBytes);
            }
            UnsafeBuffer->count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddObject(int index, IComponent component) {
            if (!UnsafeBuffer->ComponentType.isTag) {
                if (index < 0) {
                    throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {UnsafeBuffer->capacity}.");
                }
                CheckResize(index);
                ComponentHelpers.Write(unsafeBufferPtr.Ptr->buffer, index, unsafeBufferPtr.Ptr->ComponentType.size, unsafeBufferPtr.Ptr->ComponentType.index, component);
            }
            UnsafeBuffer->count++;
        }

        public object GetObject(int index)
        {
            return ComponentHelpers.Read(unsafeBufferPtr.Ptr->buffer, index, unsafeBufferPtr.Ptr->ComponentType.size,
                unsafeBufferPtr.Ptr->ComponentType.index);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetObject(int index, IComponent component) {
            if (!UnsafeBuffer->ComponentType.isTag) {
                if (index < 0) {
                    throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {unsafeBufferPtr.Ptr->capacity}.");
                }
                ComponentHelpers.Write(unsafeBufferPtr.Ptr->buffer, index, unsafeBufferPtr.Ptr->ComponentType.size, unsafeBufferPtr.Ptr->ComponentType.index, component);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int index) {
            if (unsafeBufferPtr.Ptr->ComponentType.isDisposable) {
                DisposeComponent(index);
            }
            if (!unsafeBufferPtr.Ptr->ComponentType.isTag)
            {
                ref readonly var type = ref unsafeBufferPtr.Ptr->ComponentType;
                UnsafeUtility.MemCpy(unsafeBufferPtr.Ptr->buffer + index * type.size, type.defaultValue, type.size);
            }
            unsafeBufferPtr.Ptr->count--;
        }
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public void DisposeComponent(int index) {
            var fn = new FunctionPointer<DisposeDelegate>(unsafeBufferPtr.Ptr->ComponentType.disposeFn);
            fn.Invoke(unsafeBufferPtr.Ptr->buffer, index);
        }
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public void Copy(int source, int destination) {
            if (!unsafeBufferPtr.Ptr->ComponentType.isTag) {
                CheckResize(math.max(destination, source));
                if (unsafeBufferPtr.Ptr->ComponentType.isCopyable) {
                    CopyComponent(source, destination);
                }
                else {
                    UnsafeUtility.MemCpy(unsafeBufferPtr.Ptr->buffer + destination * unsafeBufferPtr.Ptr->ComponentType.size, 
                        unsafeBufferPtr.Ptr->buffer + source * unsafeBufferPtr.Ptr->ComponentType.size, 
                        unsafeBufferPtr.Ptr->ComponentType.size);
                }
            }
            UnsafeBuffer->count++;
        }
#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private void CopyComponent(int from, int to) {
            unsafeBufferPtr.Ptr->ComponentType.CopyFn().Invoke(unsafeBufferPtr.Ptr->buffer, from, to);
            //ComponentHelpers.Copy(UnsafeBuffer->buffer, from, to, UnsafeBuffer->ComponentType.index);
        }

#if !NUKECS_DEBUG
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private void CheckResize<T>(int index) where T : unmanaged
        {
            return;
            if (index >= UnsafeBuffer->capacity)
            {
                // Calculate new capacity
                int newCapacity = math.max(UnsafeBuffer->capacity * 2, index + 1);

                // Allocate new buffer
                byte* newBuffer = (byte*)UnsafeUtility.MallocTracked(
                    newCapacity * UnsafeBuffer->ComponentType.size,
                    UnsafeUtility.AlignOf<T>(),
                    UnsafeBuffer->allocator, 0
                );

                if (newBuffer == null)
                {
                    throw new OutOfMemoryException("Failed to allocate memory for resizing.");
                }

                UnsafeUtility.MemClear(newBuffer, newCapacity * UnsafeBuffer->ComponentType.size);
                // Copy old data to new buffer
                UnsafeUtility.MemCpy(newBuffer, UnsafeBuffer->buffer, UnsafeBuffer->capacity * UnsafeBuffer->ComponentType.size);

                // Free old buffer
                UnsafeUtility.FreeTracked(UnsafeBuffer->buffer, UnsafeBuffer->allocator);

                // Update impl
                UnsafeBuffer->buffer = newBuffer;
                UnsafeBuffer->capacity = newCapacity;
                //Debug.Log($"GenericPool[{ComponentsMap.GetType(impl->componentTypeIndex).Name}] resized on set");
            }
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckResize(int index)
        {
            return;
        }

        public byte[] Serialize()
        {
            return new Span<byte>(UnsafeBuffer->buffer, UnsafeBuffer->ComponentType.size * UnsafeBuffer->capacity).ToArray();
        }

        public byte[] Serialize(int entity)
        {
            return new Span<byte>(UnsafeGetPtr(entity), UnsafeBuffer->ComponentType.size).ToArray();
        }
        public void Deserialize(byte[] data)
        {
            fixed (byte* ptr = data)
            {
                UnsafeUtility.MemCpy(UnsafeBuffer->buffer, ptr, data.Length);
            }
        }
    }
    
    public readonly unsafe struct ComponentPool<T> where T : unmanaged {
        [NativeDisableUnsafePtrRestriction]
        private readonly T* _buffer;

        internal ComponentPool(void* buffer) {
            _buffer = (T*) buffer;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Get(int index) {
            return ref _buffer[index];
        }
    }

    public static unsafe class GenericPoolExtensions
    {
        public static ComponentPool<T> AsComponentPool<T>(in this GenericPool genericPool) where T : unmanaged {
            return new ComponentPool<T>(genericPool.UnsafeBuffer->buffer);
        }
        public static AspectData<T> AsAspectData<T>(in this GenericPool genericPool) where T : unmanaged, IComponent {
            return new AspectData<T>
            {
                Buffer = (T*)genericPool.UnsafeBuffer->buffer
            };
        }

        public static Span<T> AsSpan<T>(in this GenericPool genericPool) where T : unmanaged, IComponent
        {
            return new Span<T>(genericPool.UnsafeBuffer->buffer, genericPool.UnsafeBuffer->capacity);
        }
    }

    public unsafe struct Page
    {
        public ptr<byte> buffer;
        public const int MAX_CHUNK_SIZE = 64;
        public byte isCreated;
    }

    public unsafe struct ComponentPool
    {
        public MemoryList<Page> chunks;
        public int componentSize;
        public void Add<T>(int entity, in T data, ref MemAllocator allocator) where T: unmanaged
        {
            var chunkIndex = entity / Page.MAX_CHUNK_SIZE;
            var componentIndex = entity % Page.MAX_CHUNK_SIZE;
            ref var page = ref chunks.ElementAt(chunkIndex);
            if (page.isCreated == 0)
            {
                page.buffer = allocator.AllocatePtr<byte>(Page.MAX_CHUNK_SIZE * componentSize);
                page.isCreated = 1;
            }
            
            write_element(page.buffer.Ptr, componentIndex * componentSize, data);
        }

        public ref T Get<T>(int entity) where T : unmanaged
        {
            var chunkIndex = entity / Page.MAX_CHUNK_SIZE;
            var componentIndex = entity % Page.MAX_CHUNK_SIZE;
            ref var page = ref chunks[chunkIndex];
            return ref get_element<T>(page.buffer.Ptr, componentIndex * componentSize);
        }
    }
}
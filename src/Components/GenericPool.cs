﻿using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs {
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct GenericPool : IDisposable {
        public bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (IntPtr)UnsafeBuffer != IntPtr.Zero;
        }

        [NativeDisableUnsafePtrRestriction] internal GenericPoolUnsafe* UnsafeBuffer;
        public int Count => UnsafeBuffer->count;
        
        internal static GenericPool Create<T>(int size, World.WorldUnsafe* world) where T : unmanaged {
            return new GenericPool {
                UnsafeBuffer = GenericPoolUnsafe.CreateBuffer<T>(size, world),
            };
        }

        internal static GenericPool Create(ComponentType type, int size, World.WorldUnsafe* world) {
            return new GenericPool {
                UnsafeBuffer = GenericPoolUnsafe.CreateBuffer(type, size, world)
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
            return ref ((T*)UnsafeBuffer->buffer)[index];
            //return ref *(T*) (impl->buffer + index * impl->elementSize);
        }
        
        public byte* UnsafeGetPtr(int index)
        {
            return UnsafeBuffer->buffer + index * UnsafeBuffer->ComponentType.size;
        }

        public ref T UnsafeGetRef<T>(int index, byte[] buffer) where T : unmanaged
        {
            CheckValid(index);
            return ref UnsafeBuffer->bufferPtr.AsPtr<T>(buffer)[index];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set<T>(int index, in T value) where T : unmanaged
        {
            //CheckValid(index);
            if (!UnsafeBuffer->ComponentType.isTag) {
                if (index < 0) {
                    throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {UnsafeBuffer->capacity}.");
                }
                CheckResize<T>(index);
                ((T*) UnsafeBuffer->buffer)[index] = value;
                //*(T*) (UnsafeBuffer->buffer + index * UnsafeBuffer->elementSize) = value;
            }
            UnsafeBuffer->count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index) {
            UnsafeBuffer->count++;
        }

        public ref T GetSingleton<T>() where T : unmanaged {
            return ref ((T*)UnsafeBuffer->buffer)[0];
        }
        
        public void SetSingleton<T>(in T value) where T : unmanaged{
            ((T*)UnsafeBuffer->buffer)[0] = value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPtr(int index, void* value) {
            if (!UnsafeBuffer->ComponentType.isTag) {
                if (index < 0) {
                    throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {UnsafeBuffer->capacity}.");
                }
                CheckResize(index);
                UnsafeUtility.MemCpy(UnsafeBuffer->buffer + index * UnsafeBuffer->ComponentType.size, value, UnsafeBuffer->ComponentType.size);
            }
            UnsafeBuffer->count++;
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytes(int index, byte[] value) {
            if (!UnsafeBuffer->ComponentType.isTag) {
                if (index < 0) {
                    throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {UnsafeBuffer->capacity}.");
                }
                CheckResize(index);
                fixed (byte* ptr = value) {
                    UnsafeUtility.MemCpy(UnsafeBuffer->buffer + index * UnsafeBuffer->ComponentType.size, ptr, UnsafeBuffer->ComponentType.size);
                }
            }
            UnsafeBuffer->count++;
        }
        //[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytesUnsafe(int index, byte* value, int sizeInBytes) {
            if (!UnsafeBuffer->ComponentType.isTag) {
                if (index < 0) {
                    throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {UnsafeBuffer->capacity}.");
                }
                CheckResize(index);
                UnsafeUtility.MemCpy(UnsafeBuffer->buffer + index * UnsafeBuffer->ComponentType.size, value, sizeInBytes);
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
                ComponentHelpers.Write(UnsafeBuffer->buffer, index, UnsafeBuffer->ComponentType.size, UnsafeBuffer->ComponentType.index, component);
            }
            UnsafeBuffer->count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetObject(int index, IComponent component) {
            if (!UnsafeBuffer->ComponentType.isTag) {
                if (index < 0) {
                    throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {UnsafeBuffer->capacity}.");
                }
                ComponentHelpers.Write(UnsafeBuffer->buffer, index, UnsafeBuffer->ComponentType.size, UnsafeBuffer->ComponentType.index, component);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int index) {
            if (UnsafeBuffer->ComponentType.isDisposable) {
                DisposeComponent(index);
            }
            if (!UnsafeBuffer->ComponentType.isTag)
            {
                ref readonly var type = ref UnsafeBuffer->ComponentType;
                UnsafeUtility.MemCpy(UnsafeBuffer->buffer + index * type.size, type.defaultValue, type.size);
            }
            UnsafeBuffer->count--;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DisposeComponent(int index) {
            var fn = new FunctionPointer<DisposeDelegate>(UnsafeBuffer->ComponentType.disposeFn);
            fn.Invoke(UnsafeBuffer->buffer, index);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Copy(int source, int destination) {
            if (!UnsafeBuffer->ComponentType.isTag) {
                CheckResize(math.max(destination, source));
                if (UnsafeBuffer->ComponentType.isCopyable) {
                    CopyComponent(source, destination);
                }
                else {
                    UnsafeUtility.MemCpy(UnsafeBuffer->buffer + destination * UnsafeBuffer->ComponentType.size, 
                        UnsafeBuffer->buffer + source * UnsafeBuffer->ComponentType.size, 
                        UnsafeBuffer->ComponentType.size);
                }
            }
            UnsafeBuffer->count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyComponent(int from, int to) {
            UnsafeBuffer->ComponentType.CopyFn().Invoke(UnsafeBuffer->buffer, from, to);
            //ComponentHelpers.Copy(UnsafeBuffer->buffer, from, to, UnsafeBuffer->ComponentType.index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            if (index >= UnsafeBuffer->capacity)
            {
                // Calculate new capacity
                int newCapacity = math.max(UnsafeBuffer->capacity * 2, index + 1);

                // Allocate new buffer
                byte* newBuffer = (byte*)UnsafeUtility.MallocTracked(
                    newCapacity * UnsafeBuffer->ComponentType.size,
                    UnsafeBuffer->ComponentType.align,
                    UnsafeBuffer->allocator,
                    0
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
                //Debug.Log($"GenericPool[{ComponentsMap.GetType(impl->componentTypeIndex).Name}] resized on copy");
            }
        }
        public void Dispose() {
            //if (UnsafeBuffer == null) return;
            if(!IsCreated) return;
            //var type = ComponentsMap.GetType(UnsafeBuffer->ComponentType.index).Name;
            //var cType = UnsafeBuffer->ComponentType;
            var allocator = UnsafeBuffer->allocator;
            if (UnsafeBuffer->worldPtr != null)
            {
                UnsafeBuffer->worldPtr->_free(UnsafeBuffer->buffer, UnsafeBuffer->ComponentType.size, UnsafeBuffer->ComponentType.align, UnsafeBuffer->capacity);
                UnsafeBuffer->buffer = null;
                UnsafeBuffer->count = 0;
                UnsafeBuffer->worldPtr->_free(UnsafeBuffer);
            }
            else
            {
                UnsafeUtility.FreeTracked(UnsafeBuffer->buffer, allocator);
                UnsafeBuffer->buffer = null;
                UnsafeBuffer->count = 0;
                UnsafeUtility.FreeTracked(UnsafeBuffer, allocator);
            }

            
            //IsCreated = false;
            //Debug.Log($"Pool {type} disposed. {cType.ToString()} ");
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
    public struct bbool {
        internal byte value;

        public static implicit operator bool(bbool v) {
            return v.value == 1;
        }

        public static explicit operator bbool(bool value) {
            return new bbool {
                value = value ? (byte)1 : (byte)0
            };
        }
    }
}
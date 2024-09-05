using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Wargon.Nukecs {
    public unsafe struct GenericPool : IDisposable {
        public int Count => impl->count;
        [NativeDisableUnsafePtrRestriction] internal Impl* impl;
        public bool IsCreated;
        public static GenericPool Create<T>(int size, Allocator allocator) where T : unmanaged {
            return new GenericPool {
                impl = Impl.CreateImpl<T>(size, allocator),
                IsCreated = true
            };
        }

        public static void Create<T>(int size, Allocator allocator, out GenericPool pool) where T : unmanaged {
            pool = Create<T>(size, allocator);
        }
        public static GenericPool Create(ComponentType type, int size, Allocator allocator) {
            return new GenericPool {
                impl = Impl.CreateImpl(type, size, allocator),
                IsCreated = true
            };
        }

        public static GenericPool* CreatePtr<T>(int size, Allocator allocator) where T : unmanaged {
            var ptr = (GenericPool*) UnsafeUtility.Malloc(sizeof(GenericPool), UnsafeUtility.AlignOf<GenericPool>(),
                allocator);
            *ptr = new GenericPool {
                impl = Impl.CreateImpl<T>(size, allocator),
                IsCreated = true
            };
            return ptr;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct Impl {
            [NativeDisableUnsafePtrRestriction] internal byte* buffer;
            internal int elementSize;
            internal int count;
            internal int capacity;
            internal int componentTypeIndex;
            internal int align;
            internal ComponentType ComponentType;
            internal Allocator allocator;
            
            internal static Impl* CreateImpl<T>(int size, Allocator allocator) where T : unmanaged {
                var ptr = (Impl*) UnsafeUtility.Malloc(sizeof(Impl), UnsafeUtility.AlignOf<Impl>(), allocator);
                *ptr = new Impl {
                    elementSize = sizeof(T),
                    capacity = size,
                    count = 0,
                    allocator = allocator,
                    buffer = (byte*) UnsafeUtility.Malloc(sizeof(T) * size, UnsafeUtility.AlignOf<T>(), allocator),
                    componentTypeIndex = ComponentType<T>.Index,
                    align = UnsafeUtility.AlignOf<T>(),
                    ComponentType = ComponentType<T>.Data
                };

                UnsafeUtility.MemClear(ptr->buffer,size * sizeof(T));
                return ptr;
            }

            internal static Impl* CreateImpl(ComponentType type, int size, Allocator allocator) {
                var ptr = (Impl*) UnsafeUtility.Malloc(sizeof(Impl), UnsafeUtility.AlignOf<Impl>(), allocator);

                *ptr = new Impl {
                    elementSize = type.size,
                    capacity = size,
                    count = 0,
                    allocator = allocator,
                    buffer = (byte*) UnsafeUtility.Malloc(type.size * size, type.align, allocator),
                    componentTypeIndex = type.index,
                    align = type.align,
                    ComponentType = type
                };
                UnsafeUtility.MemClear(ptr->buffer, type.size * size);
                return ptr;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set<T>(int index, in T value) where T : unmanaged {
            if (impl->elementSize != 1) {
                if (index < 0) {
                    throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
                }
                CheckResize<T>(index);
                *(T*) (impl->buffer + index * impl->elementSize) = value;
            }
            impl->count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(int index) {
            impl->count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetRef<T>(int index) where T : unmanaged {
            if (index < 0 || index >= impl->capacity) {
                throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool[{ComponentsMap.GetType(impl->componentTypeIndex).Name}] with capacity {impl->capacity}.");
            }
            return ref ((T*)impl->buffer)[index];
            //return ref *(T*) (impl->buffer + index * impl->elementSize);
        }

        public ref T GetShared<T>() where T : unmanaged{
            return ref ((T*)impl->buffer)[0];
        }
        
        public void SetShared<T>(in T value) where T : unmanaged{
            ((T*)impl->buffer)[0] = value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPtr(int index, void* value) {
            if (impl->elementSize != 1) {
                if (index < 0) {
                    throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
                }
                CheckResize(index);
                UnsafeUtility.MemCpy(impl->buffer + index * impl->elementSize, value, impl->elementSize);
            }
            impl->count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytes(int index, byte[] value) {
            if (impl->elementSize != 1) {
                if (index < 0) {
                    throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
                }
                CheckResize(index);
                fixed (byte* ptr = value) {
                    UnsafeUtility.MemCpy(impl->buffer + index * impl->elementSize, ptr, impl->elementSize);
                }
            }
            impl->count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytesUnsafe(int index, byte* value, int sizeInBytes) {
            if (impl->elementSize != 1) {
                if (index < 0) {
                    throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
                }
                CheckResize(index);
                UnsafeUtility.MemCpy(impl->buffer + index * impl->elementSize, value, sizeInBytes);
            }
            impl->count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetObject(int index, IComponent component) {
            if (impl->elementSize != 1) {
                if (index < 0) {
                    throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
                }
                CheckResize(index);
                ComponentHelpers.Write(impl->buffer, index, impl->elementSize, impl->componentTypeIndex, component);
            }
            impl->count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int index) {
            if (impl->ComponentType.isDisposable) {
                DisposeComponent(index);
            }

            if (!impl->ComponentType.isTag) {
                SetPtr(index, impl->ComponentType.defaultValue);
            }
            impl->count--;
        }
        [BurstDiscard][MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DisposeComponent(int index) {
            ComponentHelpers.Dispose(impl->buffer, index, impl->componentTypeIndex);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Copy(int source, int destination) {
            if (impl->elementSize != 1) {
                CheckResize(math.max(destination, source));
                if (impl->ComponentType.isCopyable) {
                    CopyComponent(source, destination);
                }
                else {
                    UnsafeUtility.MemCpy(impl->buffer + destination * impl->elementSize, impl->buffer + source * impl->elementSize, impl->elementSize);
                }
            }
            impl->count++;
        }
        [BurstDiscard][MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyComponent(int from, int to) {
            ComponentHelpers.Copy(impl->buffer, from, to, impl->componentTypeIndex);
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckResize<T>(int index) where T : unmanaged
        {
            if (index >= impl->capacity)
            {
                // Calculate new capacity
                int newCapacity = math.max(impl->capacity * 2, index + 1);

                // Allocate new buffer
                byte* newBuffer = (byte*)UnsafeUtility.Malloc(
                    newCapacity * impl->elementSize,
                    UnsafeUtility.AlignOf<T>(),
                    impl->allocator
                );

                if (newBuffer == null)
                {
                    throw new OutOfMemoryException("Failed to allocate memory for resizing.");
                }

                UnsafeUtility.MemClear(newBuffer, newCapacity * impl->elementSize);
                // Copy old data to new buffer
                UnsafeUtility.MemCpy(newBuffer, impl->buffer, impl->capacity * impl->elementSize);

                // Free old buffer
                UnsafeUtility.Free(impl->buffer, impl->allocator);

                // Update impl
                impl->buffer = newBuffer;
                impl->capacity = newCapacity;
                //Debug.Log($"GenericPool[{ComponentsMap.GetType(impl->componentTypeIndex).Name}] resized on set");
            }
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckResize(int index)
        {
            if (index >= impl->capacity)
            {
                // Calculate new capacity
                int newCapacity = math.max(impl->capacity * 2, index + 1);

                // Allocate new buffer
                byte* newBuffer = (byte*)UnsafeUtility.Malloc(
                    newCapacity * impl->elementSize,
                    impl->align,
                    impl->allocator
                );

                if (newBuffer == null)
                {
                    throw new OutOfMemoryException("Failed to allocate memory for resizing.");
                }

                UnsafeUtility.MemClear(newBuffer, newCapacity * impl->elementSize);
                // Copy old data to new buffer
                UnsafeUtility.MemCpy(newBuffer, impl->buffer, impl->capacity * impl->elementSize);

                // Free old buffer
                UnsafeUtility.Free(impl->buffer, impl->allocator);

                // Update impl
                impl->buffer = newBuffer;
                impl->capacity = newCapacity;
                //Debug.Log($"GenericPool[{ComponentsMap.GetType(impl->componentTypeIndex).Name}] resized on copy");
            }
        }
        public void Dispose() {
            if (impl == null) return;
            var allocator = impl->allocator;
            UnsafeUtility.Free(impl->buffer, allocator);
            impl->buffer = null;
            impl->count = 0;
            UnsafeUtility.Free(impl, allocator);
            IsCreated = false;
        }

        public ComponentPool<T> AsComponentPool<T>() where T : unmanaged {
            return new ComponentPool<T>(impl->buffer);
        }
    }
    
    public readonly unsafe struct ComponentPool<T> where T : unmanaged {
        [NativeDisableUnsafePtrRestriction]
        private readonly T* _buffer;

        internal ComponentPool(void* buffer) {
            _buffer = (T*) buffer;
        }
        public ref T Get(int index) {
            return ref _buffer[index];
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
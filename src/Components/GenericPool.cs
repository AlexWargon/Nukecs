using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Wargon.Nukecs {
    public unsafe struct GenericPool : IDisposable {
        public int Count => UnsafeBuffer->count;
        [NativeDisableUnsafePtrRestriction] internal GenericPoolUnsafe* UnsafeBuffer;
        public bool IsCreated;
        public static GenericPool Create<T>(int size, Allocator allocator) where T : unmanaged {
            return new GenericPool {
                UnsafeBuffer = GenericPoolUnsafe.CreateImpl<T>(size, allocator),
                IsCreated = true
            };
        }

        public static void Create<T>(int size, Allocator allocator, out GenericPool pool) where T : unmanaged {
            pool = Create<T>(size, allocator);
        }
        public static GenericPool Create(ComponentType type, int size, Allocator allocator) {
            return new GenericPool {
                UnsafeBuffer = GenericPoolUnsafe.CreateImpl(type, size, allocator),
                IsCreated = true
            };
        }

        public static GenericPool* CreatePtr<T>(int size, Allocator allocator) where T : unmanaged {
            var ptr = Unsafe.MallocTracked<GenericPool>(
                allocator);
            *ptr = new GenericPool {
                UnsafeBuffer = GenericPoolUnsafe.CreateImpl<T>(size, allocator),
                IsCreated = true
            };
            return ptr;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct GenericPoolUnsafe {
            [NativeDisableUnsafePtrRestriction] internal byte* buffer;
            internal int elementSize;
            internal int count;
            internal int capacity;
            internal int componentTypeIndex;
            internal int align;
            internal ComponentType ComponentType;
            internal Allocator allocator;
            
            internal static GenericPoolUnsafe* CreateImpl<T>(int size, Allocator allocator) where T : unmanaged {
                var ptr = Unsafe.MallocTracked<GenericPoolUnsafe>(allocator);
                *ptr = new GenericPoolUnsafe {
                    elementSize = sizeof(T),
                    capacity = size,
                    count = 0,
                    allocator = allocator,
                    buffer = (byte*)Unsafe.MallocTracked<T>(size, allocator),
                    componentTypeIndex = ComponentType<T>.Index,
                    align = UnsafeUtility.AlignOf<T>(),
                    ComponentType = ComponentType<T>.Data
                };

                UnsafeUtility.MemClear(ptr->buffer,size * sizeof(T));
                return ptr;
            }

            internal static GenericPoolUnsafe* CreateImpl(ComponentType type, int size, Allocator allocator) {
                var ptr = Unsafe.MallocTracked<GenericPoolUnsafe>(allocator);

                *ptr = new GenericPoolUnsafe {
                    elementSize = type.size,
                    capacity = size,
                    count = 0,
                    allocator = allocator,
                    buffer = (byte*)UnsafeUtility.MallocTracked(type.size * size, type.align, allocator, 0),
                    componentTypeIndex = type.index,
                    align = type.align,
                    ComponentType = type
                };
                UnsafeUtility.MemClear(ptr->buffer, type.size * size);
                return ptr;
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly ref T GetRef<T>(int index) where T : unmanaged {
                return ref ((T*)buffer)[index];
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ref T GetRef<T>(int index) where T : unmanaged {
            if (index < 0 || index >= UnsafeBuffer->capacity) {
                throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool[{ComponentsMap.GetType(UnsafeBuffer->componentTypeIndex).Name}] with capacity {UnsafeBuffer->capacity}.");
            }
            return ref ((T*)UnsafeBuffer->buffer)[index];
            //return ref *(T*) (impl->buffer + index * impl->elementSize);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set<T>(int index, in T value) where T : unmanaged {
            if (UnsafeBuffer->elementSize != 1) {
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

        public ref T GetShared<T>() where T : unmanaged{
            return ref ((T*)UnsafeBuffer->buffer)[0];
        }
        
        public void SetShared<T>(in T value) where T : unmanaged{
            ((T*)UnsafeBuffer->buffer)[0] = value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPtr(int index, void* value) {
            if (UnsafeBuffer->elementSize != 1) {
                if (index < 0) {
                    throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {UnsafeBuffer->capacity}.");
                }
                CheckResize(index);
                UnsafeUtility.MemCpy(UnsafeBuffer->buffer + index * UnsafeBuffer->elementSize, value, UnsafeBuffer->elementSize);
            }
            UnsafeBuffer->count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytes(int index, byte[] value) {
            if (UnsafeBuffer->elementSize != 1) {
                if (index < 0) {
                    throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {UnsafeBuffer->capacity}.");
                }
                CheckResize(index);
                fixed (byte* ptr = value) {
                    UnsafeUtility.MemCpy(UnsafeBuffer->buffer + index * UnsafeBuffer->elementSize, ptr, UnsafeBuffer->elementSize);
                }
            }
            UnsafeBuffer->count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBytesUnsafe(int index, byte* value, int sizeInBytes) {
            if (UnsafeBuffer->elementSize != 1) {
                if (index < 0) {
                    throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {UnsafeBuffer->capacity}.");
                }
                CheckResize(index);
                UnsafeUtility.MemCpy(UnsafeBuffer->buffer + index * UnsafeBuffer->elementSize, value, sizeInBytes);
            }
            UnsafeBuffer->count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetObject(int index, IComponent component) {
            if (UnsafeBuffer->elementSize != 1) {
                if (index < 0) {
                    throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {UnsafeBuffer->capacity}.");
                }
                CheckResize(index);
                ComponentHelpers.Write(UnsafeBuffer->buffer, index, UnsafeBuffer->elementSize, UnsafeBuffer->componentTypeIndex, component);
            }
            UnsafeBuffer->count++;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int index) {
            if (UnsafeBuffer->ComponentType.isDisposable) {
                DisposeComponent(index);
            }

            if (!UnsafeBuffer->ComponentType.isTag) {
                SetPtr(index, UnsafeBuffer->ComponentType.defaultValue);
            }
            UnsafeBuffer->count--;
        }
        [BurstDiscard][MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DisposeComponent(int index) {
            ComponentHelpers.Dispose(ref UnsafeBuffer->buffer, index, UnsafeBuffer->componentTypeIndex);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Copy(int source, int destination) {
            if (UnsafeBuffer->elementSize != 1) {
                CheckResize(math.max(destination, source));
                if (UnsafeBuffer->ComponentType.isCopyable) {
                    CopyComponent(source, destination);
                }
                else {
                    UnsafeUtility.MemCpy(UnsafeBuffer->buffer + destination * UnsafeBuffer->elementSize, UnsafeBuffer->buffer + source * UnsafeBuffer->elementSize, UnsafeBuffer->elementSize);
                }
            }
            UnsafeBuffer->count++;
        }
        [BurstDiscard][MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CopyComponent(int from, int to) {
            ComponentHelpers.Copy(UnsafeBuffer->buffer, from, to, UnsafeBuffer->componentTypeIndex);
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckResize<T>(int index) where T : unmanaged
        {
            if (index >= UnsafeBuffer->capacity)
            {
                // Calculate new capacity
                int newCapacity = math.max(UnsafeBuffer->capacity * 2, index + 1);

                // Allocate new buffer
                byte* newBuffer = (byte*)UnsafeUtility.MallocTracked(
                    newCapacity * UnsafeBuffer->elementSize,
                    UnsafeUtility.AlignOf<T>(),
                    UnsafeBuffer->allocator, 0
                );

                if (newBuffer == null)
                {
                    throw new OutOfMemoryException("Failed to allocate memory for resizing.");
                }

                UnsafeUtility.MemClear(newBuffer, newCapacity * UnsafeBuffer->elementSize);
                // Copy old data to new buffer
                UnsafeUtility.MemCpy(newBuffer, UnsafeBuffer->buffer, UnsafeBuffer->capacity * UnsafeBuffer->elementSize);

                // Free old buffer
                UnsafeUtility.FreeTracked(UnsafeBuffer->buffer, UnsafeBuffer->allocator);

                // Update impl
                UnsafeBuffer->buffer = newBuffer;
                UnsafeBuffer->capacity = newCapacity;
                //Debug.Log($"GenericPool[{ComponentsMap.GetType(impl->componentTypeIndex).Name}] resized on set");
            }
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckResize(int index)
        {
            if (index >= UnsafeBuffer->capacity)
            {
                // Calculate new capacity
                int newCapacity = math.max(UnsafeBuffer->capacity * 2, index + 1);

                // Allocate new buffer
                byte* newBuffer = (byte*)UnsafeUtility.MallocTracked(
                    newCapacity * UnsafeBuffer->elementSize,
                    UnsafeBuffer->align,
                    UnsafeBuffer->allocator,
                    0
                );

                if (newBuffer == null)
                {
                    throw new OutOfMemoryException("Failed to allocate memory for resizing.");
                }

                UnsafeUtility.MemClear(newBuffer, newCapacity * UnsafeBuffer->elementSize);
                // Copy old data to new buffer
                UnsafeUtility.MemCpy(newBuffer, UnsafeBuffer->buffer, UnsafeBuffer->capacity * UnsafeBuffer->elementSize);

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
            Unsafe.FreeTracked(UnsafeBuffer->buffer, allocator);
            UnsafeBuffer->buffer = null;
            UnsafeBuffer->count = 0;
            Unsafe.FreeTracked(UnsafeBuffer, allocator);
            IsCreated = false;
            //Debug.Log($"Pool {type} disposed. {cType.ToString()} ");
        }

        public ComponentPool<T> AsComponentPool<T>() where T : unmanaged {
            return new ComponentPool<T>(UnsafeBuffer->buffer);
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
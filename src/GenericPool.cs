﻿using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs {
    public unsafe struct GenericPool : IDisposable {
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
            internal Allocator allocator;

            internal static Impl* CreateImpl<T>(int size, Allocator allocator) where T : unmanaged {
                var ptr = (Impl*) UnsafeUtility.Malloc(sizeof(Impl), UnsafeUtility.AlignOf<Impl>(), allocator);
                *ptr = new Impl {
                    elementSize = sizeof(T),
                    capacity = size,
                    count = 0,
                    allocator = allocator,
                    buffer = (byte*) UnsafeUtility.Malloc(sizeof(T) * size, UnsafeUtility.AlignOf<T>(), allocator),
                    componentTypeIndex = ComponentType<T>.Index
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
                    componentTypeIndex = type.index
                };
                UnsafeUtility.MemClear(ptr->buffer, type.size * size);
                return ptr;
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set<T>(int index, in T value) where T : unmanaged {
            if (index < 0 || index >= impl->capacity) {
                throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
            }
            *(T*) (impl->buffer + index * impl->elementSize) = value;
        }

        public ref T GetRef<T>(int index) where T : unmanaged {
            if (index < 0 || index >= impl->capacity) {
                throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
            }
            //return ref ((T*)impl->buffer)[index];
            return ref *(T*) (impl->buffer + index * impl->elementSize);
        }

        public void SetPtr(int index, void* value) {
            if (index < 0 || index >= impl->capacity) {
                throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
            }
            UnsafeUtility.MemCpy(impl->buffer + index * impl->elementSize, value, impl->elementSize);
        }

        public void WriteBytes(int index, byte[] value) {
            if (index < 0 || index >= impl->capacity) {
                throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
            }
            fixed (byte* ptr = value) {
                UnsafeUtility.MemCpy(impl->buffer + index * impl->elementSize, ptr, impl->elementSize);
            }
        }
        public void WriteBytesUnsafe(int index, byte* value, int sizeInBytes) {
            if (index < 0 || index >= impl->capacity) {
                throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
            }
            UnsafeUtility.MemCpy(impl->buffer + index * impl->elementSize, value, sizeInBytes);
        }

        public void SetObject(int index, IComponent component) {
            if (index < 0 || index >= impl->capacity) {
                throw new IndexOutOfRangeException($"Index {index} is out of range for GenericPool with capacity {impl->capacity}.");
            }
            BoxedWriters.Write(impl->buffer, index, impl->elementSize, impl->componentTypeIndex, component);
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
    }
}
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Wargon.Nukecs.Transforms;

namespace Wargon.Nukecs.Tests
{
    public struct SpriteChunk {
        [NativeDisableUnsafePtrRestriction] 
        internal unsafe SpriteRenderData* renderDataChunk;
        [NativeDisableUnsafePtrRestriction] 
        internal unsafe Transform* transforms;
        internal UnsafeList<int> entityToIndex;
        internal UnsafeList<int> indexToEntity;
        internal volatile int count;
        internal int capacity;
        internal int lastRemoved;
        public static unsafe ptr<SpriteChunk> Create(int size, ref UnityAllocatorWrapper allocator)
        {
            var ptr = allocator.Allocator.AllocatePtr<SpriteChunk>();
            ptr.Ref = new SpriteChunk {
                renderDataChunk = Unsafe.MallocTracked<SpriteRenderData>(size, Allocator.Persistent),
                transforms = Unsafe.MallocTracked<Transform>(size, Allocator.Persistent),
                entityToIndex = UnsafeHelp.UnsafeListWithMaximumLenght<int>(size, Allocator.Persistent, NativeArrayOptions.ClearMemory),
                indexToEntity = UnsafeHelp.UnsafeListWithMaximumLenght<int>(size, Allocator.Persistent, NativeArrayOptions.ClearMemory),
                count = 0,
                capacity = size,
                lastRemoved = 0
            };
            
            return ptr;
        }
        public int AddInitial(int entity) {
            var index = count;
            if (entity >= entityToIndex.m_length) {
                var newCapacity = entity * 2;
                entityToIndex.Resize(newCapacity, NativeArrayOptions.ClearMemory);
                entityToIndex.m_length = entityToIndex.m_capacity;
                indexToEntity.Resize(newCapacity, NativeArrayOptions.ClearMemory);
                indexToEntity.m_length = indexToEntity.m_capacity;
                unsafe {
                    UnsafeHelp.Resize(capacity, newCapacity, ref transforms, Allocator.Persistent);
                    UnsafeHelp.Resize(capacity, newCapacity, ref renderDataChunk, Allocator.Persistent);
                }
                capacity = newCapacity;
            }
            indexToEntity[count] = entity;
            entityToIndex[entity] = count;
            //count++;
            Interlocked.Increment(ref count);
            return index;
        }
        public int Add(in Entity entity) {
            var index = count;
            if (entity.id >= entityToIndex.m_length) {
                var newCapacity = entity.id * 2;
                entityToIndex.Resize(newCapacity, NativeArrayOptions.ClearMemory);
                entityToIndex.m_length = entityToIndex.m_capacity;
                indexToEntity.Resize(newCapacity, NativeArrayOptions.ClearMemory);
                indexToEntity.m_length = indexToEntity.m_capacity;
                unsafe {
                    UnsafeHelp.Resize(capacity, newCapacity, ref transforms, Allocator.Persistent);
                    UnsafeHelp.Resize(capacity, newCapacity, ref renderDataChunk, Allocator.Persistent);
                }
                capacity = newCapacity;
            }
            indexToEntity[count] = entity.id;
            entityToIndex[entity.id] = count;
            Interlocked.Increment(ref count);
            return index; 
        }


        public unsafe void AddToFill(in Entity entity, in Transform transform, in SpriteRenderData data) {
            var index = count;
            Interlocked.Increment(ref count);
            if (index >= capacity) {
                var newCapacity = capacity * 2;
                UnsafeHelp.Resize(capacity, newCapacity, ref transforms, Allocator.Persistent);
                UnsafeHelp.Resize(capacity, newCapacity, ref renderDataChunk, Allocator.Persistent);
                capacity = newCapacity;
            }
            transforms[index] = transform;
            renderDataChunk[index] = data;
        }
        public unsafe void Remove(in Entity entity) {
            if(count <= 0) return;
            var lastIndex = count - 1;
            var lastEntityID = indexToEntity[lastIndex];
            var entityID = entity.id;
            if (lastEntityID != entityID && count > 0) {
                var entityIndex = entityToIndex[entityID];
                entityToIndex[lastEntityID] = entityIndex;
                indexToEntity[entityIndex] = lastEntityID;
                renderDataChunk[lastIndex] = renderDataChunk[entityIndex];
                transforms[lastIndex] = transforms[entityIndex];
            }

            Interlocked.Decrement(ref count);
            //count--;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void UpdateData(int entity, in SpriteRenderData data, in Transform transform) {
            var index = entityToIndex[entity];
            renderDataChunk[index] = data;
            transforms[index] = transform;
        }

        public void Clear() {
            indexToEntity.Clear();
            indexToEntity.m_length = indexToEntity.m_capacity;
            entityToIndex.Clear();
            entityToIndex.m_length = entityToIndex.m_capacity;
            count = 0;
        }
        public static unsafe void Destroy(ref ptr<SpriteChunk> chunk) {
            Unsafe.FreeTracked(chunk.Ref.transforms, Allocator.Persistent);
            Unsafe.FreeTracked(chunk.Ref.renderDataChunk, Allocator.Persistent);
            chunk.Ref.indexToEntity.Dispose();
            chunk.Ref.entityToIndex.Dispose();
        }
    }
}
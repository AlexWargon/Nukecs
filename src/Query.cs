using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs {
    public unsafe struct Query : IDisposable {
        
        [NativeDisableUnsafePtrRestriction] internal readonly QueryUnsafe* impl;
        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => impl->count;
        }

        internal struct QueryUnsafe {
            internal DynamicBitmask with;
            internal DynamicBitmask none;
            internal UnsafeList<int> entities;
            internal UnsafeList<int> entitiesMap;
            internal int count;
            [NativeDisableUnsafePtrRestriction] internal readonly World.WorldUnsafe* world;
            [NativeDisableUnsafePtrRestriction] internal readonly QueryUnsafe* self;

            internal static void Free(QueryUnsafe* queryImpl) {
                queryImpl->Free();
                var allocator = queryImpl->world->allocator;
                UnsafeUtility.Free(queryImpl, allocator);
            }

            private void Free() {
                with.Dispose();
                none.Dispose();
                entities.Dispose();
                entitiesMap.Dispose();
            }

            internal static QueryUnsafe* Create(World.WorldUnsafe* world) {
                var ptr = Unsafe.Malloc<QueryUnsafe>(world->allocator);
                *ptr = new QueryUnsafe(world, ptr);
                return ptr;
            }

            internal QueryUnsafe(World.WorldUnsafe* world, QueryUnsafe* self) {
                this.world = world;
                this.with = DynamicBitmask.CreateForComponents();
                this.none = DynamicBitmask.CreateForComponents();
                this.count = 0;
                this.entities = UnsafeHelp.UnsafeListWithMaximumLenght<int>(world->config.StartEntitiesAmount,
                    world->allocator, NativeArrayOptions.ClearMemory);
                this.entitiesMap = UnsafeHelp.UnsafeListWithMaximumLenght<int>(world->config.StartEntitiesAmount,
                    world->allocator, NativeArrayOptions.ClearMemory);
                this.self = self;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ref Entity GetEntity(int index) {
                return ref world->entities.ElementAt(entities[index]);
            }

            internal bool Has(int entity) {
                if (entitiesMap.Length <= entity) return false;
                return entitiesMap[entity] > 0;
            }

            internal void Add(int entity) {
                if(Has(entity)) return;
                if (entities.Length - 1 <= count) {
                    entities.Resize(count * 2);
                }

                if (entitiesMap.Length - 1 <= entity) {
                    entitiesMap.Resize(count * 2);
                }

                entities[count++] = entity;
                entitiesMap[entity] = count;
            }

            internal void Remove(int entity) {
                if (!Has(entity)) return;
                var index = entitiesMap[entity] - 1;
                entitiesMap[entity] = 0;
                count--;
                if (count > index) {
                    entities[index] = entities[count];
                    entitiesMap[entities[index]] = index + 1;
                }
            }

            public QueryUnsafe* With(int type) {
                with.Add(type);
                return self;
            }

            public bool HasWith(int type) {
                return with.Has(type);
            }

            public bool HasNone(int type) {
                return none.Has(type);
            }

            public QueryUnsafe* None(int type) {
                none.Add(type);
                return self;
            }

            public override string ToString() {
                var sb = new StringBuilder();
                sb.Append($"Query");
                foreach (var typesIndex in ComponentsMap.TypesIndexes) {
                    if (HasWith(typesIndex)) {
                        sb.Append($".With<{ComponentsMap.GetType(typesIndex).Name}>()");
                    }

                    if (HasNone(typesIndex)) {
                        sb.Append($".None<{ComponentsMap.GetType(typesIndex).Name}>()");
                    }
                }

                sb.Append($".Count = {count}");
                return sb.ToString();
            }
        }


        internal Query(World.WorldUnsafe* world) {
            impl = QueryUnsafe.Create(world);
        }

        internal Query(QueryUnsafe* impl) {
            this.impl = impl;
        }

        public Query With<T>() where T : unmanaged {
            impl->With(ComponentType<T>.Index);
            return this;
        }

        public Query None<T>() where T : unmanaged {
            impl->None(ComponentType<T>.Index);
            return this;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity GetEntity(int index) {
            return ref impl->GetEntity(index);
        }

        public void Dispose() {
            var allocator = impl->world->allocator;
            UnsafeUtility.Free(impl, allocator);
        }

        public override string ToString() {
            return impl->ToString();
        }

        public QueryEnumerator GetEnumerator() {
            return new QueryEnumerator(this.impl);
        }
        public NativeArray<T> ToComponentDataArray<T>(AllocatorManager.AllocatorHandle allocator)
            where T : unmanaged, IComponent
        {
            // CalculateEntityCount() syncs any jobs that could affect the filtering results for this query
            //int entityCount = impl->count;
            // We also need to complete any jobs writing to the component we're gathering.
            //var typeIndex = ComponentType<T>.Index;
            //_Access->DependencyManager->CompleteWriteDependency(typeIndex);
            
// #if ENABLE_UNITY_COLLECTIONS_CHECKS
//             var componentType = new ComponentTypeHandle<T>(SafetyHandles->GetSafetyHandleForComponentTypeHandle(TypeManager.GetTypeIndex<T>(), true), true, _Access->EntityComponentStore->GlobalSystemVersion);
//             AtomicSafetyHandle.CheckReadAndThrow(componentType.m_Safety);
// #else
            //var componentType = new ComponentTypeHandle<T>(true, _Access->EntityComponentStore->GlobalSystemVersion);
//#endif

// #if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
//             int indexInEntityQuery = GetIndexInEntityQuery(typeIndex);
//             if (indexInEntityQuery == -1)
//                 throw new InvalidOperationException($"Trying ToComponentDataArray of {TypeManager.GetType(typeIndex)} but the required component type was not declared in the EntityQuery.");
// #endif

            
            //return ChunkIterationUtility.CreateComponentDataArray(allocator, ref componentType, entityCount, outer);
            return new NativeArray<T>(impl->count, allocator.ToAllocator, NativeArrayOptions.UninitializedMemory);
        }

    }

    public unsafe ref struct QueryEnumerator {
        private int _lastIndex;
        private readonly Query.QueryUnsafe* _query;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal QueryEnumerator(Query.QueryUnsafe* queryUnsafe) {
            _query = queryUnsafe;
            _lastIndex = -1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() {
            _lastIndex++;
            return _query->count > _lastIndex;
        }

        public void Reset() {
            _lastIndex = -1;
        }

        public ref Entity Current {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _query->GetEntity(_lastIndex);
        }
    }
    public unsafe struct Bitmask1024 {
        internal const int BitsPerElement = 64;
        internal const int MaxBits = 1024;
        public const int ArraySize = MaxBits / BitsPerElement;

        internal fixed ulong _bitmaskArray[ArraySize];
        internal int count;

        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return count; }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(int position) {
            if (position < 0 || position >= MaxBits) {
                throw new ArgumentOutOfRangeException(nameof(position),
                    $"Position must be between 0 and {MaxBits - 1}.");
                return false;
            }

            int index = position / BitsPerElement;
            int bitPosition = position % BitsPerElement;

            if (!Has(position)) {
                _bitmaskArray[index] |= 1UL << bitPosition;
                count++;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(int position) {
            if (position >= MaxBits) {
                return false;
            }

            var index = position / BitsPerElement;
            var bitMask = 1UL << (position % BitsPerElement);

            return (_bitmaskArray[index] & bitMask) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int position) {
            if (position < 0 || position >= MaxBits) {
                throw new ArgumentOutOfRangeException(nameof(position),
                    $"Position must be between 0 and {MaxBits - 1}.");
            }

            int index = position / BitsPerElement;
            int bitPosition = position % BitsPerElement;

            if (Has(position)) {
                _bitmaskArray[index] &= ~(1UL << bitPosition);
                count--;
            }
        }

        public override string ToString() {
            char[] bitChars = new char[ArraySize * BitsPerElement];
            for (int i = 0; i < ArraySize; i++) {
                string bits = Convert.ToString((long) _bitmaskArray[i], 2).PadLeft(BitsPerElement, '0');
                for (int j = 0; j < BitsPerElement; j++) {
                    bitChars[i * BitsPerElement + j] = bits[j];
                }
            }

            Array.Reverse(bitChars);
            return new string(bitChars);
        }
    }

    public static class Bitmask1024Ext {
        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool Has2(ref this Bitmask1024 bitmask1024, int position) {
            var index = position / Bitmask1024.BitsPerElement;
            var bitPosition = position % Bitmask1024.BitsPerElement;
            return (bitmask1024._bitmaskArray[index] & (1UL << bitPosition)) != 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct BitmaskChunk {
        private const int BitsPerElement = 64;
        private const int ArraySize = 1024 / BitsPerElement;
        internal fixed ulong bitmaskArray[ArraySize];

        public static int SizeInBytes() {
            return sizeof(ulong) * ArraySize;
        }

        public void Add(int position) {
            int index = position / BitsPerElement;
            int bitPosition = position % BitsPerElement;
            bitmaskArray[index] |= 1UL << bitPosition;
        }

        public bool Has(int position) {
            int index = position / BitsPerElement;
            int bitPosition = position % BitsPerElement;
            return (bitmaskArray[index] & (1UL << bitPosition)) != 0;
        }

        public void Remove(int position) {
            int index = position / BitsPerElement;
            int bitPosition = position % BitsPerElement;
            bitmaskArray[index] &= ~(1UL << bitPosition);
        }
    }

    public struct Bitmask64 {
        private ulong bitmask;
        private int count;


        // Property to get the count of set bits
        public int Count {
            get { return count; }
        }

        // Method to add an element (set a specific bit)
        public bool Add(int position) {
            if (position < 0 || position >= 64) {
                //throw new ArgumentOutOfRangeException(nameof(position), "Position must be between 0 and 63.");
                return false;
            }

            if (!Has(position)) {
                bitmask |= 1UL << position;
                count++;
                return true;
            }

            return false;
        }

        // Method to check if an element is present (a specific bit is set)
        public bool Has(int position) {
            if (position < 0 || position >= 64) {
                throw new ArgumentOutOfRangeException(nameof(position), "Position must be between 0 and 63.");
            }

            return (bitmask & (1UL << position)) != 0;
        }

        // Method to clear an element (unset a specific bit)
        public void Remove(int position) {
            if (position < 0 || position >= 64) {
                throw new ArgumentOutOfRangeException(nameof(position), "Position must be between 0 and 63.");
            }

            if (Has(position)) {
                bitmask &= ~(1UL << position);
                count--;
            }
        }

        // Override ToString() to display the bitmask in binary form
        public override string ToString() {
            return Convert.ToString((long) bitmask, 2).PadLeft(64, '0');
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DynamicBitmask {
        private const int BitsPerUlong = 64;
        [NativeDisableUnsafePtrRestriction] private ulong* bitmaskArray;
        private int count;
        private int maxBits;
        private int arraySize;
        
        
        public static DynamicBitmask CreateForComponents() {
            return new DynamicBitmask(ComponentAmount.Value.Data);
        }

        public DynamicBitmask(int maxBits) {
            if (maxBits <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBits), "maxBits in DynamicBitmask must be greater than zero.");

            this.maxBits = maxBits;
            arraySize = (maxBits + BitsPerUlong - 1) / BitsPerUlong; // Calculate the number of ulong elements needed
            bitmaskArray = (ulong*) UnsafeUtility.Malloc(arraySize * sizeof(ulong), UnsafeUtility.AlignOf<ulong>(),
                Allocator.Persistent);
            count = 0;

            // Clear the allocated memory
            ClearBitmask();
        }

        private void ClearBitmask() {
            for (int i = 0; i < arraySize; i++) {
                bitmaskArray[i] = 0;
            }
        }

        // Property to get the count of set bits
        public int Count => count;

        // Method to add an element (set a specific bit)
        public void Add(int position) {
            if (position < 0 || position >= maxBits) {
                throw new ArgumentOutOfRangeException(nameof(position),
                    $"Position must be between 0 and {maxBits - 1}.");
            }

            int index = position / BitsPerUlong;
            int bitPosition = position % BitsPerUlong;

            if (!Has(position)) {
                bitmaskArray[index] |= 1UL << bitPosition;
                count++;
            }
        }

        // Method to check if an element is present (a specific bit is set)
        public bool Has(int position) {
            if (position < 0 || position >= maxBits) {
                throw new ArgumentOutOfRangeException(nameof(position),
                    $"Position must be between 0 and {maxBits - 1}. Position = {position}");
                return false;
            }

            int index = position / BitsPerUlong;
            int bitPosition = position % BitsPerUlong;

            return (bitmaskArray[index] & (1UL << bitPosition)) != 0;
        }

        // Method to clear an element (unset a specific bit)
        public void Remove(int position) {
            if (position < 0 || position >= maxBits) {
                throw new ArgumentOutOfRangeException(nameof(position),
                    $"Position must be between 0 and {maxBits - 1}.");
            }

            int index = position / BitsPerUlong;
            int bitPosition = position % BitsPerUlong;

            if (Has(position)) {
                bitmaskArray[index] &= ~(1UL << bitPosition);
                count--;
            }
        }

        // Override ToString() to display the bitmask in binary form
        public override string ToString() {
            StringBuilder sb = new StringBuilder();
            for (int i = arraySize - 1; i >= 0; i--) {
                sb.Append(Convert.ToString((long) bitmaskArray[i], 2).PadLeft(BitsPerUlong, '0'));
            }

            return sb.ToString();
        }

        // Copy method to create a deep copy of the DynamicBitmask
        public DynamicBitmask Copy() {
            var copy = new DynamicBitmask(maxBits);
            var byteLength = arraySize * sizeof(ulong);
            UnsafeUtility.MemCpy(bitmaskArray, copy.bitmaskArray, byteLength);
            copy.count = count;
            return copy;
        }

        public DynamicBitmask CopyPlusOne() {
            var copy = new DynamicBitmask(maxBits + 1);
            var byteLength = arraySize * sizeof(ulong);
            UnsafeUtility.MemCpy(bitmaskArray, copy.bitmaskArray, byteLength);
            copy.count = count;
            return copy;
        }

        // Dispose method to release allocated memory
        public void Dispose() {
            UnsafeUtility.Free(bitmaskArray, Allocator.Persistent);
            bitmaskArray = null;
        }
    }
}
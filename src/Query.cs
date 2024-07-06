using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Burst;

namespace Wargon.Nukecs {
    public unsafe struct Query {
        internal unsafe struct QueryImpl {
            internal Bitmask1024 with;
            internal Bitmask1024 none;
            internal Unity.Collections.LowLevel.Unsafe.UnsafeList<int> entities;
            internal Unity.Collections.LowLevel.Unsafe.UnsafeList<int> entitiesMap;
            internal int count;
            internal readonly World.WorldImpl* world;
            internal readonly QueryImpl* self;

            internal static QueryImpl* Create(World.WorldImpl* world) {
                var ptr = Unsafe.Malloc<QueryImpl>(world->allocator);
                *ptr = new QueryImpl(world, ptr);
                return ptr;
            }

            internal QueryImpl(World.WorldImpl* world, QueryImpl* self) {
                this.world = world;
                this.with = default;
                this.none = default;
                this.count = default;
                this.entities = new Unity.Collections.LowLevel.Unsafe.UnsafeList<int>(world->config.StartEntitiesAmount, world->allocator);
                this.entitiesMap = new Unity.Collections.LowLevel.Unsafe.UnsafeList<int>(world->config.StartEntitiesAmount, world->allocator);
                this.self = self;
            }
            public ref Entity GetEntity(int index) {
                return ref world->GetEntity(entities[index]);
            }
            internal bool Has(int entity) {
                if (entitiesMap.Length <= entity) return false;
                return entitiesMap[entity] > 0;
            }

            internal void Add(int entity) {
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

            public QueryImpl* With(int type) {
                with.Add(type);
                return self;
            }

            public bool HasWith(int type) {
                return with.Has(type);
            }

            public bool HasNone(int type) {
                return none.Has(type);
            }
            public QueryImpl* None(int type) {
                none.Add(type);
                return self;
            }
        }

        internal readonly QueryImpl* impl;

        internal Query(World.WorldImpl* world) {
            impl = QueryImpl.Create(world);
        }

        internal Query(QueryImpl* impl) {
            this.impl = impl;
        }

        public Query With<T>(){
            impl->With(ComponentMeta<T>.Index);
            return this;
        }

        public Query None<T>() {
            impl->None(ComponentMeta<T>.Index);
            return this;
        }

        public ref Entity GetEntity(int index) {
            return ref impl->GetEntity(index);
        }
    }
    [BurstCompile]
    public unsafe struct Bitmask1024
    {
        internal const int BitsPerElement = 64;
        internal const int MaxBits = 1024;
        public const int ArraySize = MaxBits / BitsPerElement;

        internal fixed ulong _bitmaskArray[ArraySize];
        internal int count;

        // Property to get the count of set bits
        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return count; }
        }

        // Method to add an element (set a specific bit)
        [BurstCompile][MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(int position)
        {
            if (position < 0 || position >= MaxBits)
            {
                throw new ArgumentOutOfRangeException(nameof(position), $"Position must be between 0 and {MaxBits - 1}.");
                return false;
            }

            int index = position / BitsPerElement;
            int bitPosition = position % BitsPerElement;

            if (!Has(position))
            {
                _bitmaskArray[index] |= 1UL << bitPosition;
                count++;
                return true;
            }

            return false;
        }

        // Method to check if an element is present (a specific bit is set)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(int position)
        {
            // Eliminate bounds checking with (uint) position < MaxBits.
            if (position >= MaxBits)
            {
                return false;
            }
            var index = position / BitsPerElement;
            var bitMask = 1UL << (position % BitsPerElement);

            return (_bitmaskArray[index] & bitMask) != 0;
        }

        // Method to clear an element (unset a specific bit)
        [BurstCompile][MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(int position)
        {
            if (position < 0 || position >= MaxBits)
            {
                throw new ArgumentOutOfRangeException(nameof(position), $"Position must be between 0 and {MaxBits - 1}.");
            }

            int index = position / BitsPerElement;
            int bitPosition = position % BitsPerElement;

            if (Has(position))
            {
                _bitmaskArray[index] &= ~(1UL << bitPosition);
                count--;
            }
        }

        // Override ToString() to display the bitmask in binary form
        public override string ToString()
        {
            char[] bitChars = new char[ArraySize * BitsPerElement];
            for (int i = 0; i < ArraySize; i++)
            {
                string bits = Convert.ToString((long)_bitmaskArray[i], 2).PadLeft(BitsPerElement, '0');
                for (int j = 0; j < BitsPerElement; j++)
                {
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
    public unsafe struct BitmaskChunk
    {
        private const int BitsPerElement = 64;
        private const int ArraySize = 1024 / BitsPerElement;
        internal fixed ulong bitmaskArray[ArraySize];

        // Method to add an element (set a specific bit)
        public void Add(int position)
        {
            int index = position / BitsPerElement;
            int bitPosition = position % BitsPerElement;
            bitmaskArray[index] |= 1UL << bitPosition;
        }

        // Method to check if an element is present (a specific bit is set)
        public bool Has(int position)
        {
            int index = position / BitsPerElement;
            int bitPosition = position % BitsPerElement;
            return (bitmaskArray[index] & (1UL << bitPosition)) != 0;
        }

        // Method to clear an element (unset a specific bit)
        public void Remove(int position)
        {
            int index = position / BitsPerElement;
            int bitPosition = position % BitsPerElement;
            bitmaskArray[index] &= ~(1UL << bitPosition);
        }
    }
    
    public unsafe struct BitmaskMax : IDisposable
    {
        private const int ChunkSize = 1024;
        private const int NumberOfChunks = (int)(((long)int.MaxValue + 1) / ChunkSize);
        private BitmaskChunk* bitmaskChunks;
        private int count;

        // Constructor to allocate memory
        public static BitmaskMax New() {
            BitmaskMax bitmaskMax;
            bitmaskMax.bitmaskChunks = (BitmaskChunk*)Marshal.AllocHGlobal(NumberOfChunks * sizeof(BitmaskChunk));
            for (int i = 0; i < NumberOfChunks; i++)
            {
                bitmaskMax.bitmaskChunks[i] = new BitmaskChunk();
            }
            bitmaskMax.count = 0;
            return bitmaskMax;
        }

        // Destructor to free memory
        public void Free()
        {
            Dispose();
        }

        // Implement IDisposable to free memory
        public void Dispose()
        {
            if (bitmaskChunks != null)
            {
                Marshal.FreeHGlobal((IntPtr)bitmaskChunks);
                bitmaskChunks = null;
            }
        }

        // Property to get the count of set bits
        public int Count
        {
            get { return count; }
        }

        // Method to add an element (set a specific bit)
        public void Add(int position)
        {
            if (position < 0 || position >= int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(position), $"Position must be between 0 and {int.MaxValue - 1}.");
            }

            int chunkIndex = position / ChunkSize;
            int positionInChunk = position % ChunkSize;

            if (!Has(position))
            {
                bitmaskChunks[chunkIndex].Add(positionInChunk);
                count++;
            }
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(int position)
        {
            if (position < 0 || position >= int.MaxValue)
            {
                return false;
            }

            int chunkIndex = position / ChunkSize;
            int positionInChunk = position % ChunkSize;

            int index = positionInChunk / 64;
            int bitPosition = positionInChunk % 64;

            return (bitmaskChunks[chunkIndex].bitmaskArray[index] & (1UL << bitPosition)) != 0;
        }

        // Method to clear an element (unset a specific bit)
        public void Remove(int position)
        {
            if (position < 0 || position >= int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(position), $"Position must be between 0 and {int.MaxValue - 1}.");
            }

            int chunkIndex = position / ChunkSize;
            int positionInChunk = position % ChunkSize;

            if (Has(position))
            {
                bitmaskChunks[chunkIndex].Remove(positionInChunk);
                count--;
            }
        }

        // Override ToString() to display the bitmask in binary form
        public override string ToString()
        {
            var bitChars = new StringBuilder();
            for (int i = NumberOfChunks - 1; i >= 0; i--)
            {
                for (int j = 0; j < ChunkSize / 64; j++)
                {
                    string bits = Convert.ToString((long)bitmaskChunks[i].bitmaskArray[j], 2).PadLeft(64, '0');
                    bitChars.Append(bits);
                }
            }
            return bitChars.ToString();
        }
    }

    public struct Bitmask64
    {
        private ulong bitmask;
        private int count;


        // Property to get the count of set bits
        public int Count
        {
            get { return count; }
        }

        // Method to add an element (set a specific bit)
        public bool Add(int position)
        {
            if (position < 0 || position >= 64)
            {
                //throw new ArgumentOutOfRangeException(nameof(position), "Position must be between 0 and 63.");
                return false;
            }

            if (!Has(position))
            {
                bitmask |= 1UL << position;
                count++;
                return true;
            }

            return false;
        }

        // Method to check if an element is present (a specific bit is set)
        public bool Has(int position)
        {
            if (position < 0 || position >= 64)
            {
                throw new ArgumentOutOfRangeException(nameof(position), "Position must be between 0 and 63.");
            }

            return (bitmask & (1UL << position)) != 0;
        }

        // Method to clear an element (unset a specific bit)
        public void Remove(int position)
        {
            if (position < 0 || position >= 64)
            {
                throw new ArgumentOutOfRangeException(nameof(position), "Position must be between 0 and 63.");
            }

            if (Has(position))
            {
                bitmask &= ~(1UL << position);
                count--;
            }
        }

        // Override ToString() to display the bitmask in binary form
        public override string ToString()
        {
            return Convert.ToString((long)bitmask, 2).PadLeft(64, '0');
        }
    }
}
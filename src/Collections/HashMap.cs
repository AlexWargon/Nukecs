using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Wargon.Nukecs.Collections
{
    public unsafe struct HashMap<TKey, TValue> where TKey : unmanaged, IEquatable<TKey> where TValue : unmanaged
    {
        internal HashMapHelper<TKey> data;

        public HashMap(int initialCapacity, ref UnityAllocatorHandler allocatorHandler)
        {
            data = default;
            data.Init(initialCapacity, sizeof(TValue), HashMapHelper<TKey>.K_MINIMUM_CAPACITY, ref allocatorHandler);
        }
        public HashMap(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            data = default;
            data.Init(initialCapacity, sizeof(TValue), HashMapHelper<TKey>.K_MINIMUM_CAPACITY, allocator);
        }

        public void OnDeserialize(ref SerializableMemoryAllocator allocator, Allocator unityAllocator)
        {
            data.OnDeserialize(ref allocator, unityAllocator);
        }
        public void Dispose()
        {
            if (!IsCreated)
            {
                return;
            }

            data.Dispose();
        }
        public readonly bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => data.IsCreated;
        }
        public readonly bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => data.IsEmpty;
        }
        public readonly int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => data.Count;
        }
        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get => data.Capacity;
            set => data.Resize(value);
        }
        public void Clear()
        {
            data.Clear();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(TKey key, TValue item)
        {
            var idx = data.TryAdd(key);
            if (-1 != idx)
            {
                UnsafeUtility.WriteArrayElement(data.Ptr, idx, item);
                return true;
            }

            return false;
        }
        public void Add(TKey key, TValue item)
        {
            var result = TryAdd(key, item);

            if (!result)
            {
                ThrowKeyAlreadyAdded(key);
            }
        }
        public bool Remove(TKey key)
        {
            return -1 != data.TryRemove(key);
        }
        public bool TryGetValue(TKey key, out TValue item)
        {
            return data.TryGetValue(key, out item);
        }

        public bool TryGetValuePtr(TKey key, byte* basePtr, out ptr<TValue> ptr)
        {
            return data.TryGetPtr(key, basePtr, out ptr);
        }
        public bool ContainsKey(TKey key)
        {
            return -1 != data.Find(key);
        }
        public void TrimExcess() => data.TrimExcess();

        public TValue this[TKey key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                TValue result;
                if (!data.TryGetValue(key, out result))
                {
                    ThrowKeyNotPresent(key);
                }

                return result;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                var idx = data.Find(key);
                if (-1 != idx)
                {
                    UnsafeUtility.WriteArrayElement(data.Ptr, idx, value);
                    return;
                }

                TryAdd(key, value);
            }
        }
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        void ThrowKeyNotPresent(TKey key)
        {
            throw new ArgumentException($"Key: {key} is not present.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        void ThrowKeyAlreadyAdded(TKey key)
        {
            throw new ArgumentException($"An item with the same key has already been added: {key}");
        }
        public readonly Enumerator GetEnumerator()
        {
            fixed (HashMapHelper<TKey>* data = &this.data)
            {
                return new Enumerator { m_Enumerator = new HashMapHelper<TKey>.Enumerator(data) };
            }
        }
        public struct Enumerator
        {
            internal HashMapHelper<TKey>.Enumerator m_Enumerator;

            /// <summary>
            /// Does nothing.
            /// </summary>
            public void Dispose() { }

            /// <summary>
            /// Advances the enumerator to the next key-value pair.
            /// </summary>
            /// <returns>True if <see cref="Current"/> is valid to read after the call.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => m_Enumerator.MoveNext();

            /// <summary>
            /// Resets the enumerator to its initial state.
            /// </summary>
            public void Reset() => m_Enumerator.Reset();

            /// <summary>
            /// The current key-value pair.
            /// </summary>
            /// <value>The current key-value pair.</value>
            public KVPair<TKey, TValue> Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => m_Enumerator.GetCurrent<TValue>();
            }
        }
    }
    
    [StructLayout(LayoutKind.Sequential)]
    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
    internal unsafe struct HashMapHelper<TKey> where TKey : unmanaged, IEquatable<TKey>
    {
        internal ptr_offset PtrOffset;
        
        [NativeDisableUnsafePtrRestriction]
        internal byte* Ptr;
        
        [NativeDisableUnsafePtrRestriction]
        internal TKey* Keys;

        [NativeDisableUnsafePtrRestriction]
        internal int* Next;

        [NativeDisableUnsafePtrRestriction]
        internal int* Buckets;

        internal int Count;
        internal int Capacity;
        internal int Log2MinGrowth;
        internal int BucketCapacity;
        internal int AllocatedIndex;
        internal int FirstFreeIdx;
        internal int SizeOfTValue;
        int keyOffset, nextOffset, bucketOffset;
        internal AllocatorManager.AllocatorHandle Allocator;

        internal const int K_MINIMUM_CAPACITY = 256;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int CalcCapacityCeilPow2(int capacity)
        {
            capacity = math.max(math.max(1, Count), capacity);
            var newCapacity = math.max(capacity, 1 << Log2MinGrowth);
            var result = math.ceilpow2(newCapacity);

            return result;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetBucketSize(int capacity)
        {
            return capacity * 2;
        }

        internal readonly bool IsCreated
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Ptr != null;
        }

        internal readonly bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => !IsCreated || Count == 0;
        }

        internal void Clear()
        {
            UnsafeUtility.MemSet(Buckets, 0xff, BucketCapacity * sizeof(int));
            UnsafeUtility.MemSet(Next, 0xff, Capacity * sizeof(int));

            Count = 0;
            FirstFreeIdx = -1;
            AllocatedIndex = 0;
        }

        internal void OnDeserialize(ref SerializableMemoryAllocator allocator, Allocator unityAllocator)
        {
            Allocator = unityAllocator;
            Ptr = PtrOffset.AsPtr<byte>(ref allocator);
            Keys = (TKey*)(Ptr + keyOffset);
            Next = (int*)(Ptr + nextOffset);
            Buckets = (int*)(Ptr + bucketOffset);
        }
        
        internal void Init(int capacity, int sizeOfValueT, int minGrowth, ref UnityAllocatorHandler allocator)
        {
            Count = 0;
            Log2MinGrowth = (byte)(32 - math.lzcnt(math.max(1, minGrowth) - 1));

            capacity = CalcCapacityCeilPow2(capacity);
            Capacity = capacity;
            BucketCapacity = GetBucketSize(capacity);
            Allocator = allocator.AllocatorWrapper.Handle;
            SizeOfTValue = sizeOfValueT;

            int totalSize = CalculateDataSize(capacity, BucketCapacity, sizeOfValueT, out keyOffset, out nextOffset, out bucketOffset);
            PtrOffset = allocator.AllocatorWrapper.Allocator.AllocateRaw(totalSize);
            Ptr = PtrOffset.AsPtr<byte>(ref allocator.AllocatorWrapper.Allocator);
            Keys = (TKey*)(Ptr + keyOffset);
            Next = (int*)(Ptr + nextOffset);
            Buckets = (int*)(Ptr + bucketOffset);

            Clear();       
        }

        internal void Init(int capacity, int sizeOfValueT, int minGrowth, AllocatorManager.AllocatorHandle allocator)
        {
            Count = 0;
            Log2MinGrowth = (byte)(32 - math.lzcnt(math.max(1, minGrowth) - 1));

            capacity = CalcCapacityCeilPow2(capacity);
            Capacity = capacity;
            BucketCapacity = GetBucketSize(capacity);
            Allocator = allocator;
            SizeOfTValue = sizeOfValueT;

            int totalSize = CalculateDataSize(capacity, BucketCapacity, sizeOfValueT, out keyOffset, out nextOffset, out bucketOffset);

            Ptr = (byte*)AllocatorManager.Allocate(allocator, totalSize, JobsUtility.CacheLineSize);
            Keys = (TKey*)(Ptr + keyOffset);
            Next = (int*)(Ptr + nextOffset);
            Buckets = (int*)(Ptr + bucketOffset);

            Clear();
        }

        internal void Dispose()
        {
            AllocatorManager.Free(Allocator, Ptr);
            Ptr = null;
            Keys = null;
            Next = null;
            Buckets = null;
            Count = 0;
            BucketCapacity = 0;
        }

        internal static HashMapHelper<TKey>* Alloc(int capacity, int sizeOfValueT, int minGrowth, AllocatorManager.AllocatorHandle allocator)
        {
            var data = (HashMapHelper<TKey>*)AllocatorManager.Allocate(allocator, sizeof(HashMapHelper<TKey>), UnsafeUtility.AlignOf<HashMapHelper<TKey>>());
            data->Init(capacity, sizeOfValueT, minGrowth, allocator);

            return data;
        }

        internal static void Free(HashMapHelper<TKey>* data)
        {
            if (data == null)
            {
                throw new InvalidOperationException("Hash based container has yet to be created or has been destroyed!");
            }
            data->Dispose();
            AllocatorManager.Free(data->Allocator, data);
        }

        internal void Resize(int newCapacity)
        {
            newCapacity = math.max(newCapacity, Count);
            var newBucketCapacity = math.ceilpow2(GetBucketSize(newCapacity));

            if (Capacity == newCapacity && BucketCapacity == newBucketCapacity)
            {
                return;
            }

            ResizeExact(newCapacity, newBucketCapacity);
        }

        internal void ResizeExact(int newCapacity, int newBucketCapacity)
        {
            int keyOffset, nextOffset, bucketOffset;
            int totalSize = CalculateDataSize(newCapacity, newBucketCapacity, SizeOfTValue, out keyOffset, out nextOffset, out bucketOffset);

            var oldPtr = Ptr;
            var oldKeys = Keys;
            var oldNext = Next;
            var oldBuckets = Buckets;
            var oldBucketCapacity = BucketCapacity;

            Ptr = (byte*)AllocatorManager.Allocate(Allocator,totalSize, JobsUtility.CacheLineSize);
            Keys = (TKey*)(Ptr + keyOffset);
            Next = (int*)(Ptr + nextOffset);
            Buckets = (int*)(Ptr + bucketOffset);
            Capacity = newCapacity;
            BucketCapacity = newBucketCapacity;

            Clear();

            for (int i = 0, num = oldBucketCapacity; i < num; ++i)
            {
                for (int idx = oldBuckets[i]; idx != -1; idx = oldNext[idx])
                {
                    var newIdx = TryAdd(oldKeys[idx]);
                    UnsafeUtility.MemCpy(Ptr + SizeOfTValue * newIdx, oldPtr + SizeOfTValue * idx, SizeOfTValue);
                }
            }

            AllocatorManager.Free(Allocator, oldPtr);
        }

        internal void TrimExcess()
        {
            var capacity = CalcCapacityCeilPow2(Count);
            ResizeExact(capacity, GetBucketSize(capacity));
        }

        internal static int CalculateDataSize(int capacity, int bucketCapacity, int sizeOfTValue, out int outKeyOffset, out int outNextOffset, out int outBucketOffset)
        {
            var sizeOfTKey = sizeof(TKey);
            var sizeOfInt = sizeof(int);

            var valuesSize = sizeOfTValue * capacity;
            var keysSize = sizeOfTKey * capacity;
            var nextSize = sizeOfInt * capacity;
            var bucketSize = sizeOfInt * bucketCapacity;
            var totalSize = valuesSize + keysSize + nextSize + bucketSize;

            outKeyOffset = 0 + valuesSize;
            outNextOffset = outKeyOffset + keysSize;
            outBucketOffset = outNextOffset + nextSize;

            return totalSize;
        }

        internal readonly int GetCount()
        {
            if (AllocatedIndex <= 0)
            {
                return 0;
            }

            var numFree = 0;

            for (var freeIdx = FirstFreeIdx; freeIdx >= 0; freeIdx = Next[freeIdx])
            {
                ++numFree;
            }

            return math.min(Capacity, AllocatedIndex) - numFree;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int GetBucket(in TKey key)
        {
            return (int)((uint)key.GetHashCode() & (BucketCapacity - 1));
        }

        internal int TryAdd(in TKey key)
        {
            if (-1 == Find(key))
            {
                // Allocate an entry from the free list
                int idx;
                int* next;

                if (AllocatedIndex >= Capacity && FirstFreeIdx < 0)
                {
                    int newCap = CalcCapacityCeilPow2(Capacity + (1 << Log2MinGrowth));
                    Resize(newCap);
                }

                idx = FirstFreeIdx;

                if (idx >= 0)
                {
                    FirstFreeIdx = Next[idx];
                }
                else
                {
                    idx = AllocatedIndex++;
                }

                CheckIndexOutOfBounds(idx);

                UnsafeUtility.WriteArrayElement(Keys, idx, key);
                var bucket = GetBucket(key);

                // Add the index to the hash-map
                next = Next;
                next[idx] = Buckets[bucket];
                Buckets[bucket] = idx;
                Count++;

                return idx;
            }
            return -1;
        }

        internal int Find(TKey key)
        {
            if (AllocatedIndex > 0)
            {
                // First find the slot based on the hash
                var bucket = GetBucket(key);
                var entryIdx = Buckets[bucket];

                if ((uint)entryIdx < (uint)Capacity)
                {
                    var nextPtrs = Next;
                    while (!UnsafeUtility.ReadArrayElement<TKey>(Keys, entryIdx).Equals(key))
                    {
                        entryIdx = nextPtrs[entryIdx];
                        if ((uint)entryIdx >= (uint)Capacity)
                        {
                            return -1;
                        }
                    }

                    return entryIdx;
                }
            }

            return -1;
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
        internal bool TryGetValue<TValue>(TKey key, out TValue item)
            where TValue : unmanaged
        {
            var idx = Find(key);

            if (-1 != idx)
            {
                item = UnsafeUtility.ReadArrayElement<TValue>(Ptr, idx);
                return true;
            }

            item = default;
            return false;
        }
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
        internal TValue* TryGetPtr<TValue>(TKey key, out bool contain) where TValue : unmanaged
        {
            var idx = Find(key);
            if (-1 != idx)
            {
                contain = true;
                return ((TValue*)Ptr) + idx;
            }

            contain = false;
            return null;
        }
        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
        internal bool TryGetPtr<TValue>(TKey key, byte* basePtr, out ptr<TValue> valuePtr) where TValue : unmanaged
        {
            var idx = Find(key);

            if (-1 != idx)
            {
                var offset = (uint)(PtrOffset.Offset + sizeof(TValue) * idx);
                valuePtr = new ptr<TValue>(basePtr, offset);
                return true;
            }

            valuePtr = ptr<TValue>.NULL;
            return false;
        }
        internal int TryRemove(TKey key)
        {
            if (Capacity != 0)
            {
                var removed = 0;

                // First find the slot based on the hash
                var bucket = GetBucket(key);

                var prevEntry = -1;
                var entryIdx = Buckets[bucket];

                while (entryIdx >= 0 && entryIdx < Capacity)
                {
                    if (UnsafeUtility.ReadArrayElement<TKey>(Keys, entryIdx).Equals(key))
                    {
                        ++removed;

                        // Found matching element, remove it
                        if (prevEntry < 0)
                        {
                            Buckets[bucket] = Next[entryIdx];
                        }
                        else
                        {
                            Next[prevEntry] = Next[entryIdx];
                        }

                        // And free the index
                        int nextIdx = Next[entryIdx];
                        Next[entryIdx] = FirstFreeIdx;
                        FirstFreeIdx = entryIdx;
                        entryIdx = nextIdx;

                        break;
                    }
                    else
                    {
                        prevEntry = entryIdx;
                        entryIdx = Next[entryIdx];
                    }
                }

                Count -= removed;
                return 0 != removed ? removed : -1;
            }

            return -1;
        }

        internal bool MoveNextSearch(ref int bucketIndex, ref int nextIndex, out int index)
        {
            for (int i = bucketIndex, num = BucketCapacity; i < num; ++i)
            {
                var idx = Buckets[i];

                if (idx != -1)
                {
                    index = idx;
                    bucketIndex = i + 1;
                    nextIndex = Next[idx];

                    return true;
                }
            }

            index = -1;
            bucketIndex = BucketCapacity;
            nextIndex = -1;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool MoveNext(ref int bucketIndex, ref int nextIndex, out int index)
        {
            if (nextIndex != -1)
            {
                index = nextIndex;
                nextIndex = Next[nextIndex];
                return true;
            }

            return MoveNextSearch(ref bucketIndex, ref nextIndex, out index);
        }

        internal NativeArray<TKey> GetKeyArray(AllocatorManager.AllocatorHandle allocator)
        {
            var result = CollectionHelper.CreateNativeArray<TKey>(Count, allocator, NativeArrayOptions.UninitializedMemory);

            for (int i = 0, count = 0, max = result.Length, capacity = BucketCapacity
                ; i < capacity && count < max
                ; ++i
                )
            {
                int bucket = Buckets[i];

                while (bucket != -1)
                {
                    result[count++] = UnsafeUtility.ReadArrayElement<TKey>(Keys, bucket);
                    bucket = Next[bucket];
                }
            }

            return result;
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
        internal NativeArray<TValue> GetValueArray<TValue>(AllocatorManager.AllocatorHandle allocator)
            where TValue : unmanaged
        {
            var result = CollectionHelper.CreateNativeArray<TValue>(Count, allocator, NativeArrayOptions.UninitializedMemory);

            for (int i = 0, count = 0, max = result.Length, capacity = BucketCapacity; 
                 i < capacity && count < max; 
                 ++i)
            {
                int bucket = Buckets[i];

                while (bucket != -1)
                {
                    result[count++] = UnsafeUtility.ReadArrayElement<TValue>(Ptr, bucket);
                    bucket = Next[bucket];
                }
            }

            return result;
        }

        [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int) })]
        internal NativeKeyValueArrays<TKey, TValue> GetKeyValueArrays<TValue>(AllocatorManager.AllocatorHandle allocator)
            where TValue : unmanaged
        {
            var result = new NativeKeyValueArrays<TKey, TValue>(Count, allocator, NativeArrayOptions.UninitializedMemory);

            for (int i = 0, count = 0, max = result.Length, capacity = BucketCapacity
                ; i < capacity && count < max
                ; ++i
                )
            {
                int bucket = Buckets[i];

                while (bucket != -1)
                {
                    result.Keys[count] = UnsafeUtility.ReadArrayElement<TKey>(Keys, bucket);
                    result.Values[count] = UnsafeUtility.ReadArrayElement<TValue>(Ptr, bucket);
                    count++;
                    bucket = Next[bucket];
                }
            }

            return result;
        }

        internal unsafe struct Enumerator
        {
            [NativeDisableUnsafePtrRestriction]
            internal HashMapHelper<TKey>* m_Data;
            internal int m_Index;
            internal int m_BucketIndex;
            internal int m_NextIndex;

            internal unsafe Enumerator(HashMapHelper<TKey>* data)
            {
                m_Data = data;
                m_Index = -1;
                m_BucketIndex = 0;
                m_NextIndex = -1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal bool MoveNext()
            {
                return m_Data->MoveNext(ref m_BucketIndex, ref m_NextIndex, out m_Index);
            }

            internal void Reset()
            {
                m_Index = -1;
                m_BucketIndex = 0;
                m_NextIndex = -1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal KVPair<TKey, TValue> GetCurrent<TValue>()
                where TValue : unmanaged
            {
                return new KVPair<TKey, TValue> { m_Data = m_Data, m_Index = m_Index };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal TKey GetCurrentKey()
            {
                if (m_Index != -1)
                {
                    return m_Data->Keys[m_Index];
                }

                return default;
            }
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CheckIndexOutOfBounds(int idx)
        {
            if ((uint)idx >= (uint)Capacity)
            {
                throw new InvalidOperationException($"Internal HashMap error. idx {idx}");
            }
        }
    }
    
    [DebuggerDisplay("Key = {Key}, Value = {Value}")]
    [GenerateTestsForBurstCompatibility(GenericTypeArguments = new[] { typeof(int), typeof(int) })]
    public unsafe struct KVPair<TKey, TValue> where TKey : unmanaged, IEquatable<TKey> where TValue : unmanaged
    {
        internal HashMapHelper<TKey>* m_Data;
        internal int m_Index;
        internal int m_Next;

        /// <summary>
        ///  An invalid KeyValue.
        /// </summary>
        /// <value>In a hash map enumerator's initial state, its <see cref="UnsafeHashMap{TKey,TValue}.Enumerator.Current"/> value is Null.</value>
        public static KVPair<TKey, TValue> Null => new KVPair<TKey, TValue> { m_Index = -1 };

        /// <summary>
        /// The key.
        /// </summary>
        /// <value>The key. If this KeyValue is Null, returns the default of TKey.</value>
        public TKey Key
        {
            get
            {
                if (m_Index != -1)
                {
                    return m_Data->Keys[m_Index];
                }

                return default;
            }
        }

        /// <summary>
        /// Value of key/value pair.
        /// </summary>
        public ref TValue Value
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
                if (m_Index == -1)
                    throw new ArgumentException("must be valid");
#endif

                return ref UnsafeUtility.AsRef<TValue>(m_Data->Ptr + sizeof(TValue) * m_Index);
            }
        }

        /// <summary>
        /// Gets the key and the value.
        /// </summary>
        /// <param name="key">Outputs the key. If this KeyValue is Null, outputs the default of TKey.</param>
        /// <param name="value">Outputs the value. If this KeyValue is Null, outputs the default of TValue.</param>
        /// <returns>True if the key-value pair is valid.</returns>
        public bool GetKeyValue(out TKey key, out TValue value)
        {
            if (m_Index != -1)
            {
                key = m_Data->Keys[m_Index];
                value = UnsafeUtility.ReadArrayElement<TValue>(m_Data->Ptr, m_Index);
                return true;
            }

            key = default;
            value = default;
            return false;
        }
    }
}
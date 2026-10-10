using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Wargon.Nukecs {
    public static unsafe class NUnsafe {
        public static T* MallocTracked<T>(AllocatorHandle allocator) where T : unmanaged
        {
            return (T*) Mem.MallocTracked(sizeof(T), Mem.AlignOf<T>(), allocator, 0);
        }

        public static T* MallocTracked<T>(int items, AllocatorHandle allocator) where T : unmanaged {
            return (T*)Mem.MallocTracked(sizeof(T) * items, Mem.AlignOf<T>(), allocator, 0);
        }

        public static void FreeTracked(void* ptr, AllocatorHandle allocator) {
            Mem.FreeTracked(ptr, allocator);
        }

    }
    [BurstCompile]
    public struct random
    {
        private uint state;
        public static unsafe random New()
        {
            return new random(*UnsafeStatic.malloc_t_cast<uint>(AllocatorHandle.Temp));
        }
        
        public random(uint seed)
        {
            state = seed != 0 ? seed : 1;
        }
        [BurstCompile]
        public float Range(float min, float max)
        {
            return NextFloat(min, max);
        }
        [BurstCompile]
        public int Range(int min, int max)
        {
            return NextInt(min, max);
        }
        [BurstCompile]
        public uint NextUInt()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        [BurstCompile]
        public float NextFloat()
        {
            return (NextUInt() & 0x00FFFFFF) / (float)0x01000000;
        }

        [BurstCompile]
        public int NextInt(int min, int max)
        {
            return min + (int)(NextUInt() % (uint)(max - min));
        }

        [BurstCompile]
        public float NextFloat(float min, float max)
        {
            return math.lerp(min, max, NextFloat());
        }
    }

    public unsafe struct nString
    {
        private char* ptr;
    }
}
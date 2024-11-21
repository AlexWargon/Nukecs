using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs.Collision2D
{
    using Unity.Mathematics;

    public struct Body2D : IComponent {
        public float2 velocity;
    }

    public static unsafe class allocator {
        private static Chunk* memory;
        private static int lastFreeBlock;
        private static int currentBlock;
        public static void initialize(int blockSize, int blockAmount) {
            memory = (Chunk*)UnsafeUtility.MallocTracked(sizeof(Chunk) * blockAmount, UnsafeUtility.AlignOf<Chunk>(),
                Allocator.Persistent, 0);
        }
        // public static ref T @allocate<T>() where T : unmanaged {
        //     ref var chunk = ref memory[currentBlock];
        //     if (chunk.start < chunk.end) {
        //         ref var item = ref *(T*)chunk.start;
        //         chunk.start += sizeof(T);
        //         return ref item;
        //     }
        //     var m = new AllocatorHelper<>()
        //     return ref default;
        // }
        //
        // public static void @destroy<T>(T* ptr) where T : unmanaged {
        //     
        // }
        internal unsafe struct Chunk {
            internal byte* start;
            internal byte* end;
            internal int sizeInBytes;
        }
    }
}  
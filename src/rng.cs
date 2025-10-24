using Unity.Burst;
using Unity.Collections;
using static Wargon.Nukecs.UnsafeStatic;
// ReSharper disable InconsistentNaming
namespace Wargon.Nukecs
{
    public unsafe struct rng
    {
        private static readonly SharedStatic<random> random = SharedStatic<random>.GetOrCreate<rng>();

        static rng()
        {
            var seed = malloc_t<uint>(Allocator.Temp);
            random.Data = new random(*seed);
        }
        [BurstCompile]
        public static int range(int min, int max)
        {
            return random.Data.NextInt(min, max);
        }
        [BurstCompile]
        public static float range(float min, float max)
        {
            return random.Data.NextFloat(min, max);
        }
        public static float val => random.Data.NextFloat();
    }
}
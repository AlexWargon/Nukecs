using Unity.Collections;

namespace Wargon.Nukecs
{
    public class StartFixedECBSystem : ISystem, IOnCreate, IFixed
    {
        private EntityCommandBuffer ecb;
        public static StartFixedECBSystem Instance;
        public void OnCreate(ref World world)
        {
            ecb = new EntityCommandBuffer(512, Allocator.Persistent);
            Instance = this;
        }

        public void OnUpdate(ref State state)
        {
            ecb.Playback(ref state.World);
        }
        public ref EntityCommandBuffer CommandBuffer => ref ecb;
    }
}
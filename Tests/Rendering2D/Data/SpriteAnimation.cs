using System.Runtime.InteropServices;

namespace Wargon.Nukecs.Tests {
    [StructLayout(LayoutKind.Sequential)]
    public struct SpriteAnimation : IComponent
    {
        public const int MaxFrames = 16;
        public int FrameCount;
        public float FrameRate;
        public float CurrentTime;
        public int AnimationID;
    }
}
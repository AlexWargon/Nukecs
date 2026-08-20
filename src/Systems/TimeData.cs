using System.Runtime.InteropServices;

namespace Wargon.Nukecs
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TimeData
    {
        public float DeltaTime;
        public float DeltaTimeFixed;
        public float Time;
        public double ElapsedTime;
        public uint TickCount;
    }
}
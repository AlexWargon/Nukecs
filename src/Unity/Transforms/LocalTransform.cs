using System.Runtime.CompilerServices;

namespace Wargon.Nukecs.Transforms {
    
    using System.Runtime.InteropServices;
    using Unity.Mathematics;
    
    [StructLayout(LayoutKind.Sequential)]
    public struct LocalTransform : IComponent{
        public float3 Position;
        public quaternion Rotation;
        public float3 Scale;
        public float4x4 Matrix
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => float4x4.TRS(Position, Rotation, Scale);
        }
    }
}
namespace Wargon.Nukecs.Transforms {
    
    using System.Runtime.InteropServices;
    using Unity.Mathematics;
    
    [StructLayout(LayoutKind.Sequential)]
    public struct LocalTransform : IComponent{
        public float3 Position;
        public quaternion Rotation;
        public float3 Scale;
    }
}
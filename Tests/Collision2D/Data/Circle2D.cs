namespace Wargon.Nukecs.Collision2D
{
    using System;
    using System.Runtime.InteropServices;
    using Unity.Mathematics;

    [StructLayout(LayoutKind.Sequential)]
    public struct Circle2D : IComponent, IEquatable<Circle2D> {
        public int index;
        public int version;
        public int cellIndex;
        public float radius;
        public float radiusDefault;
        public float2 position;
        [MarshalAs(UnmanagedType.U1)] public bool collided;
        [MarshalAs(UnmanagedType.U1)] public bool trigger;
        [MarshalAs(UnmanagedType.U1)] public bool oneFrame;
        public CollisionLayer layer;
        public CollisionLayer collideWith;

        public override int GetHashCode() {
            return index;
        }

        public bool Equals(Circle2D other) {
            return other.index == index;
        }
    }
}  
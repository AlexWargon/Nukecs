namespace Wargon.Nukecs.Collision2D
{
    using Unity.Mathematics;

    public struct HitInfo {
        public float2 Pos;
        public float2 Normal;
        public int From;
        public int To;
        public bool HasCollision => To != 0;
    }
}  
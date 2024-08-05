namespace Wargon.Nukecs.Collision2D
{
    using System.Runtime.InteropServices;
    using Unity.Mathematics;
    using UnityEngine;

    [StructLayout(LayoutKind.Sequential)]
    public struct Grid2DCell {
        public int W;
        public int H;
        public int Index;
        public float2 Pos;
        public BufferInt256 CollidersBuffer;
        public BufferInt128 RectanglesBuffer;
        private Vector3 Y1 => new(Pos.x, Pos.y + H);
        private Vector3 X1 => new(Pos.x, Pos.y);
        private Vector3 Y2 => new(Pos.x + W, Pos.y + H);
        private Vector3 X2 => new(Pos.x + W, Pos.y);

        public void Draw(Color color) {
            Debug.DrawLine(X1, Y1, color);
            Debug.DrawLine(Y1, Y2, color);
            Debug.DrawLine(Y2, X2, color);
            Debug.DrawLine(X2, X1, color);
        }

        // public void DrawSolid(Color color) {
        //     DebugUtility.DrawRect(new Vector2(Pos.x, Pos.y), new Vector2(W, H), color, color);
        // }
        //
        // public unsafe ref Circle2D GetCircle(Circle2D* ptr, int index) {
        //     return ref UnsafeUtility.ArrayElementAsRef<Circle2D>(ptr, index);
        // }
    }
}  
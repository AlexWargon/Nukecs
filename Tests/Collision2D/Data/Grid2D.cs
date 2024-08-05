namespace Wargon.Nukecs.Collision2D
{
    using System.Runtime.CompilerServices;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;
    using UnityEngine;

    public class Grid2D {
        public static Grid2D Instance;
        public UnsafeList<Grid2DCell> cells;
        
        private int count;
        public NativeQueue<HitInfo> Hits;
        private int len;
        public Vector2 Offset;
        public Vector2 Position;
        internal GenericPool circleColliders;
        internal GenericPool rectColliders;
        internal GenericPool trasforms;
        public int W, H, CellSize;
        private readonly World world;

        public Grid2D(int w, int h, int cellSize, World world, Vector2 offset, Vector2 position = default) {
            this.world = world;
            Position = position;
            W = w;
            H = h;
            Offset = offset;
            CellSize = cellSize;
            cells = new UnsafeList<Grid2DCell>(W * H, Allocator.Persistent);
            cells.Length = W * H;
            circleColliders = world.GetPool<Circle2D>();
            trasforms = world.GetPool<Wargon.Nukecs.Transforms.Transform>();
            rectColliders = world.GetPool<Rectangle2D>();

            Hits = new NativeQueue<HitInfo>(Allocator.Persistent);

            for (var x = 0; x < W; x++)
            for (var y = 0; y < H; y++) {
                var i = W * y + x;
                var cell = new Grid2DCell {
                    W = cellSize,
                    H = cellSize,
                    Pos = new Vector2(x * cellSize, y * cellSize) + offset + Position,
                    CollidersBuffer = default,
                    RectanglesBuffer = default,
                    Index = i
                };
                cells[i] = cell;
            }

            Instance = this;
        }
        
        //public unsafe Circle2D* Colliders => circleColliders;

        public void UpdateGrid(Vector2Int size, int cellSize) {
            W = size.x;
            H = size.y;
            CellSize = cellSize;
            Offset = new Vector2(-(size.x * CellSize / 2), -(size.y * CellSize / 2));
            cells.Dispose();
            cells = new UnsafeList<Grid2DCell>(W * H, Allocator.Persistent);
            cells.Length = W * H;
            
            for (var x = 0; x < W; x++)
            for (var y = 0; y < H; y++) {
                var i = W * y + x;
                var cell = new Grid2DCell {
                    W = cellSize,
                    H = cellSize,
                    Pos = new Vector2(x * cellSize, y * cellSize) + Offset + Position,
                    CollidersBuffer = default,
                    RectanglesBuffer = default,
                    Index = i
                };
                cells[i] = cell;
            }

        }

        public void DrawCells() {
#if UNITY_EDITOR
            var style = new GUIStyle();
            style.normal.textColor = Color.white;

            for (var i = 0; i < cells.Length; i++) {
                var cell = cells[i];
                cell.Draw(Color.yellow);
                //UnityEditor.Handles.Label((Vector2)cell.Pos + Vector2.one, $"{cell.index}", style);
                //UnityEditor.Handles.Label((Vector2)cell.Pos + Vector2.one * 2, $"{cell.CollidersBuffer.Count}", style);
            }
#endif
        }

        [BurstCompile(FloatMode = FloatMode.Fast)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool IsOverlap(Circle2D* circle1, in Circle2D circle2, out float distance) {
            distance = math.distance(circle2.position, circle1->position);
            return circle1->radius + circle2.radius > distance;
        }

        [BurstCompile(FloatMode = FloatMode.Fast)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOverlap(in Circle2D circle1, in Circle2D circle2, out float distance) {
            distance = math.distance(circle2.position, circle1.position);
            return circle1.radius + circle2.radius > distance;
        }

        public void Clear() {
            cells.Dispose();
            Hits.Dispose();
        }
    }
}  
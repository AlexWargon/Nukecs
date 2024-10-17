using System;
using System.Collections.Generic;

namespace Wargon.Nukecs.Collision2D
{
    using System.Runtime.CompilerServices;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;
    using UnityEngine;

    public unsafe class Grid2D {
        public static Grid2D Instance;
        public UnsafeList<Grid2DCell> cells;
        
        private int count;
        public NativeQueue<HitInfo> Hits;
        private int len;
        public Vector2 Offset;
        public Vector2 Position;

        public NativeParallelHashMap<ulong, bool> collisionStates;
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

            Hits = new NativeQueue<HitInfo>(Allocator.Persistent);
            collisionStates = new NativeParallelHashMap<ulong, bool>(world.Config.StartEntitiesAmount,
                Allocator.Persistent);
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
            style.fontSize = 24;
            
            for (var i = 0; i < cells.Length; i++) {
                var cell = cells[i];
                cell.Draw(Color.yellow);
                //UnityEditor.Handles.Label((Vector2)cell.Pos + Vector2.one, $"{cell.Index}", style);
                //UnityEditor.Handles.Label((Vector2)cell.Pos + Vector2.up, $"{cell.CollidersBuffer.Count}", style);
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
            collisionStates.Dispose();
        }
    }
    public static class Debug2D {
        public static readonly Queue<(Action,Color)> Buffer = new Queue<(Action, Color)>();
        public static void DrawRect(Vector2 pos, Vector2 size, Color color, Color colorOutline) {
#if UNITY_EDITOR
            ;
            Buffer.Enqueue((() => {
                    UnityEditor.Handles.DrawSolidRectangleWithOutline(new Rect(pos, size), color, colorOutline);}
                ,color));
#endif
        }

        public static void DrawCircle(Vector2 pos, float radius, Color color, float thick) {
#if UNITY_EDITOR
            Buffer.Enqueue((() => {
                    UnityEditor.Handles.DrawWireDisc(pos, Vector3.forward, radius, thick);}
                ,color));
#endif
        }
        
        public static void DrawLabel(string text, Vector3 pos, Color color, GUIStyle style = null) {
#if UNITY_EDITOR
            if (style == null) style = GUIStyle.none;
            style.normal.textColor = color;
            Buffer.Enqueue((
                ()=>{UnityEditor.Handles.Label(pos, text, style);}
                , color));
#endif
        }
    }
}  
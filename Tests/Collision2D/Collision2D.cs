using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Wargon.Nukecs;
using Transform = Wargon.Nukecs.Tests.Transform;
    [StructLayout(LayoutKind.Sequential)]
    public struct Circle2D : IEquatable<Circle2D> {
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
    public struct Body2D {
        public float2 velocity;
    }
    public struct Rectangle2D {
        public int index;
        public float w;
        public float h;
        public CollisionLayer layer;
        public CollisionLayer collisionWith;
    }

    public struct HitInfo {
        public float2 Pos;
        public float2 Normal;
        public int From;
        public int To;
        public bool HasCollision => To != 0;
    }

    [Flags]
    public enum CollisionLayer {
        None = 0,
        Player = 1 << 0,
        Enemy = 1 << 1,
        PlayerProjectle = 1 << 2,
        EnemyProjectile = 1 << 3,
        Bonus = 1 << 4,
        DoorTrigger = 1 << 5,
        WinCollider = 1 << 6
    }

    public unsafe struct BufferInt128 {
        private fixed int buffer[128];
        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set;
        }

        public int this[int index] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => buffer[index];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => buffer[index] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int value) {
            if (Count == 127) return;
            buffer[Count++] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() {
            Count = 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    [BurstCompile]
    public unsafe struct BufferInt256 {
        private fixed int buffer[256];

        public int Count {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set;
        }

        public int this[int index] {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => buffer[index];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => buffer[index] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int value) {
            if (Count == 255) return;
            buffer[Count++] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() {
            Count = 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Grid2DCell {
        public int W;
        public int H;
        public int index;
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
        public NativeQueue<HitInfo> StayHits;
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
            trasforms = world.GetPool<Wargon.Nukecs.Tests.Transform>();
            rectColliders = world.GetPool<Rectangle2D>();

            Hits = new NativeQueue<HitInfo>(Allocator.Persistent);
            StayHits = new NativeQueue<HitInfo>(Allocator.Persistent);

            for (var x = 0; x < W; x++)
            for (var y = 0; y < H; y++) {
                var i = W * y + x;
                var cell = new Grid2DCell {
                    W = cellSize,
                    H = cellSize,
                    Pos = new Vector2(x * cellSize, y * cellSize) + offset + Position,
                    CollidersBuffer = default,
                    RectanglesBuffer = default,
                    index = i
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
                    index = i
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
            StayHits.Dispose();
        }
    }
    public static class MathHelp {
        private static int floor(float x) {
            var xi = (int)x;
            return x < xi ? xi - 1 : xi;
        }

        public static int floorToInt(this float x) {
            var xint = (int)x;
            return x < xint ? xint - 1 : xint;
        }
    }

    public struct Collision2DSystem : ISystem, IOnCreate {
        private GenericPool transforms;
        private GenericPool colliders;
        private GenericPool rectangles;
        private GenericPool bodies;
        public void OnCreate(ref World world) {
            
        }
        public void OnUpdate(ref World world, float deltaTime) {
            Collision2DMark2ParallelHitsJob collisionJob1 = new() {
                collisionEnterHits = Grid2D.Instance.Hits.AsParallelWriter(),
                colliders = colliders.AsComponentPool<Circle2D>(),
                transforms = transforms.AsComponentPool<Transform>(),
                bodies = bodies.AsComponentPool<Body2D>(),
                rectangles = rectangles.AsComponentPool<Rectangle2D>(),
                cells = Grid2D.Instance.cells,
                W = Grid2D.Instance.W,
                H = Grid2D.Instance.H,
                Offset = Grid2D.Instance.Offset,
                GridPosition = Grid2D.Instance.Position,
                cellSize = Grid2D.Instance.CellSize,
                iterations = 1
            };


            world.Dependencies = collisionJob1.Schedule(Grid2D.Instance.cells.Length, 1, world.Dependencies);
        }


    }
    [BurstCompile]
    public struct Collision2DMark2ParallelHitsJob : IJobParallelFor {
            public UnsafeList<Grid2DCell> cells;
            public ComponentPool<Circle2D> colliders;
            public ComponentPool<Transform> transforms;
            public ComponentPool<Rectangle2D> rectangles;
            public ComponentPool<Body2D> bodies;
            [WriteOnly] public NativeQueue<HitInfo>.ParallelWriter collisionEnterHits;
            [WriteOnly] public UnsafeList<HitInfo>.ParallelWriter collisionEnterList;
            public float2 Offset, GridPosition;
            public int W, H, cellSize, iterations;

            public void Execute(int idx) {
                var x = idx % W;
                var y = idx / W;

                var cell1 = cells[idx];
                cell1.Pos = new float2(x * cellSize, y * cellSize) + Offset + GridPosition;
                cells[idx] = cell1;

                for (var dx = -1; dx <= 1; ++dx)
                for (var dy = -1; dy <= 1; ++dy) {
                    var di = W * (y + dy) + x + dx;
                    if (di < 0 || di >= cells.Length) continue;
                    var cell2 = cells[di];

                    for (var i = 0; i < cell1.CollidersBuffer.Count; i++) {
                        var e1 = cell1.CollidersBuffer[i];
                        ref var c1 = ref colliders.Get(e1);
                        //ref var t1 = ref transforms.Get(circle1.index);
                        ref var b1 = ref bodies.Get(e1);

                        for (var iteration = 0; iteration < iterations; iteration++)
                        for (var j = 0; j < cell2.CollidersBuffer.Count; j++) {
                            var e2 = cell2.CollidersBuffer[j];
                            if (e1 == e2) continue;
                            ref var c2 = ref colliders.Get(e2);

                            if ((c1.collideWith & c2.layer) == c2.layer)
                                if (Grid2D.IsOverlap(in c1, in c2, out var distance)) {
                                    c1.collided = true;
                                    c2.collided = true;
                                    ref var b2 = ref bodies.Get(e2);

                                    if (c1.layer == CollisionLayer.Enemy && c2.layer == CollisionLayer.Enemy)
                                        ResolveCollisionInternal(ref c1, ref c2, distance, ref b1, ref b2);
                                    else {
                                        var hitInfo =
                                            ResolveCollisionInternal(ref c1, ref c2, distance, ref b1, ref b2);
                                        collisionEnterHits.Enqueue(hitInfo);
                                        
                                    }
                                }
                        }

                        for (var j = 0; j < cell2.RectanglesBuffer.Count; j++) {
                            var e2 = cell2.RectanglesBuffer[j];
                            if (e1 == e2) continue;
                            ref var rect = ref rectangles.Get(e2);
                            ref var rectTransform = ref transforms.Get(e2);
                            if (CircleRectangleCollision(in c1, in rect, in rectTransform)) {
                                c1.collided = true;

                                var hit = ResolveCollisionCircleVsRectInternal(ref c1, ref b1, in rect,
                                    in rectTransform);
                                collisionEnterHits.Enqueue(hit);
                            }
                        }
                    }
                }
            }

            [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Default)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private HitInfo ResolveCollisionCircleVsRectInternal(ref Circle2D circle, ref Body2D circleBody, in Rectangle2D rect, in Transform rectTransform) {
                var closestX = math.max(rectTransform.Position.x,
                    math.min(circle.position.x, rectTransform.Position.x + rect.w));
                var closestY = math.max(rectTransform.Position.y,
                    math.min(circle.position.y, rectTransform.Position.y + rect.h));
                var deltaX = circle.position.x - closestX;
                var deltaY = circle.position.y - closestY;
                float distance;
                if (deltaX == 0 && deltaY == 0)
                    distance = 0.0f; // Set a default distance
                else
                    distance = math.sqrt(deltaX * deltaX + deltaY * deltaY);
                var overlap = circle.radius - distance;
                float2 normal = default;
                if (distance != 0) normal = new float2(deltaX / distance, deltaY / distance);
                circleBody.velocity += normal * overlap;
                return new HitInfo {
                    Pos = new float2(normal.x, normal.y),
                    Normal = normal,
                    From = circle.index,
                    To = rect.index
                };
            }

            [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Default)]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool CircleRectangleCollision(in Circle2D circle, in Rectangle2D rectangle2D,
                in Transform rectTransform) {
                var closestX = math.max(rectTransform.Position.x,
                    math.min(circle.position.x, rectTransform.Position.x + rectangle2D.w));
                var closestY = math.max(rectTransform.Position.y,
                    math.min(circle.position.y, rectTransform.Position.y + rectangle2D.h));
                var distanceSquared = math.distancesq(circle.position, new float2(closestX, closestY));
                return distanceSquared <= circle.radius * circle.radius;
            }

            [BurstCompile]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private HitInfo ResolveCollisionInternal(ref Circle2D circle1, ref Circle2D circle2, float distance,
                ref Body2D b1, ref Body2D b2) {
                var direction = circle2.position - circle1.position;
                var normal = math.normalize(direction);
                var depth = circle1.radius + circle2.radius - distance;
                if (!(circle1.trigger || circle2.trigger)) {
                    if (depth < 0.2F) {
                        circle1.position -= normal * depth * 0.5F;
                        circle2.position += normal * depth * 0.5f;
                        b1.velocity -= normal * depth * 0.5F;
                        b2.velocity += normal * depth * 0.5F;
                    }
                    else {
                        b1.velocity -= normal * depth * 0.15F;
                        b2.velocity += normal * depth * 0.15F;
                    }

                    //t1.position.x = circle1.position.x;
                    //t1.position.y = circle1.position.y;
                    //t2.position.x = circle2.position.x;
                    //t2.position.y = circle2.position.y;
                }

                return new HitInfo {
                    Pos = circle1.position + normal * circle1.radius,
                    Normal = normal,
                    From = circle1.index,
                    To = circle2.index
                };
            }
        }
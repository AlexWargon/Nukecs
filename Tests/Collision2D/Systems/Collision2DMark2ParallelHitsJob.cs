namespace Wargon.Nukecs.Collision2D {
    using System.Runtime.CompilerServices;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Transform = Transforms.Transform;

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
                if (di < 0 || di >= cells.m_length) continue;
                var cell2 = cells[di];

                for (var i = 0; i < cell1.CollidersBuffer.Count; i++) {
                    var e1 = cell1.CollidersBuffer[i];
                    ref var c1 = ref colliders.Get(e1);
                    //ref var t1 = ref transforms.Get(e1);
                    ref var b1 = ref bodies.Get(e1);

                    for (var iteration = 0; iteration < iterations; iteration++)
                    for (var j = 0; j < cell2.CollidersBuffer.Count; j++) {
                        var e2 = cell2.CollidersBuffer[j];
                        if (e1 == e2) continue;
                        ref var c2 = ref colliders.Get(e2);
                        //ref var t2 = ref transforms.Get(e2);
                        if ((c1.collideWith & c2.layer) == c2.layer) {
                            if (Grid2D.IsOverlap(in c1, in c2, out var distance)) {
                                c1.collided = true;
                                c2.collided = true;
                                ref var b2 = ref bodies.Get(e2);

                                if (c1.layer == CollisionLayer.Enemy && c2.layer == CollisionLayer.Enemy)
                                    _ = ResolveCollisionInternal(ref c1, ref c2, distance, ref b1, ref b2);
                                else {
                                    var hitInfo =
                                        ResolveCollisionInternal(ref c1, ref c2, distance, ref b1, ref b2);
                                    collisionEnterHits.Enqueue(hitInfo);
                                }
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

                            var hit = ResolveCollisionCircleVsRectInternal(ref c1, ref b1, in rect, in rectTransform);
                            collisionEnterHits.Enqueue(hit);
                        }
                    }
                }
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Default)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private HitInfo ResolveCollisionCircleVsRectInternal(ref Circle2D circle, ref Body2D circleBody,
            in Rectangle2D rect, in Transform rectTransform) {
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
                    circle1.position -= normal * depth * 0.7F;
                    circle2.position += normal * depth * 0.7F;
                    b1.velocity -= normal * depth * 0.7F;
                    b2.velocity += normal * depth * 0.7F;
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
}
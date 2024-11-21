namespace Wargon.Nukecs.Collision2D {
    using System.Runtime.CompilerServices;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Transform = Transforms.Transform;

    [BurstCompile]
    public struct Collision2DHitsParallelJob : IJobParallelFor {
        public UnsafeList<Grid2DCell> cells;
        public ComponentPool<Circle2D> colliders;
        public ComponentPool<Transform> transforms;
        public ComponentPool<Rectangle2D> rectangles;
        public ComponentPool<Body2D> bodies;
        public ComponentPool<ComponentArray<Collision2DData>> collisionData;
        [WriteOnly] public NativeQueue<HitInfo>.ParallelWriter collisionEnterHits;
        public float2 Offset, GridPosition;
        public int W, H, cellSize;
        public World world;
        public void Execute(int idx) {
            var x = idx % W;
            var y = idx / W;

            ref var cell1 = ref cells.ElementAt(idx);
            cell1.Pos = new float2(x * cellSize, y * cellSize) + Offset + GridPosition;

            for (var dx = -1; dx <= 1; ++dx)
            for (var dy = -1; dy <= 1; ++dy) {
                var di = W * (y + dy) + x + dx;
                if (di < 0 || di >= cells.m_length) continue;
                
                var cell2 = cells[di];
                
                for (var i = 0; i < cell1.CollidersBuffer.Count; i++) {
                    var e1 = cell1.CollidersBuffer[i];
                    ref var c1 = ref colliders.Get(e1);
                    ref var t1 = ref transforms.Get(e1);
                    ref var b1 = ref bodies.Get(e1);

                    for (var j = 0; j < cell2.CollidersBuffer.Count; j++) {
                        var e2 = cell2.CollidersBuffer[j];
                        if (e1 == e2) continue;
                        ref var c2 = ref colliders.Get(e2);
                        ref var t2 = ref transforms.Get(e2);
                        if ((c1.collideWith & c2.layer) == c2.layer)
                        {
                            var distance = math.distance(t1.Position, t2.Position);
                            if (c1.radius + c2.radius >= distance) {
                                c1.collided = true;
                                c2.collided = true;
                                c1.index = e1;
                                c2.index = e2;
                                ref var b2 = ref bodies.Get(e2);
                                HitInfo hitInfo = default;
                                ResolveCollisionInternal(ref c1, ref c2, distance, ref b1, ref b2, ref transforms.Get(e1), ref transforms.Get(e2), ref hitInfo);
                                //if (c1.layer == CollisionLayer.Enemy && c2.layer == CollisionLayer.Enemy) {}
                                //else { collisionEnterHits.Enqueue(hitInfo); }
                                
                                
                                collisionEnterHits.Enqueue(hitInfo);
                                ref var buffer1 = ref collisionData.Get(e1);
                                ref var buffer2 = ref collisionData.Get(e2);
                                ref var ent1 = ref world.GetEntity(e1);
                                ref var ent2 = ref world.GetEntity(e2);
                                buffer1.AddParallel(new Collision2DData() {
                                    Other = ent2,
                                    Type = hitInfo.Type,
                                    Position = hitInfo.Pos,
                                    Normal = hitInfo.Normal
                                });
                                
                                buffer2.AddParallel(new Collision2DData() {
                                    Other = ent1,
                                    Type = hitInfo.Type,
                                    Position = hitInfo.Pos,
                                    Normal = hitInfo.Normal
                                });
                                
                                ent1.Add(new CollidedFlag());
                                ent2.Add(new CollidedFlag());
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong GetCollisionKey(int e1, int e2)
        {
            if (e1 > e2)
            {
                (e1, e2) = (e2, e1);
            }
            return ((ulong)e1 << 32) | (uint)e2;
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
        private void ResolveCollisionInternal(ref Circle2D circle1, ref Circle2D circle2, float distance,
            ref Body2D b1, ref Body2D b2, ref Transform t1, ref Transform t2, ref HitInfo hitInfo) {
            var delta = t2.Position - t1.Position;
            var normal = math.normalize(delta);
            var depth = circle1.radius + circle2.radius - distance;
            var normal2d = new float2(normal.x, normal.y);
            if (!(circle1.trigger || circle2.trigger))
            {
                t1.Position -= normal * depth * 0.5F;
                t2.Position += normal * depth * 0.5f;
            }

            // float velocityAlongNormal1 = math.dot(b1.velocity, normal2d);
                // float velocityAlongNormal2 = math.dot(b2.velocity, normal2d);
                //
                // float j = (velocityAlongNormal1 + velocityAlongNormal2) * (1 + 0.5f); // adjust the restitution coefficient as needed
                // var impulse = normal2d * j;
                //
                // b1.velocity -= impulse;
                // b2.velocity += impulse; 

                //t1.position.x = circle1.position.x;
                //t1.position.y = circle1.position.y;
                //t2.position.x = circle2.position.x;
                //t2.position.y = circle2.position.y;
            //}

            
            hitInfo = new HitInfo {
                Pos = circle1.position + normal2d * circle1.radius,
                Normal = normal2d,
                From = circle1.index,
                To = circle2.index
            };
        }
    }




}
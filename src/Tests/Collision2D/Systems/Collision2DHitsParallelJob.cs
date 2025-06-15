namespace Wargon.Nukecs.Collision2D {
    using System.Runtime.CompilerServices;
    using Unity.Burst;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Mathematics;
    using Transform = Transforms.Transform;
    
    [BurstCompile]
    public struct Collision2DHitsParallelJobBatched : IJobParallelForBatch
    {
        public UnsafeList<Grid2DCell> Cells;
        public ComponentPool<Circle2D> Colliders;
        public ComponentPool<Transform> Transforms;
        public ComponentPool<Rectangle2D> Rectangles;
        public ComponentPool<Body2D> Bodies;
        public ComponentPool<ComponentArray<Collision2DData>> CollisionData;
        [WriteOnly] public NativeQueue<HitInfo>.ParallelWriter CollisionEnterHits;
        public float2 Offset, GridPosition;
        public int W, H, CellSize;
        public World World;
        [WriteOnly] public NativeParallelHashSet<ulong>.ParallelWriter ProcessedCollisions;
        [ReadOnly] public NativeList<int> CellIndexes;
        [BurstCompile]
        public void Execute(int startIndex, int count)
        {
            for (var idx = startIndex; idx < startIndex + count; idx++)
            {
                var cellIndex = CellIndexes[idx];
                var x = cellIndex % W;
                var y = cellIndex / W;
                
                ref var cell1 = ref Cells.ElementAt(cellIndex);
                cell1.Pos = new float2(x * CellSize, y * CellSize) + Offset + GridPosition;

                for (var dx = -1; dx <= 1; ++dx)
                for (var dy = -1; dy <= 1; ++dy)
                {
                    var di = W * (y + dy) + x + dx;
                    if (di < 0 || di >= Cells.m_length) continue;

                    var cell2 = Cells[di];

                    for (var i = 0; i < cell1.CollidersBuffer.Count; i++)
                    {
                        var e1 = cell1.CollidersBuffer[i];
                        ref var c1 = ref Colliders.Get(e1);
                        ref var t1 = ref Transforms.Get(e1);
                        ref var b1 = ref Bodies.Get(e1);
                        // circle vs rect
                        for (var j = 0; j < cell2.RectanglesBuffer.Count; j++)
                        {
                            var e2 = cell2.RectanglesBuffer[j];
                            if (e1 == e2) continue;
                            ref var rect = ref Rectangles.Get(e2);
                            ref var rectTransform = ref Transforms.Get(e2);
                            if ((c1.collideWith & rect.layer) == rect.layer &&
                                CircleRectangleCollision(in c1, in t1, in rect, in rectTransform))
                            {
                                ulong collisionKey = GetCollisionKey(e1, e2);
                                if (!ProcessedCollisions.Add(collisionKey)) continue;

                                c1.collided = true;
                                rect.index = e2;

                                HitInfo hitInfo = default;
                                ResolveCircleRectangleCollisionGpt4(ref c1, ref t1, ref b1, ref rect, ref rectTransform, ref hitInfo);
                                hitInfo.From = e1;
                                hitInfo.To = e2;
                                CollisionEnterHits.Enqueue(hitInfo);
                                
                                // ref var buffer1 = ref CollisionData.Get(e1);
                                // ref var buffer2 = ref CollisionData.Get(e2);
                                // ref var ent1 = ref World.GetEntity(e1);
                                // ref var ent2 = ref World.GetEntity(e2);
                                // buffer1.AddParallel(new Collision2DData()
                                // {
                                //     Other = ent2,
                                //     Type = hitInfo.Type,
                                //     Position = hitInfo.Pos,
                                //     Normal = hitInfo.Normal
                                // });
                                //
                                // buffer2.AddParallel(new Collision2DData()
                                // {
                                //     Other = ent1,
                                //     Type = hitInfo.Type,
                                //     Position = hitInfo.Pos,
                                //     Normal = hitInfo.Normal
                                // });
                                //
                                // ent1.Add(new CollidedFlag());
                                // ent2.Add(new CollidedFlag());
                            }
                        }
                        // circle vs circle
                        for (var j = 0; j < cell2.CollidersBuffer.Count; j++)
                        {
                            var e2 = cell2.CollidersBuffer[j];
                            if (e1 == e2) continue;

                            ref var c2 = ref Colliders.Get(e2);
                            ref var t2 = ref Transforms.Get(e2);
                            if ((c1.collideWith & c2.layer) == c2.layer)
                            {
                                var distance = math.distance(t1.Position.xy, t2.Position.xy);
                                if (c1.radius + c2.radius >= distance)
                                {
                                    ulong collisionKey = GetCollisionKey(e1, e2);
                                    if (!ProcessedCollisions.Add(collisionKey)) continue;

                                    c1.collided = true;
                                    c2.collided = true;
                                    c1.index = e1;
                                    c2.index = e2;
                                    ref var b2 = ref Bodies.Get(e2);
                                    HitInfo hitInfo = default;
                                    ResolveCollisionInternal(ref c1, ref c2, distance, ref b1, ref b2, ref t1, ref t2,
                                        ref hitInfo);
                                    hitInfo.From = e1;
                                    hitInfo.To = e2;
                                    if (((c1.WriteHitsWithSameLayer || c2.WriteHitsWithSameLayer) && c1.layer == c2.layer) || c1.layer != c2.layer)
                                    {
                                        CollisionEnterHits.Enqueue(hitInfo);
                                    }



                                    // ref var buffer1 = ref CollisionData.Get(e1);
                                    // ref var buffer2 = ref CollisionData.Get(e2);
                                    // ref var ent1 = ref World.GetEntity(e1);
                                    // ref var ent2 = ref World.GetEntity(e2);
                                    // buffer1.AddParallel(new Collision2DData
                                    // {
                                    //     Other = ent2,
                                    //     Type = hitInfo.Type,
                                    //     Position = hitInfo.Pos,
                                    //     Normal = hitInfo.Normal
                                    // });
                                    //
                                    // buffer2.AddParallel(new Collision2DData
                                    // {
                                    //     Other = ent1,
                                    //     Type = hitInfo.Type,
                                    //     Position = hitInfo.Pos,
                                    //     Normal = hitInfo.Normal
                                    // });
                                    //
                                    // ent1.Add(new CollidedFlag());
                                    // ent2.Add(new CollidedFlag());
                                }
                            }
                        }


                    }

                    // rect vs rect
                    for (var i = 0; i < cell1.RectanglesBuffer.Count; i++)
                    {
                        var e1 = cell1.RectanglesBuffer[i];
                        ref var r1 = ref Rectangles.Get(e1);
                        ref var t1 = ref Transforms.Get(e1);
                        ref var b1 = ref Bodies.Get(e1);

                        for (var j = 0; j < cell2.RectanglesBuffer.Count; j++)
                        {
                            var e2 = cell2.RectanglesBuffer[j];
                            if (e1 == e2) continue;

                            ref var r2 = ref Rectangles.Get(e2);
                            ref var t2 = ref Transforms.Get(e2);
                            if ((r1.collideWith & r2.layer) == r2.layer)
                            {
                                ulong collisionKey = GetCollisionKey(e1, e2);
                                if (!ProcessedCollisions.Add(collisionKey)) continue;

                                r1.index = e1;
                                r2.index = e2;

                                var hitInfo = ResolveCollisionRectVsRectInternal(in r1, in t1, in r2, in t2, ref b1);
                                if (hitInfo.Normal.x != 0 || hitInfo.Normal.y != 0)
                                {
                                    hitInfo.From = e1;
                                    hitInfo.To = e2;
                                    CollisionEnterHits.Enqueue(hitInfo);
                                    // ref var buffer1 = ref CollisionData.Get(e1);
                                    // ref var buffer2 = ref CollisionData.Get(e2);
                                    // ref var ent1 = ref World.GetEntity(e1);
                                    // ref var ent2 = ref World.GetEntity(e2);
                                    // buffer1.AddParallel(new Collision2DData()
                                    // {
                                    //     Other = ent2,
                                    //     Type = hitInfo.Type,
                                    //     Position = hitInfo.Pos,
                                    //     Normal = hitInfo.Normal
                                    // });
                                    //
                                    // buffer2.AddParallel(new Collision2DData()
                                    // {
                                    //     Other = ent1,
                                    //     Type = hitInfo.Type,
                                    //     Position = hitInfo.Pos,
                                    //     Normal = hitInfo.Normal
                                    // });
                                    //
                                    // ent1.Add(new CollidedFlag());
                                    // ent2.Add(new CollidedFlag());
                                }
                            }
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
        private static void ResolveCircleRectangleCollisionGpt3(
            ref Circle2D circle,
            ref Transform circleTransform,
            ref Body2D circleBody,
            ref Rectangle2D rectangle,
            ref Transform rectangleTransform,
            ref HitInfo hit)
        {
            var circlePos = circleTransform.Position;
            var rectCenter = rectangleTransform.Position;
            var invRotation = math.inverse(rectangleTransform.Rotation);

            // Transform the circle position to the local space of the rectangle
            var localCirclePos = math.mul(invRotation, new float3(circlePos - rectCenter)).xy;

            var halfWidth = rectangle.w * 0.5f;
            var halfHeight = rectangle.h * 0.5f;

            // Find the closest point on the rectangle in local space
            var clamped = math.clamp(localCirclePos, new float2(-halfWidth, -halfHeight),
                new float2(halfWidth, halfHeight));
            var localDelta = localCirclePos - clamped;
            var distSqr = math.lengthsq(localDelta);
            var radius = circle.radius;

            // If there is no collision, exit
            if (distSqr > radius * radius)
                return;

            var dist = math.sqrt(distSqr);
            float2 localNormal;
            float2 localMtv;
            if (dist != 0f)
            {
                localNormal = localDelta / dist;
                localMtv = localNormal * (radius - dist);
            }
            else
            {
                // The center of the circle inside the rectangle; looking for the nearest edge 
                var distToLeft = -localCirclePos.x - halfWidth;
                var distToRight = localCirclePos.x - halfWidth;
                var distToBottom = -localCirclePos.y - halfHeight;
                var distToTop = localCirclePos.y - halfHeight;

                var minDist = math.min(math.min(distToLeft, distToRight), math.min(distToBottom, distToTop));

                if (minDist == distToLeft) localNormal = new float2(-1, 0);
                else if (minDist == distToRight) localNormal = new float2(1, 0);
                else if (minDist == distToBottom) localNormal = new float2(0, -1);
                else localNormal = new float2(0, 1);

                localMtv = localNormal * radius;
            }

            // Transform the normal and MTV to world space
            var worldMtv = math.mul(rectangleTransform.Rotation, new float3(localMtv, 0f));
            var worldNormal = math.normalize(worldMtv);

            // Transform the contact point to world space
            var contactPointWorld = (rectCenter + math.mul(rectangleTransform.Rotation, new float3(clamped, 0f))).xy;

            // Allow intersection by moving the circle
            circleTransform.Position += worldMtv;
            var worldNormal2 = worldNormal.xy;
            // Adjust the velocity to prevent penetration
            var projection = math.dot(circleBody.velocity, worldNormal2);
            if (projection < 0f)
                circleBody.velocity -= projection * worldNormal2;

            circle.collided = true;

            hit.Pos = contactPointWorld;
            hit.Normal = worldNormal2;
            hit.From = circle.index;
            hit.To = rectangle.index;
        }
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Default)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ResolveCircleRectangleCollisionGpt4(
            ref Circle2D circle,
            ref Transform circleTransform,
            ref Body2D circleBody,
            ref Rectangle2D rectangle,
            ref Transform rectangleTransform,
            ref HitInfo hit)
        {
            var circlePos = circleTransform.Position.xy;
            var rectCenter = rectangleTransform.Position.xy;
            var invRotation = math.inverse(rectangleTransform.Rotation);

            // Преобразуем позицию круга в локальное пространство прямоугольника
            var localCirclePos = math.mul(invRotation, new float3(circlePos - rectCenter, 0f)).xy;

            var halfWidth = rectangle.w * 0.5f;
            var halfHeight = rectangle.h * 0.5f;

            // Находим ближайшую точку на прямоугольнике в локальном пространстве
            var clamped = math.clamp(localCirclePos, new float2(-halfWidth, -halfHeight), new float2(halfWidth, halfHeight));
            var localDelta = localCirclePos - clamped;
            var distSqr = math.lengthsq(localDelta);
            var radius = circle.radius;

            // Проверяем наличие столкновения
            if (distSqr > radius * radius)
                return;

            float2 localNormal;
            float2 localMtv;
            float dist = math.sqrt(distSqr);

            if (dist > 0f)
            {
                // Круг снаружи: нормаль от круга к прямоугольнику
                localNormal = localDelta / dist;
                localMtv = localNormal * (radius - dist) * 1.05f; // Усиление для устранения проникновения
            }
            else
            {
                // Круг внутри: ищем ближайшую грань
                var distToLeft = halfWidth + localCirclePos.x;
                var distToRight = halfWidth - localCirclePos.x;
                var distToBottom = halfHeight + localCirclePos.y;
                var distToTop = halfHeight - localCirclePos.y;

                var minDist = math.min(math.min(distToLeft, distToRight), math.min(distToBottom, distToTop));

                if (minDist == distToLeft)
                {
                    localNormal = new float2(-1f, 0f);
                    localMtv = new float2(-distToLeft - radius, 0f) * 1.05f;
                }
                else if (minDist == distToRight)
                {
                    localNormal = new float2(1f, 0f);
                    localMtv = new float2(distToRight + radius, 0f) * 1.05f;
                }
                else if (minDist == distToBottom)
                {
                    localNormal = new float2(0f, -1f);
                    localMtv = new float2(0f, -distToBottom - radius) * 1.05f;
                }
                else // distToTop
                {
                    localNormal = new float2(0f, 1f);
                    localMtv = new float2(0f, distToTop + radius) * 1.05f;
                }
            }

            // Преобразуем нормаль и MTV в мировое пространство
            var worldMtv = math.mul(rectangleTransform.Rotation, new float3(localMtv, 0f)).xy;
            var worldNormal = math.mul(rectangleTransform.Rotation, new float3(localNormal, 0f)).xy;
            if (math.lengthsq(worldNormal) > 0f)
                worldNormal = math.normalize(worldNormal);

            // Точка контакта в мировом пространстве
            var contactPointWorld = rectCenter + math.mul(rectangleTransform.Rotation, new float3(clamped, 0f)).xy;

            // Перемещаем круг наружу
            circleTransform.Position += new float3(worldMtv, 0f);

            // Корректируем скорость
            var projection = math.dot(circleBody.velocity, worldNormal);
            if (projection < 0f) // Только если круг движется к прямоугольнику
            {
                const float friction = 0.7f;
                circleBody.velocity -= projection * worldNormal; // Убираем нормальную составляющую
                var tangent = new float2(-worldNormal.y, worldNormal.x);
                var tangentProjection = math.dot(circleBody.velocity, tangent);
                circleBody.velocity -= tangentProjection * friction * tangent; // Применяем трение
            }

            // Устанавливаем флаги и HitInfo
            circle.collided = true;
            hit.Pos = contactPointWorld;
            hit.Normal = worldNormal;
            hit.From = circle.index;
            hit.To = rectangle.index;
        }
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Default)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CircleRectangleCollision(in Circle2D circle, in Transform circleTransform,
            in Rectangle2D rectangle2D,
            in Transform rectTransform)
        {
            // Преобразуем позицию круга в локальное пространство прямоугольника
            float3 circlePos = circleTransform.Position;
            float3 rectCenter = rectTransform.Position;

            // Вычисляем обратное вращение
            quaternion inverseRotation = math.inverse(rectTransform.Rotation);
            float2 localCirclePos = math.mul(inverseRotation, circlePos - rectCenter).xy;

            // Проверяем столкновение в локальном пространстве (без вращения)
            float halfW = rectangle2D.w * 0.5f;
            float halfH = rectangle2D.h * 0.5f;

            // Находим ближайшую точку на прямоугольнике в локальном пространстве
            float closestX = math.clamp(localCirclePos.x, -halfW, halfW);
            float closestY = math.clamp(localCirclePos.y, -halfH, halfH);

            // Вычисляем расстояние от центра круга до ближайшей точки
            float deltaX = localCirclePos.x - closestX;
            float deltaY = localCirclePos.y - closestY;
            float distanceSquared = deltaX * deltaX + deltaY * deltaY;

            return distanceSquared <= circle.radius * circle.radius;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Default)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResolveCollisionInternal2(
            ref Circle2D circle1, 
            ref Circle2D circle2, 
            float distance,
            ref Body2D b1, 
            ref Body2D b2, 
            ref Transform t1, 
            ref Transform t2, 
            ref HitInfo hitInfo)
        {
            var delta = t2.Position - t1.Position;
            var normal = math.normalize(delta);
            var depth = circle1.radius + circle2.radius - distance;
            var normal2d = new float2(normal.x, normal.y);

            if (!(circle1.trigger || circle2.trigger))
            {
                // Разрешаем пересечение, двигая оба круга
                var mtv = normal * (depth * 0.5f);
                t1.Position -= mtv;
                t2.Position += mtv;

                // Корректируем скорости
                var v1 = math.dot(b1.velocity, normal2d);
                var v2 = math.dot(b2.velocity, normal2d);
                if (v1 > v2) // Только если круги движутся навстречу
                {
                    var restitution = 0.0f; // Настрой, если нужна упругость (0 = неупругое, 1 = упругое)
                    var impulse = -(1 + restitution) * (v1 - v2) * 0.5f;
                    b1.velocity += impulse * normal2d;
                    b2.velocity -= impulse * normal2d;
                }
            }

            hitInfo = new HitInfo
            {
                Pos = circle1.position + normal2d * circle1.radius,
                Normal = normal2d,
                From = circle1.index,
                To = circle2.index
            };
        }

        [BurstCompile]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResolveCollisionInternal(ref Circle2D circle1, ref Circle2D circle2, float distance,
            ref Body2D b1, ref Body2D b2, ref Transform t1, ref Transform t2, ref HitInfo hitInfo)
        {
            var delta = t2.Position - t1.Position;
            var normal = math.normalize(delta);
            var depth = circle1.radius + circle2.radius - distance;
            var normal2d = new float2(normal.x, normal.y);
            if (!(circle1.trigger || circle2.trigger))
            {
                var z = t1.Position.z;
                t1.Position -= normal * depth * 0.5F;
                t2.Position += normal * depth * 0.5f;
                t1.Position.z = z;
                t2.Position.z = z;
            }
            
            hitInfo = new HitInfo
            {
                Pos = circle1.position + normal2d * circle1.radius,
                Normal = normal2d,
                From = circle1.index,
                To = circle2.index
            };
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Default)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private HitInfo ResolveCollisionRectVsRectInternal(
            in Rectangle2D rect1,
            in Transform rect1Transform,
            in Rectangle2D rect2,
            in Transform rect2Transform,
            ref Body2D rect1Body)
        {
            // Получаем вершины обоих прямоугольников
            rect1.GetVertices(rect1Transform, out var r1V0, out var r1V1, out var r1V2, out var r1V3);
            rect2.GetVertices(rect2Transform, out var r2V0, out var r2V1, out var r2V2, out var r2V3);

            // Оси для проверки (нормали сторон)
            var axis0 = math.normalize(r1V1 - r1V0); // Нижняя грань rect1
            var axis1 = math.normalize(r1V2 - r1V1); // Правая грань rect1
            var axis2 = math.normalize(r2V1 - r2V0); // Нижняя грань rect2
            var axis3 = math.normalize(r2V2 - r2V1); // Правая грань rect2

            var minOverlap = float.MaxValue;
            var collisionNormal = float2.zero;
            var collisionDetected = true;

            // Проверяем все оси разделения
            for (int i = 0; i < 4; i++)
            {
                float2 axis = i switch
                {
                    0 => axis0,
                    1 => axis1,
                    2 => axis2,
                    _ => axis3
                };

                // Проецируем вершины на ось
                var r1Min = math.min(math.min(math.dot(r1V0, axis), math.dot(r1V1, axis)),
                    math.min(math.dot(r1V2, axis), math.dot(r1V3, axis)));
                var r1Max = math.max(math.max(math.dot(r1V0, axis), math.dot(r1V1, axis)),
                    math.max(math.dot(r1V2, axis), math.dot(r1V3, axis)));
                var r2Min = math.min(math.min(math.dot(r2V0, axis), math.dot(r2V1, axis)),
                    math.min(math.dot(r2V2, axis), math.dot(r2V3, axis)));
                var r2Max = math.max(math.max(math.dot(r2V0, axis), math.dot(r2V1, axis)),
                    math.max(math.dot(r2V2, axis), math.dot(r2V3, axis)));

                // Проверяем пересечение проекций
                if (r1Max <= r2Min || r2Max <= r1Min)
                {
                    collisionDetected = false;
                    break;
                }

                // Вычисляем пересечение
                var overlap = math.min(r1Max - r2Min, r2Max - r1Min);
                if (overlap < minOverlap)
                {
                    minOverlap = overlap;
                    collisionNormal = axis;

                    // Корректируем направление нормали
                    var center1 = rect1Transform.Position.xy;
                    var center2 = rect2Transform.Position.xy;
                    if (math.dot(center2 - center1, axis) < 0)
                        collisionNormal = -collisionNormal;
                }
            }

            if (!collisionDetected)
            {
                return new HitInfo { From = rect1.index, To = rect2.index };
            }

            // Если не триггер - применяем разрешение коллизии
            if (!rect1.trigger && !rect2.trigger && minOverlap > 0)
            {
                rect1Body.velocity += collisionNormal * minOverlap;
            }

            return new HitInfo
            {
                Pos = (rect1Transform.Position.xy + rect2Transform.Position.xy) * 0.5f,
                Normal = collisionNormal,
                From = rect1.index,
                To = rect2.index
            };
        }

    }

}
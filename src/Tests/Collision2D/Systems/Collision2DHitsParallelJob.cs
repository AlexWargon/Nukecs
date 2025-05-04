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
        public UnsafeList<Grid2DCell> Cells;
        public ComponentPool<Circle2D> Colliders;
        public ComponentPool<Transform> Transforms;
        public ComponentPool<Rectangle2D> Rectangles;
        public ComponentPool<Body2D> Bodies;
        public ComponentPool<ComponentArray<Collision2DData>> CollisionData;
        [WriteOnly] public NativeQueue<HitInfo>.ParallelWriter CollisionEnterHits;
        public float2 Offset, GridPosition;
        public int W, H, cellSize;
        public World world;
        [WriteOnly] public NativeParallelHashSet<ulong>.ParallelWriter ProcessedCollisions;

        public void Execute(int idx) {
            var x = idx % W;
            var y = idx / W;

            ref var cell1 = ref Cells.ElementAt(idx);
            cell1.Pos = new float2(x * cellSize, y * cellSize) + Offset + GridPosition;

            for (var dx = -1; dx <= 1; ++dx)
            for (var dy = -1; dy <= 1; ++dy) {
                var di = W * (y + dy) + x + dx;
                if (di < 0 || di >= Cells.m_length) continue;
                
                var cell2 = Cells[di];
                

                for (var i = 0; i < cell1.CollidersBuffer.Count; i++) {
                    var e1 = cell1.CollidersBuffer[i];
                    ref var c1 = ref Colliders.Get(e1);
                    ref var t1 = ref Transforms.Get(e1);
                    ref var b1 = ref Bodies.Get(e1);

                    // circle vs circle
                    for (var j = 0; j < cell2.CollidersBuffer.Count; j++) {
                        var e2 = cell2.CollidersBuffer[j];
                        if (e1 == e2) continue;

                        ref var c2 = ref Colliders.Get(e2);
                        ref var t2 = ref Transforms.Get(e2);
                        if ((c1.collideWith & c2.layer) == c2.layer)
                        {
                            var distance = math.distance(t1.Position, t2.Position);
                            if (c1.radius + c2.radius >= distance) {
                                ulong collisionKey = GetCollisionKey(e1, e2);
                                if (!ProcessedCollisions.Add(collisionKey)) continue;
                                
                                c1.collided = true;
                                c2.collided = true;
                                c1.index = e1;
                                c2.index = e2;
                                ref var b2 = ref Bodies.Get(e2);
                                HitInfo hitInfo = default;
                                ResolveCollisionInternal(ref c1, ref c2, distance, ref b1, ref b2, ref t1, ref t2, ref hitInfo);
                                CollisionEnterHits.Enqueue(hitInfo);
                                
                                ref var buffer1 = ref CollisionData.Get(e1);
                                ref var buffer2 = ref CollisionData.Get(e2);
                                ref var ent1 = ref world.GetEntity(e1);
                                ref var ent2 = ref world.GetEntity(e2);
                                buffer1.AddParallel(new Collision2DData {
                                    Other = ent2,
                                    Type = hitInfo.Type,
                                    Position = hitInfo.Pos,
                                    Normal = hitInfo.Normal
                                });
                                
                                buffer2.AddParallel(new Collision2DData {
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

                    // circle vs rect
                    for (var j = 0; j < cell2.RectanglesBuffer.Count; j++) {
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
                            var hitInfo = ResolveCollisionCircleVsRectInternal(ref c1, ref b1, in rect, in rectTransform);
                            CollisionEnterHits.Enqueue(hitInfo);
                            
                            ref var buffer1 = ref CollisionData.Get(e1);
                            ref var buffer2 = ref CollisionData.Get(e2);
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

                // rect vs rect
                for (var i = 0; i < cell1.RectanglesBuffer.Count; i++) {
                    var e1 = cell1.RectanglesBuffer[i];
                    ref var r1 = ref Rectangles.Get(e1);
                    ref var t1 = ref Transforms.Get(e1);
                    ref var b1 = ref Bodies.Get(e1);

                    for (var j = 0; j < cell2.RectanglesBuffer.Count; j++) {
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
                            if (hitInfo.Normal.x != 0 || hitInfo.Normal.y != 0) // Проверка на наличие коллизии
                            {
                                CollisionEnterHits.Enqueue(hitInfo);
                                
                                ref var buffer1 = ref CollisionData.Get(e1);
                                ref var buffer2 = ref CollisionData.Get(e2);
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
        private HitInfo ResolveCollisionCircleVsRectInternal(ref Circle2D circle, ref Body2D circleBody, in Rectangle2D rect, in Transform rectTransform)
        {
            float rectLeft = rectTransform.Position.x - rect.w / 2f;
            float rectBottom = rectTransform.Position.y - rect.h / 2f;

            var closestX = math.max(rectLeft, 
                math.min(circle.position.x, rectLeft + rect.w));
            var closestY = math.max(rectBottom, 
                math.min(circle.position.y, rectBottom + rect.h));

            var deltaX = circle.position.x - closestX;
            var deltaY = circle.position.y - closestY;
            
            float distance;
            if (deltaX == 0 && deltaY == 0)
            {
                distance = 0.0f;
            }
            else
            {
                distance = math.sqrt(deltaX * deltaX + deltaY * deltaY);
            }

            var overlap = circle.radius - distance;
            float2 normal = default;
            if (distance != 0)
            {
                normal = new float2(deltaX / distance, deltaY / distance);
            }
            else if (overlap > 0)
            {

                float distToLeft = circle.position.x - rectLeft;
                float distToRight = (rectLeft + rect.w) - circle.position.x;
                float distToBottom = circle.position.y - rectBottom;
                float distToTop = (rectBottom + rect.h) - circle.position.y;
                
                float minDist = math.min(math.min(distToLeft, distToRight),
                                       math.min(distToBottom, distToTop));
                
                if (minDist == distToLeft) normal = new float2(-1, 0);
                else if (minDist == distToRight) normal = new float2(1, 0);
                else if (minDist == distToBottom) normal = new float2(0, -1);
                else normal = new float2(0, 1);
            }

            if (!rect.trigger && !circle.trigger && overlap > 0)
            {
                circleBody.velocity += normal * overlap;
            }
            
            return new HitInfo {
                Pos = new float2(closestX, closestY),
                Normal = normal,
                From = circle.index,
                To = rect.index
            };
        }
        // [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Default)]
        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // private static bool CircleRectangleCollision(in Circle2D circle, in Rectangle2D rectangle2D,
        //     in Transform rectTransform) {
        //     var closestX = math.max(rectTransform.Position.x,
        //         math.min(circle.position.x, rectTransform.Position.x + rectangle2D.w));
        //     var closestY = math.max(rectTransform.Position.y,
        //         math.min(circle.position.y, rectTransform.Position.y + rectangle2D.h));
        //     var distanceSquared = math.distancesq(circle.position, new float2(closestX, closestY));
        //     return distanceSquared <= circle.radius * circle.radius;
        // }
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Default)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        private static bool CircleRectangleCollision(in Circle2D circle, in Transform circleTransform, in Rectangle2D rectangle2D,
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
            
            hitInfo = new HitInfo {
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
            rect1.GetVertices(rect1Transform, out float2 r1v0, out float2 r1v1, out float2 r1v2, out float2 r1v3);
            rect2.GetVertices(rect2Transform, out float2 r2v0, out float2 r2v1, out float2 r2v2, out float2 r2v3);

            // Оси для проверки (нормали сторон)
            float2 axis0 = math.normalize(r1v1 - r1v0); // Нижняя грань rect1
            float2 axis1 = math.normalize(r1v2 - r1v1); // Правая грань rect1
            float2 axis2 = math.normalize(r2v1 - r2v0); // Нижняя грань rect2
            float2 axis3 = math.normalize(r2v2 - r2v1); // Правая грань rect2

            float minOverlap = float.MaxValue;
            float2 collisionNormal = float2.zero;
            bool collisionDetected = true;

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
                float r1min = math.min(math.min(math.dot(r1v0, axis), math.dot(r1v1, axis)),
                                     math.min(math.dot(r1v2, axis), math.dot(r1v3, axis)));
                float r1max = math.max(math.max(math.dot(r1v0, axis), math.dot(r1v1, axis)),
                                     math.max(math.dot(r1v2, axis), math.dot(r1v3, axis)));
                float r2min = math.min(math.min(math.dot(r2v0, axis), math.dot(r2v1, axis)),
                                     math.min(math.dot(r2v2, axis), math.dot(r2v3, axis)));
                float r2max = math.max(math.max(math.dot(r2v0, axis), math.dot(r2v1, axis)),
                                     math.max(math.dot(r2v2, axis), math.dot(r2v3, axis)));

                // Проверяем пересечение проекций
                if (r1max <= r2min || r2max <= r1min)
                {
                    collisionDetected = false;
                    break;
                }

                // Вычисляем пересечение
                float overlap = math.min(r1max - r2min, r2max - r1min);
                if (overlap < minOverlap)
                {
                    minOverlap = overlap;
                    collisionNormal = axis;
                    
                    // Корректируем направление нормали
                    float2 center1 = rect1Transform.Position.xy;
                    float2 center2 = rect2Transform.Position.xy;
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


    [BurstCompile]
    public struct Collision2DHitsParallelJobBatched : IJobParallelForBatch {
        public UnsafeList<Grid2DCell> Cells;
        public ComponentPool<Circle2D> Colliders;
        public ComponentPool<Transform> Transforms;
        public ComponentPool<Rectangle2D> Rectangles;
        public ComponentPool<Body2D> Bodies;
        public ComponentPool<ComponentArray<Collision2DData>> CollisionData;
        [WriteOnly] public NativeQueue<HitInfo>.ParallelWriter CollisionEnterHits;
        public float2 Offset, GridPosition;
        public int W, H, cellSize;
        public World world;
        [WriteOnly] public NativeParallelHashSet<ulong>.ParallelWriter ProcessedCollisions;

        public void Execute(int startIndex, int count)
        {
            for (var idx = startIndex; idx < startIndex + count; idx++)
            {
                var x = idx % W;
                var y = idx / W;

                ref var cell1 = ref Cells.ElementAtNoCheck(idx);
                cell1.Pos = new float2(x * cellSize, y * cellSize) + Offset + GridPosition;

                for (var dx = -1; dx <= 1; ++dx)
                for (var dy = -1; dy <= 1; ++dy) {
                    var di = W * (y + dy) + x + dx;
                    if (di < 0 || di >= Cells.m_length) continue;
                    
                    var cell2 = Cells[di];
                    

                    for (var i = 0; i < cell1.CollidersBuffer.Count; i++) {
                        var e1 = cell1.CollidersBuffer[i];
                        ref var c1 = ref Colliders.Get(e1);
                        ref var t1 = ref Transforms.Get(e1);
                        ref var b1 = ref Bodies.Get(e1);

                        // circle vs circle
                        for (var j = 0; j < cell2.CollidersBuffer.Count; j++) {
                            var e2 = cell2.CollidersBuffer[j];
                            if (e1 == e2) continue;

                            ref var c2 = ref Colliders.Get(e2);
                            ref var t2 = ref Transforms.Get(e2);
                            if ((c1.collideWith & c2.layer) == c2.layer)
                            {
                                var distance = math.distance(t1.Position, t2.Position);
                                if (c1.radius + c2.radius >= distance) {
                                    ulong collisionKey = GetCollisionKey(e1, e2);
                                    if (!ProcessedCollisions.Add(collisionKey)) continue;
                                    
                                    c1.collided = true;
                                    c2.collided = true;
                                    c1.index = e1;
                                    c2.index = e2;
                                    ref var b2 = ref Bodies.Get(e2);
                                    HitInfo hitInfo = default;
                                    ResolveCollisionInternal(ref c1, ref c2, distance, ref b1, ref b2, ref t1, ref t2, ref hitInfo);
                                    CollisionEnterHits.Enqueue(hitInfo);
                                    
                                    ref var buffer1 = ref CollisionData.Get(e1);
                                    ref var buffer2 = ref CollisionData.Get(e2);
                                    ref var ent1 = ref world.GetEntity(e1);
                                    ref var ent2 = ref world.GetEntity(e2);
                                    buffer1.AddParallel(new Collision2DData {
                                        Other = ent2,
                                        Type = hitInfo.Type,
                                        Position = hitInfo.Pos,
                                        Normal = hitInfo.Normal
                                    });
                                    
                                    buffer2.AddParallel(new Collision2DData {
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

                        // circle vs rect
                        for (var j = 0; j < cell2.RectanglesBuffer.Count; j++) {
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
                                var hitInfo = ResolveCollisionCircleVsRectInternal(ref c1, ref b1, in rect, in rectTransform);
                                CollisionEnterHits.Enqueue(hitInfo);
                                
                                ref var buffer1 = ref CollisionData.Get(e1);
                                ref var buffer2 = ref CollisionData.Get(e2);
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

                    // rect vs rect
                    for (var i = 0; i < cell1.RectanglesBuffer.Count; i++) {
                        var e1 = cell1.RectanglesBuffer[i];
                        ref var r1 = ref Rectangles.Get(e1);
                        ref var t1 = ref Transforms.Get(e1);
                        ref var b1 = ref Bodies.Get(e1);

                        for (var j = 0; j < cell2.RectanglesBuffer.Count; j++) {
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
                                if (hitInfo.Normal.x != 0 || hitInfo.Normal.y != 0) // Проверка на наличие коллизии
                                {
                                    CollisionEnterHits.Enqueue(hitInfo);
                                    
                                    ref var buffer1 = ref CollisionData.Get(e1);
                                    ref var buffer2 = ref CollisionData.Get(e2);
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
                    }
                }
            }
        }
        public void Execute(int idx) {
            
            
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
        private HitInfo ResolveCollisionCircleVsRectInternal(ref Circle2D circle, ref Body2D circleBody, in Rectangle2D rect, in Transform rectTransform)
        {
            float rectLeft = rectTransform.Position.x - rect.w / 2f;
            float rectBottom = rectTransform.Position.y - rect.h / 2f;

            var closestX = math.max(rectLeft, 
                math.min(circle.position.x, rectLeft + rect.w));
            var closestY = math.max(rectBottom, 
                math.min(circle.position.y, rectBottom + rect.h));

            var deltaX = circle.position.x - closestX;
            var deltaY = circle.position.y - closestY;
            
            float distance;
            if (deltaX == 0 && deltaY == 0)
            {
                distance = 0.0f;
            }
            else
            {
                distance = math.sqrt(deltaX * deltaX + deltaY * deltaY);
            }

            var overlap = circle.radius - distance;
            float2 normal = default;
            if (distance != 0)
            {
                normal = new float2(deltaX / distance, deltaY / distance);
            }
            else if (overlap > 0)
            {

                float distToLeft = circle.position.x - rectLeft;
                float distToRight = (rectLeft + rect.w) - circle.position.x;
                float distToBottom = circle.position.y - rectBottom;
                float distToTop = (rectBottom + rect.h) - circle.position.y;
                
                float minDist = math.min(math.min(distToLeft, distToRight),
                                       math.min(distToBottom, distToTop));
                
                if (minDist == distToLeft) normal = new float2(-1, 0);
                else if (minDist == distToRight) normal = new float2(1, 0);
                else if (minDist == distToBottom) normal = new float2(0, -1);
                else normal = new float2(0, 1);
            }

            if (!rect.trigger && !circle.trigger && overlap > 0)
            {
                circleBody.velocity += normal * overlap;
            }
            
            return new HitInfo {
                Pos = new float2(closestX, closestY),
                Normal = normal,
                From = circle.index,
                To = rect.index
            };
        }
        // [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Default)]
        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        // private static bool CircleRectangleCollision(in Circle2D circle, in Rectangle2D rectangle2D,
        //     in Transform rectTransform) {
        //     var closestX = math.max(rectTransform.Position.x,
        //         math.min(circle.position.x, rectTransform.Position.x + rectangle2D.w));
        //     var closestY = math.max(rectTransform.Position.y,
        //         math.min(circle.position.y, rectTransform.Position.y + rectangle2D.h));
        //     var distanceSquared = math.distancesq(circle.position, new float2(closestX, closestY));
        //     return distanceSquared <= circle.radius * circle.radius;
        // }
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Default)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]

        private static bool CircleRectangleCollision(in Circle2D circle, in Transform circleTransform, in Rectangle2D rectangle2D,
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
            
            hitInfo = new HitInfo {
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
            rect1.GetVertices(rect1Transform, out float2 r1v0, out float2 r1v1, out float2 r1v2, out float2 r1v3);
            rect2.GetVertices(rect2Transform, out float2 r2v0, out float2 r2v1, out float2 r2v2, out float2 r2v3);

            // Оси для проверки (нормали сторон)
            float2 axis0 = math.normalize(r1v1 - r1v0); // Нижняя грань rect1
            float2 axis1 = math.normalize(r1v2 - r1v1); // Правая грань rect1
            float2 axis2 = math.normalize(r2v1 - r2v0); // Нижняя грань rect2
            float2 axis3 = math.normalize(r2v2 - r2v1); // Правая грань rect2

            float minOverlap = float.MaxValue;
            float2 collisionNormal = float2.zero;
            bool collisionDetected = true;

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
                float r1min = math.min(math.min(math.dot(r1v0, axis), math.dot(r1v1, axis)),
                                     math.min(math.dot(r1v2, axis), math.dot(r1v3, axis)));
                float r1max = math.max(math.max(math.dot(r1v0, axis), math.dot(r1v1, axis)),
                                     math.max(math.dot(r1v2, axis), math.dot(r1v3, axis)));
                float r2min = math.min(math.min(math.dot(r2v0, axis), math.dot(r2v1, axis)),
                                     math.min(math.dot(r2v2, axis), math.dot(r2v3, axis)));
                float r2max = math.max(math.max(math.dot(r2v0, axis), math.dot(r2v1, axis)),
                                     math.max(math.dot(r2v2, axis), math.dot(r2v3, axis)));

                // Проверяем пересечение проекций
                if (r1max <= r2min || r2max <= r1min)
                {
                    collisionDetected = false;
                    break;
                }

                // Вычисляем пересечение
                float overlap = math.min(r1max - r2min, r2max - r1min);
                if (overlap < minOverlap)
                {
                    minOverlap = overlap;
                    collisionNormal = axis;
                    
                    // Корректируем направление нормали
                    float2 center1 = rect1Transform.Position.xy;
                    float2 center2 = rect2Transform.Position.xy;
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
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using Wargon.Nukecs.Transforms;
using Random = Unity.Mathematics.Random;

namespace Wargon.Nukecs.Demos.Boids
{
    public static class BoidsDemo
    {
        [System]
        public static void SpawnBoids(ref State state, ref Res<BoidCount> boidCount)
        {
            int count = boidCount.Ref.Value;
            if (state.World.EntitiesAmount > 0) return;
            
            const float spawnRadius = 8f;
            const float boidScale = 0.2f;
            var entities = state.World.BatchCreateEntity(count);
            var rng = new Random(42);

            for (int i = 0; i < entities.Length; i++)
            {
                ref var e = ref entities[i];
                var pos = new float3(
                    rng.NextFloat(-spawnRadius, spawnRadius),
                    rng.NextFloat(-spawnRadius * 0.5f, spawnRadius * 0.5f),
                    rng.NextFloat(-spawnRadius, spawnRadius));
                e.Add(new LocalTransform
                {
                    Position = pos,
                    Rotation = quaternion.LookRotationSafe(
                        rng.NextFloat3Direction(), math.up()),
                    Scale = new float3(boidScale, boidScale, boidScale)
                });
                e.Add(new Velocity
                {
                    Value = rng.NextFloat3Direction() * rng.NextFloat(1f, 3f)
                });
                e.Add<BoidTag>();
            }
        }

        [System, BurstCompile]
        public static unsafe void BoidsUpdate(
            ref Query<LocalTransform, Velocity, BoidTag> query,
            ref State state,
            ref Res<BoidRenderData> renderData)
        {
            const float separationWeight = 1.5f;
            const float alignmentWeight  = 1.0f;
            const float cohesionWeight   = 1.0f;
            const float perceptionRadius = 3.0f;
            const float maxSpeed         = 5.0f;
            const float minSpeed         = 1.0f;
            const float steeringForce    = 12.0f;
            const float boundsRadius     = 4.0f;

            var count = query.Count;
            if (count == 0) return;

            var dt = state.Time.DeltaTime;
            var perceptionSq = perceptionRadius * perceptionRadius;
            
            var positions  = stackalloc float3[count];
            var velocities = stackalloc float3[count];
            var posPtrs    = stackalloc LocalTransform*[count];
            var velPtrs    = stackalloc Velocity*[count];

            int idx = 0;
            foreach (var (t, v) in query.iter_unsafe())
            {
                positions[idx]  = t->Position;
                velocities[idx] = v->Value;
                posPtrs[idx]    = t;
                velPtrs[idx]    = v;
                idx++;
            }

            var matrices = renderData.Ref.Matrices;
            int matrixIdx = 0;

            for (int i = 0; i < count; i++)
            {
                var sep = float3.zero;
                var ali = float3.zero;
                var coh = float3.zero;
                int neighbors = 0;

                for (int j = 0; j < count; j++)
                {
                    if (j == i) continue;
                    var diff = positions[j] - positions[i];
                    var distSq = math.lengthsq(diff);
                    if (distSq < perceptionSq && distSq > 0.0001f)
                    {
                        var dist = math.sqrt(distSq);
                        sep -= diff / (dist * dist);
                        ali += velocities[j];
                        coh += positions[j];
                        neighbors++;
                    }
                }

                if (neighbors > 0)
                {
                    ali /= neighbors;
                    coh = (coh / neighbors) - positions[i];
                }

                var force = (sep * separationWeight
                           + ali * alignmentWeight
                           + coh * cohesionWeight) * steeringForce;

                var vel = velPtrs[i];
                var pos = posPtrs[i];
                vel->Value += force * dt;

                if (math.length(pos->Position) > boundsRadius)
                    vel->Value -= pos->Position * (dt * 2f);

                var speed = math.length(vel->Value);
                if (speed > maxSpeed)
                    vel->Value = vel->Value / speed * maxSpeed;
                else if (speed < minSpeed && speed > 0.001f)
                    vel->Value = vel->Value / speed * minSpeed;

                pos->Position += vel->Value * dt;

                if (speed > 0.1f)
                    pos->Rotation = quaternion.LookRotation(
                        vel->Value / speed, math.up());

                if (matrices.IsCreated && matrixIdx < matrices.Length)
                    matrices[matrixIdx] = pos->Matrix;
                matrixIdx++;
            }

            renderData.Ref.count = matrixIdx;
        }

        [System]
        public static unsafe void DrawBoids( 
            ref Res<BoidRenderData> renderData,
            ref ResManaged<MeshData> meshData)
        {
            var rd = renderData.Ref;

            if (!rd.Matrices.IsCreated || meshData.Val.Mesh == null || meshData.Val.Material == null) return;
            
            var count = rd.count;
            const int batchMax = 1023;
            for (int batch = 0; batch < count; batch += batchMax)
            {
                var thisBatch = math.min(batchMax, count - batch);
                var slice = rd.Matrices.GetSubArray(batch, thisBatch);
                Graphics.RenderMeshInstanced(
                    new RenderParams(meshData.Val.Material), meshData.Val.Mesh, 0, slice);
            }

            rd.count = 0;
        }
    }
}

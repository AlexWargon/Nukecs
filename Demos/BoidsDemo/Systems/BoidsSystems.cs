using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
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
                e.Add<BoidForce>();
                e.Add<BoidTag>();
            }
        }

        [System, BurstCompile]
        public static unsafe void BoidsCalculateForces(
            ref Query<LocalTransform, Velocity, BoidForce, BoidTag> query,
            ref State state)
        {
            const float separationWeight = 1.5f;
            const float alignmentWeight  = 1.0f;
            const float cohesionWeight   = 1.0f;
            const float perceptionRadius = 3.0f;
            const float steeringForce    = 12.0f;

            var count = query.Count;
            if (count == 0) return;

            var perceptionSq = perceptionRadius * perceptionRadius;
            var positions  = stackalloc float3[count];
            var velocities = stackalloc float3[count];
            var forcePtrs  = stackalloc BoidForce*[count];

            int idx = 0;
            foreach (var (t, v, f) in query.iter_unsafe())
            {
                positions[idx]  = t->Position;
                velocities[idx] = v->Value;
                forcePtrs[idx]  = f;
                idx++;
            }

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

                forcePtrs[i]->Value = (sep * separationWeight
                                     + ali * alignmentWeight
                                     + coh * cohesionWeight) * steeringForce;
            }
        }

        [System, BurstCompile]
        public static unsafe void BoidsApplyMovement(
            ref Query<LocalTransform, Velocity, BoidForce, BoidTag> query,
            ref State state)
        {
            const float maxSpeed     = 5.0f;
            const float minSpeed     = 1.0f;
            const float boundsRadius = 4.0f;
            var dt = state.Time.DeltaTime;
            
            foreach (var (t, v, f) in query.par_iter_unsafe())
            {
                v->Value += f->Value * dt;

                if (math.length(t->Position) > boundsRadius)
                    v->Value -= t->Position * (dt * 2f);

                var speed = math.length(v->Value);
                if (speed > maxSpeed)
                    v->Value = v->Value / speed * maxSpeed;
                else if (speed < minSpeed && speed > 0.001f)
                    v->Value = v->Value / speed * minSpeed;

                t->Position += v->Value * dt;

                speed = math.length(v->Value);
                if (speed > 0.1f)
                    t->Rotation = quaternion.LookRotation(
                        v->Value / speed, math.up());
            }
        }

        [System]
        public static unsafe void DrawBoids(
            ref Query<LocalTransform, BoidTag> query,
            ref Res<BoidRenderData> renderData,
            ref ResManaged<MeshData> meshData)
        {
            var rd = renderData.Ref;

            if (!rd.Matrices.IsCreated || meshData.Val.Mesh == null || meshData.Val.Material == null) return;

            var matrices = rd.Matrices;
            int idx = 0;
            foreach (var (t, _) in query.iter_unsafe())
            {
                if (idx < matrices.Length)
                    matrices[idx] = t->Matrix;
                idx++;
            }
            rd.count = idx;

            const int batchMax = 1023;
            var param = new RenderParams(meshData.Val.Material);
            param.shadowCastingMode = ShadowCastingMode.On;
            for (int batch = 0; batch < rd.count; batch += batchMax)
            {
                var thisBatch = math.min(batchMax, rd.count - batch);
                var slice = rd.Matrices.GetSubArray(batch, thisBatch);
                Graphics.RenderMeshInstanced(param, meshData.Val.Mesh, 0, slice);
            }

            rd.count = 0;
        }
    }
}

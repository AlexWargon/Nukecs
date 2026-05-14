using System.Threading;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using Wargon.Nukecs.Transforms;

namespace Wargon.Nukecs.Demos.CubeSculpture
{
    [BurstCompile]
    public class CubeSculpture : ISystemsGroup
    {
        public void Build(Systems systems, ref World world)
        {
            
            systems
                .Add(Render, Threads.Main)
                .Add(Spawn, Threads.MainRun)
                .Add(FreeMovement)
                .Add(SwarmTransition)
                .Add(SwarmMovement)
                .Add(Assembly)
                .Add(AssemblyMovement)
                .Add(AssemblyComplete)
                .Add(Disassemble)
                .Add(FillMatrix)
                ;
        }
        [System, BurstCompile]
        public static void Spawn(ref State state, ref Res<ConfigData> config)
        {
            var world = state.World;
            var maxCount = config.Ref.TargetCount;
            var remaining = maxCount - world.EntitiesAmount;
            if (remaining <= 0) return;
            if (config.Ref.timer > 0)
            {
                config.Ref.timer -= state.Time.DeltaTime;
                return;
            }

            config.Ref.timer = config.Ref.spawnTime;
            var batch = math.min(config.Ref.SpawnBatchSize, remaining);
            var entities = world.BatchCreateEntity(batch);
            var rng = new random(state.Time.TickCount);
            var scale = config.Ref.CubeScale;

            for (int i = 0; i < entities.Length; i++)
            {
                ref var e = ref entities[i];
                var pos = new float3(
                    rng.NextFloat(-5f, 5f),
                    rng.NextFloat(5f, 15f),
                    rng.NextFloat(-5f, 5f));
                e.Add(new LocalTransform
                {
                    Position = pos,
                    Rotation = quaternion.identity,
                    Scale = new float3(scale, scale, scale)
                });
                e.Add(new Velocity
                {
                    Value = new float3(rng.NextFloat(-1f, 1f), 0f, rng.NextFloat(-1f, 1f))
                });
                e.Add(new CubeStateTag { Value = CubeState.Free });
                e.Add(new FormationOffset { Value = float3.zero });
                e.Add(new AnimationPhase { Time = 0f });
                e.Add(new SculptureSlotIndex { Value = -1 });
            }
        }

        [System, BurstCompile]
        public static void FreeMovement(
            ref Query<LocalTransform, Velocity, CubeStateTag> query, ref State state)
        {
            var dt = state.Time.DeltaTime;
            var time = state.Time.Time;
            var damping = 1f - math.min(0.5f * dt, 0.999f);
            foreach (var (tRef, vRef, tagRef) in query.par_iter())
            {
                ref var tag = ref tagRef.Get;
                if (tag.Value != CubeState.Free) continue;

                ref var vel = ref vRef.Get;
                ref var t = ref tRef.Get;

                var drift = new float3(
                    math.sin(time * 1.5f + t.Position.x * 0.1f),
                    0f,
                    math.cos(time * 1.2f + t.Position.z * 0.1f)
                ) * 0.5f * dt;

                vel.Value.y -= 0.2f * dt;
                vel.Value += drift;
                vel.Value *= damping;

                t.Position += vel.Value * dt;

                if (t.Position.y < -20f)
                {
                    t.Position.y = -20f;
                    vel.Value.y = math.abs(vel.Value.y) * 0.5f;
                }
            }
        }

        [System, BurstCompile]
        public static void SwarmTransition(
            ref Query<
                CubeStateTag, 
                FormationOffset, 
                AnimationPhase> query,
            ref State state, 
            ref Res<ConfigData> config, 
            ref Res<SculptureData> sculpture,
            ref Res<CycleData> cycle)
        {
            if (query.Count < config.Ref.TargetCount) return;
            if (cycle.Ref.Disassembling) return;

            sculpture.Ref.TransitionCounter = 0;
            var totalCount = query.Count;
            var formation = cycle.Ref.SwarmFormation;

            foreach (var (tagRef, offsetRef, phaseRef) in query.par_iter())
            {
                ref var tag = ref tagRef.Get;
                if (tag.Value != CubeState.Free) continue;

                tag.Value = CubeState.Swarm;

                var idx = Interlocked.Increment(ref sculpture.Ref.TransitionCounter) - 1;
                offsetRef.Get.Value = CubeSculpture.FormationPosition(idx, totalCount, formation);
                phaseRef.Get.Time = 0f;
            }
        }

        public static float3 FormationPosition(int idx, int totalCount, int formation)
        {
            switch (formation % 4)
            {
                default:
                {
                    var phi = math.acos(1f - 2f * (idx + 0.5f) / totalCount);
                    var theta = math.PI * (1f + math.sqrt(5f)) * idx;
                    var radius = 15f;
                    return new float3(
                        radius * math.sin(phi) * math.cos(theta),
                        radius * math.cos(phi),
                        radius * math.sin(phi) * math.sin(theta)
                    );
                }
                case 1:
                {
                    var majorRadius = 12f;
                    var minorRadius = 4f;
                    var t = (float)idx / totalCount;
                    var angle = t * math.PI * 2f;
                    var tubeAngle = t * math.PI * 8f;
                    return new float3(
                        (majorRadius + minorRadius * math.cos(tubeAngle)) * math.cos(angle),
                        minorRadius * math.sin(tubeAngle),
                        (majorRadius + minorRadius * math.cos(tubeAngle)) * math.sin(angle)
                    );
                }
                case 2:
                {
                    var radius = 10f;
                    var height = 20f;
                    var strand = idx % 2;
                    var t = (float)(idx / 2) / (totalCount / 2f);
                    var angleOffset = strand * math.PI;
                    var angle = t * math.PI * 6f + angleOffset;
                    return new float3(
                        math.cos(angle) * radius,
                        t * height - height * 0.5f,
                        math.sin(angle) * radius
                    );
                }
                case 3:
                {
                    var gridSize = (int)math.ceil(math.pow(totalCount, 1f / 3f));
                    var gx = idx % gridSize;
                    var gy = (idx / gridSize) % gridSize;
                    var gz = idx / (gridSize * gridSize);
                    var spacing = 2f;
                    var offset = (gridSize - 1) * spacing * 0.5f;
                    return new float3(
                        gx * spacing - offset,
                        gy * spacing - offset,
                        gz * spacing - offset
                    );
                }
            }
        }

        [System, BurstCompile]
        public static void SwarmMovement(
            ref Query<
                LocalTransform, 
                Velocity, 
                CubeStateTag, 
                FormationOffset, 
                AnimationPhase> query,
            ref State state)
        {
            var dt = state.Time.DeltaTime;
            var damping = 1f - math.min(3f * dt, 0.999f);
            foreach (var (tRef, vRef, tagRef, offsetRef, phaseRef) in query.par_iter())
            {
                ref var tag = ref tagRef.Get;
                if (tag.Value != CubeState.Swarm) continue;
                ref var vel = ref vRef.Get;
                ref var t = ref tRef.Get;
                ref var target = ref offsetRef.Get;
                ref var phase = ref phaseRef.Get;

                phase.Time += dt;
                var animTime = phase.Time;

                var amplitude = math.max(0f, 2f - animTime * 0.5f);
                var spiralAngle = animTime * 2f;
                var spiralOffset = new float3(
                    math.cos(spiralAngle) * amplitude,
                    math.sin(animTime * 3f) * amplitude * 0.3f,
                    math.sin(spiralAngle) * amplitude
                );

                var effectiveTarget = target.Value + spiralOffset;

                var toTarget = effectiveTarget - t.Position;
                var dist = math.length(toTarget);
                var dir = dist > 0.001f ? toTarget / dist : float3.zero;

                vel.Value += dir * 8f * dt;
                vel.Value *= damping;
                t.Position += vel.Value * dt;
            }
        }

        [System, BurstCompile]
        public static void Assembly(
            ref Query<
                LocalTransform, 
                CubeStateTag, 
                FormationOffset,
                SculptureSlotIndex, 
                AnimationPhase> query,
            ref State state, 
            ref Res<ConfigData> config, 
            ref Res<SculptureData> sculpture)
        {
            foreach (var (tRef, tagRef, offsetRef, slotRef, phaseRef) in query.par_iter())
            {
                ref var tag = ref tagRef.Get;
                if (tag.Value != CubeState.Swarm) continue;

                ref var t = ref tRef.Get;
                ref var offset = ref offsetRef.Get;
                var distSq = math.lengthsq(t.Position - offset.Value);
                if (distSq > 1f) continue;

                tag.Value = CubeState.Assemble;
                var slot = Interlocked.Increment(ref sculpture.Ref.SlotCounter) - 1;
                slotRef.Get.Value = slot;
                phaseRef.Get.Time = 0f;
            }
        }

        [System, BurstCompile]
        public static void AssemblyMovement(
            ref Query<
                LocalTransform, 
                Velocity, 
                CubeStateTag, 
                SculptureSlotIndex, 
                AnimationPhase> query,
            ref State state,
            ref Res<ConfigData> config,
            ref Res<CycleData> cycle)
        {
            var dt = state.Time.DeltaTime;
            var totalCount = config.Ref.TargetCount;
            var scale = config.Ref.CubeScale;
            var shapeIndex = cycle.Ref.SculptureShape;

            foreach (var (tRef, vRef, tagRef, slotRef, phaseRef) in query.par_iter())
            {
                ref var tag = ref tagRef.Get;
                if (tag.Value != CubeState.Assemble) continue;

                ref var slot = ref slotRef.Get;
                if (slot.Value < 0) continue;

                ref var t = ref tRef.Get;
                ref var vel = ref vRef.Get;
                ref var phase = ref phaseRef.Get;

                phase.Time += dt;
                var animTime = phase.Time;

                var target = SculptureTemplate.GetPosition(slot.Value, totalCount, scale, shapeIndex);

                var toTarget = target - t.Position;
                var distSq = math.lengthsq(toTarget);

                if (distSq < 0.01f)
                {
                    t.Position = target;
                    vel.Value = float3.zero;
                    tag.Value = CubeState.Assembled;
                    continue;
                }

                var dist = math.sqrt(distSq);

                var approachAngle = animTime * 4f + slot.Value * 0.3f;
                var spiralRadius = dist * 0.3f;
                var spiralOffset = new float3(
                    math.cos(approachAngle) * spiralRadius,
                    math.sin(approachAngle * 0.7f) * spiralRadius * 0.5f,
                    math.sin(approachAngle) * spiralRadius
                );

                var effectiveTarget = target + spiralOffset;
                var toEffective = effectiveTarget - t.Position;
                var effectiveDist = math.length(toEffective);
                var effectiveDir = effectiveDist > 0.001f ? toEffective / effectiveDist : float3.zero;

                var speed = math.min(effectiveDist, 15f * dt);
                t.Position += effectiveDir * speed;
                vel.Value = effectiveDir * speed / dt;
            }
        }

        [System, BurstCompile]
        public static void AssemblyComplete(
            ref Query<LocalTransform, CubeStateTag> query,
            ref State state,
            ref Res<ConfigData> config,
            ref Res<CycleData> cycle)
        {
            var dt = state.Time.DeltaTime;
            var time = state.Time.Time;
            var totalCount = config.Ref.TargetCount;

            cycle.Ref.AssembledCount = 0;

            var rot = quaternion.RotateY(0.5f * dt);
            var pulseScale = math.sin(time * 2f) * 0.1f + 1f;
            var baseScale = config.Ref.CubeScale;

            foreach (var (tRef, tagRef) in query.par_iter())
            {
                ref var tag = ref tagRef.Get;
                if (tag.Value != CubeState.Assembled) continue;

                Interlocked.Increment(ref cycle.Ref.AssembledCount);

                ref var t = ref tRef.Get;
                t.Position = math.mul(rot, t.Position);
                var s = baseScale * pulseScale;
                t.Scale = new float3(s, s, s);
            }

            if (cycle.Ref.AssembledCount < totalCount) return;

            cycle.Ref.AssembledTimer += dt;
            if (cycle.Ref.AssembledTimer >= cycle.Ref.AssembledDuration)
            {
                cycle.Ref.Disassembling = true;
                cycle.Ref.DisassembleTimer = 0f;
            }
        }

        [System, BurstCompile]
        public static void Disassemble(
            ref Query<
                LocalTransform, 
                Velocity, 
                CubeStateTag, 
                FormationOffset,
                SculptureSlotIndex, 
                AnimationPhase> query,
            ref State state,
            ref Res<SculptureData> sculpture,
            ref Res<CycleData> cycle,
            ref Res<ConfigData> config)
        {
            if (!cycle.Ref.Disassembling) return;

            sculpture.Ref.SlotCounter = 0;
            sculpture.Ref.TransitionCounter = 0;

            var scale = config.Ref.CubeScale;

            foreach (var (tRef, vRef, tagRef, offsetRef, slotRef, phaseRef) in query.par_iter())
            {
                ref var tag = ref tagRef.Get;
                if (tag.Value != CubeState.Assembled && tag.Value != CubeState.Assemble) continue;

                ref var t = ref tRef.Get;
                ref var vel = ref vRef.Get;
                ref var offset = ref offsetRef.Get;
                ref var slot = ref slotRef.Get;
                ref var phase = ref phaseRef.Get;

                var dir = t.Position;
                var dist = math.length(dir);
                var explodeDir = dist > 0.001f ? dir / dist : new float3(0f, 1f, 0f);
                vel.Value = explodeDir * 8f + new float3(0f, 3f, 0f);

                tag.Value = CubeState.Free;
                slot.Value = -1;
                phase.Time = 0f;
                offset.Value = float3.zero;
                t.Scale = new float3(scale, scale, scale);
            }
            Interlocked.Increment(ref cycle.Ref.CycleIndex);
            cycle.Ref.SwarmFormation = cycle.Ref.CycleIndex % 4;
            cycle.Ref.SculptureShape = (cycle.Ref.CycleIndex / 4) % 4;
            cycle.Ref.AssembledTimer = 0f;
            cycle.Ref.Disassembling = false;
        }

        [System, BurstCompile]
        public static void FillMatrix(
            ref Query<LocalTransform, With<CubeStateTag>> query,
            ref Res<RenderBridge> bridge)
        {
            var count = query.Count;
            var matrices = bridge.Ref.Matrices;
            if (!matrices.IsCreated || count > matrices.Length) return;
            var range = query.Range;
            var idx = range.start;

            foreach (var (tRef, _) in query.par_iter())
            {
                matrices[idx] = tRef.Get.Matrix;
                idx++;
            }

            bridge.Ref.count = query.Count;
        }

        [System]
        public static void Render(
            ref Query<LocalTransform, CubeStateTag> query,
            ref Res<RenderBridge> bridge,
            ref State state)
        {
            var cfg = CubeSculptureBootstrap.Instance;
            if (cfg == null) return;

            var matrices = bridge.Ref.Matrices;
            var idx = bridge.Ref.count;
            const int batchMax = 1023;
            for (int batch = 0; batch < idx; batch += batchMax)
            {
                var thisBatch = math.min(batchMax, idx - batch);
                var slice = matrices.GetSubArray(batch, thisBatch);
                Graphics.RenderMeshInstanced(
                    new RenderParams(cfg.CubeMaterial),
                    cfg.CubeMesh,
                    0,
                    slice
                );
            }

            bridge.Ref.count = 0;
        }

    }
}

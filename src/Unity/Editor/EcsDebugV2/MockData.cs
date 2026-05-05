#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public enum FieldValueType
    {
        Number,
        String,
        Bool,
        EntityRef
    }

    public struct FieldValue : IEquatable<FieldValue>
    {
        public FieldValueType Type;
        public double NumberVal;
        public string StringVal;
        public bool BoolVal;
        public int EntityRefVal;

        public static FieldValue FromNumber(double v) => new FieldValue { Type = FieldValueType.Number, NumberVal = v };
        public static FieldValue FromString(string v) => new FieldValue { Type = FieldValueType.String, StringVal = v };
        public static FieldValue FromBool(bool v) => new FieldValue { Type = FieldValueType.Bool, BoolVal = v };
        public static FieldValue FromEntityRef(int id) => new FieldValue { Type = FieldValueType.EntityRef, EntityRefVal = id };

        public bool Equals(FieldValue other)
        {
            if (Type != other.Type) return false;
            switch (Type)
            {
                case FieldValueType.Number: return Mathf.Approximately((float)NumberVal, (float)other.NumberVal);
                case FieldValueType.String: return StringVal == other.StringVal;
                case FieldValueType.Bool: return BoolVal == other.BoolVal;
                case FieldValueType.EntityRef: return EntityRefVal == other.EntityRefVal;
                default: return false;
            }
        }

        public override bool Equals(object obj) => obj is FieldValue other && Equals(other);
        public override int GetHashCode() => Type.GetHashCode();
    }

    public class ComponentInstance
    {
        public string Name;
        public Dictionary<string, FieldValue> Fields = new Dictionary<string, FieldValue>();
    }

    public class MockEntity
    {
        public int Id;
        public string Name;
        public string Archetype;
        public bool Alive = true;
        public List<ComponentInstance> Components = new List<ComponentInstance>();
    }

    public class MockArchetype
    {
        public int Id;
        public List<string> Components = new List<string>();
        public int EntityCount;
        public int ChunkCount;
    }

    public class MockQuery
    {
        public string Id;
        public string Name;
        public List<string> With = new List<string>();
        public List<string> Without = new List<string>();
        public int Matched;
        public double LastRunMs;
    }

    public class MockResource
    {
        public string Name;
        public string Type;
        public Dictionary<string, FieldValue> Value = new Dictionary<string, FieldValue>();
        public bool IsScalar;
        public FieldValue ScalarValue;
    }

    public static class MockData
    {
        public static readonly string[] ALL_COMPONENT_TYPES =
        {
            "Transform", "Velocity", "Health", "Sprite", "PlayerController",
            "AIBrain", "Damage", "Lifetime", "Pickup", "Collider", "Camera"
        };

        private static readonly (string name, string[] comps)[] ArchetypeDefs =
        {
            ("Player", new[] { "Transform", "Velocity", "PlayerController", "Health", "Sprite" }),
            ("Enemy", new[] { "Transform", "Velocity", "AIBrain", "Health", "Sprite" }),
            ("Projectile", new[] { "Transform", "Velocity", "Damage", "Lifetime" }),
            ("Pickup", new[] { "Transform", "Sprite", "Pickup" }),
            ("StaticProp", new[] { "Transform", "Sprite", "Collider" }),
            ("Camera", new[] { "Transform", "Camera" }),
        };

        private struct Seed { public uint V; }

        private static float Rand(ref Seed s)
        {
            s.V = (uint)((s.V * 1664525 + 1013904223) & 0xFFFFFFFF);
            return s.V / (float)0xFFFFFFFF;
        }

        private static double Round2(double v) => Math.Round(v * 100) / 100.0;

        public static List<MockEntity> BuildMockEntities(int count = 72)
        {
            var seed = new Seed { V = 42 };
            var entities = new List<MockEntity>(count);
            for (int i = 0; i < count; i++)
            {
                var a = ArchetypeDefs[Mathf.FloorToInt(Rand(ref seed) * ArchetypeDefs.Length)];
                var e = new MockEntity
                {
                    Id = 1000 + i,
                    Name = $"{a.name}_{i:D3}",
                    Archetype = a.name,
                    Alive = Rand(ref seed) > 0.05f
                };
                foreach (var compName in a.comps)
                    e.Components.Add(MakeComponent(compName, ref seed));
                entities.Add(e);
            }
            return entities;
        }

        private static ComponentInstance MakeComponent(string name, ref Seed seed)
        {
            var c = new ComponentInstance { Name = name };

            switch (name)
            {
                case "Transform":
                    c.Fields["x"] = FieldValue.FromNumber(RandRound(ref seed));
                    c.Fields["y"] = FieldValue.FromNumber(RandRound(ref seed));
                    c.Fields["z"] = FieldValue.FromNumber(RandRound(ref seed));
                    c.Fields["rot"] = FieldValue.FromNumber(RandRound(ref seed));
                    break;
                case "Velocity":
                    c.Fields["vx"] = FieldValue.FromNumber(RandRound(ref seed) - 50);
                    c.Fields["vy"] = FieldValue.FromNumber(RandRound(ref seed) - 50);
                    c.Fields["vz"] = FieldValue.FromNumber(0);
                    break;
                case "Health":
                    c.Fields["current"] = FieldValue.FromNumber(Mathf.FloorToInt(RandRound(ref seed)));
                    c.Fields["max"] = FieldValue.FromNumber(100);
                    c.Fields["regen"] = FieldValue.FromNumber(0.5);
                    break;
                case "Sprite":
                    c.Fields["texture"] = FieldValue.FromString("atlas_main.png");
                    c.Fields["layer"] = FieldValue.FromNumber(Mathf.FloorToInt(RandRound(ref seed) / 10));
                    c.Fields["tint"] = FieldValue.FromString("#ffffff");
                    break;
                case "PlayerController":
                    c.Fields["speed"] = FieldValue.FromNumber(5.5);
                    c.Fields["jumpHeight"] = FieldValue.FromNumber(2.4);
                    c.Fields["grounded"] = FieldValue.FromBool(true);
                    break;
                case "AIBrain":
                    c.Fields["state"] = FieldValue.FromString("patrol");
                    c.Fields["aggroRange"] = FieldValue.FromNumber(12);
                    c.Fields["target"] = FieldValue.FromEntityRef(1000);
                    break;
                case "Damage":
                    c.Fields["amount"] = FieldValue.FromNumber(25);
                    c.Fields["type"] = FieldValue.FromString("kinetic");
                    c.Fields["crit"] = FieldValue.FromBool(false);
                    break;
                case "Lifetime":
                    c.Fields["remaining"] = FieldValue.FromNumber(RandRound(ref seed) / 10);
                    c.Fields["total"] = FieldValue.FromNumber(5);
                    break;
                case "Pickup":
                    c.Fields["kind"] = FieldValue.FromString("ammo");
                    c.Fields["amount"] = FieldValue.FromNumber(10);
                    c.Fields["respawn"] = FieldValue.FromBool(false);
                    break;
                case "Collider":
                    c.Fields["shape"] = FieldValue.FromString("box");
                    c.Fields["w"] = FieldValue.FromNumber(1);
                    c.Fields["h"] = FieldValue.FromNumber(1);
                    c.Fields["trigger"] = FieldValue.FromBool(false);
                    break;
                case "Camera":
                    c.Fields["fov"] = FieldValue.FromNumber(60);
                    c.Fields["near"] = FieldValue.FromNumber(0.1);
                    c.Fields["far"] = FieldValue.FromNumber(1000);
                    c.Fields["active"] = FieldValue.FromBool(true);
                    break;
            }
            return c;
        }

        private static float RandRound(ref Seed seed)
        {
            return Mathf.RoundToInt(Rand(ref seed) * 10000) / 100f;
        }

        public static ComponentInstance MakeComponentByName(string name)
        {
            var s = new Seed { V = (uint)(UnityEngine.Random.Range(0, int.MaxValue)) };
            return MakeComponent(name, ref s);
        }

        public static List<MockArchetype> BuildArchetypes(List<MockEntity> entities)
        {
            var map = new Dictionary<string, MockArchetype>();
            int id = 0;
            foreach (var e in entities)
            {
                var keys = e.Components.Select(c => c.Name).ToList();
                keys.Sort();
                var key = string.Join("|", keys);
                if (!map.TryGetValue(key, out var arch))
                {
                    arch = new MockArchetype
                    {
                        Id = id++,
                        Components = new List<string>(key.Split('|'))
                    };
                    map[key] = arch;
                }
                arch.EntityCount++;
                arch.ChunkCount = Mathf.Max(1, Mathf.CeilToInt((float)arch.EntityCount / 16f));
            }
            return map.Values.ToList();
        }

        public static List<MockQuery> BuildQueries()
        {
            return new List<MockQuery>
            {
                new MockQuery { Id = "q1", Name = "MovementQuery", With = { "Transform", "Velocity" }, LastRunMs = 0.42 },
                new MockQuery { Id = "q2", Name = "RenderQuery", With = { "Transform", "Sprite" }, LastRunMs = 1.18 },
                new MockQuery { Id = "q3", Name = "AITickQuery", With = { "AIBrain", "Transform" }, Without = { "Dead" }, LastRunMs = 0.83 },
                new MockQuery { Id = "q4", Name = "DamageQuery", With = { "Damage", "Transform" }, LastRunMs = 0.21 },
                new MockQuery { Id = "q5", Name = "PlayerInputQuery", With = { "PlayerController" }, LastRunMs = 0.07 },
                new MockQuery { Id = "q6", Name = "LifetimeDecay", With = { "Lifetime" }, LastRunMs = 0.15 },
            };
        }

        public static List<MockResource> BuildResources()
        {
            return new List<MockResource>
            {
                new MockResource
                {
                    Name = "Time", Type = "TimeRes", IsScalar = false,
                    Value =
                    {
                        { "delta", FieldValue.FromNumber(0.0166) },
                        { "elapsed", FieldValue.FromNumber(1284.32) },
                        { "frame", FieldValue.FromNumber(77123) }
                    }
                },
                new MockResource
                {
                    Name = "Input", Type = "InputRes", IsScalar = false,
                    Value =
                    {
                        { "mouseX", FieldValue.FromNumber(412) },
                        { "mouseY", FieldValue.FromNumber(233) },
                        { "buttons", FieldValue.FromNumber(0) }
                    }
                },
                new MockResource
                {
                    Name = "Gravity", Type = "Vec3", IsScalar = false,
                    Value =
                    {
                        { "x", FieldValue.FromNumber(0) },
                        { "y", FieldValue.FromNumber(-9.81) },
                        { "z", FieldValue.FromNumber(0) }
                    }
                },
                new MockResource
                {
                    Name = "Score", Type = "u32", IsScalar = true,
                    ScalarValue = FieldValue.FromNumber(14250)
                },
                new MockResource
                {
                    Name = "Paused", Type = "bool", IsScalar = true,
                    ScalarValue = FieldValue.FromBool(false)
                },
                new MockResource
                {
                    Name = "LevelName", Type = "string", IsScalar = true,
                    ScalarValue = FieldValue.FromString("arena_03")
                },
            };
        }

        public static void UpdateQueryMatches(List<MockQuery> queries, List<MockEntity> entities)
        {
            foreach (var q in queries)
            {
                q.Matched = 0;
                foreach (var e in entities)
                {
                    var names = new HashSet<string>(e.Components.Select(c => c.Name));
                    bool match = q.With.All(w => names.Contains(w)) && q.Without.All(w => !names.Contains(w));
                    if (match) q.Matched++;
                }
            }
        }

        public static void MutateRandomFields(List<MockEntity> entities, int touches, Dictionary<string, long> changes)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            for (int i = 0; i < touches; i++)
            {
                if (entities.Count == 0) break;
                var e = entities[UnityEngine.Random.Range(0, entities.Count)];
                if (e.Components.Count == 0) continue;
                var c = e.Components[UnityEngine.Random.Range(0, e.Components.Count)];
                var keys = c.Fields.Keys.ToList();
                if (keys.Count == 0) continue;
                var k = keys[UnityEngine.Random.Range(0, keys.Count)];
                c.Fields[k] = Mutate(c.Fields[k]);
                changes[$"{e.Id}:{c.Name}:{k}"] = now;
            }
        }

        private static FieldValue Mutate(FieldValue v)
        {
            switch (v.Type)
            {
                case FieldValueType.Number:
                    return FieldValue.FromNumber(Math.Round((v.NumberVal + (UnityEngine.Random.value - 0.5) * 4) * 100) / 100.0);
                case FieldValueType.Bool:
                    return FieldValue.FromBool(!v.BoolVal);
                case FieldValueType.String:
                    var opts = new[] { "idle", "patrol", "chase", "attack", "flee" };
                    return FieldValue.FromString(opts[UnityEngine.Random.Range(0, opts.Length)]);
                default:
                    return v;
            }
        }
    }
}
#endif

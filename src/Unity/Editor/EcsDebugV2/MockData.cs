#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        public static FieldValue FromNumber(double v) 
            => new () { Type = FieldValueType.Number, NumberVal = v };
        public static FieldValue FromString(string v) 
            => new () { Type = FieldValueType.String, StringVal = v };
        public static FieldValue FromBool(bool v) 
            => new () { Type = FieldValueType.Bool, BoolVal = v };
        public static FieldValue FromEntityRef(int id) 
            => new () { Type = FieldValueType.EntityRef, EntityRefVal = id };

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

    public class ComponentInfo
    {
        public int TypeIndex = -1;
        public string Name;
        public int ByteSize;
        public List<(string Key, FieldValue Value)> Fields = new ();

        public FieldValue GetField(string key)
        {
            for (int i = 0; i < Fields.Count; i++)
                if (Fields[i].Key == key) return Fields[i].Value;
            return default;
        }

        public void SetField(string key, FieldValue val)
        {
            for (var i = 0; i < Fields.Count; i++)
            {
                if (Fields[i].Key == key)
                {
                    Fields[i] = (key, val);
                    return;
                }
            }
        }

        public bool HasField(string key)
        {
            for (int i = 0; i < Fields.Count; i++)
                if (Fields[i].Key == key) return true;
            return false;
        }
    }

    public class EntityInfo
    {
        public int id;
        public string name;
        public string archetype;
        public bool alive = true;
        public List<ComponentInfo> components = new ();
    }

    public class ArchetypeInfo
    {
        public int id;
        public List<string> components = new ();
        public int entityCount;
        public int chunkCount;
        public List<int> entityIds = new ();
    }

    public class QueryInfo
    {
        public int id;
        public string name;
        public List<string> with = new ();
        public List<string> without = new ();
        public int matched;
        public double lastRunMs;
    }

    public class ResourceInfo
    {
        public string name;
        public string type;
        public Dictionary<string, FieldValue> value = new ();
        public bool isScalar;
        public FieldValue scalarValue;
    }

    public static class MockData
    {
        public static readonly string[] AllComponentTypes =
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

        public static List<EntityInfo> BuildMockEntities(int count = 72)
        {
            var seed = new Seed { V = 42 };
            var entities = new List<EntityInfo>(count);
            for (int i = 0; i < count; i++)
            {
                var a = ArchetypeDefs[Mathf.FloorToInt(Rand(ref seed) * ArchetypeDefs.Length)];
                var e = new EntityInfo
                {
                    id = 1000 + i,
                    name = $"{a.name}_{i:D3}",
                    archetype = a.name,
                    alive = Rand(ref seed) > 0.05f
                };
                foreach (var compName in a.comps)
                    e.components.Add(MakeComponent(compName, ref seed));
                entities.Add(e);
            }
            return entities;
        }

        private static ComponentInfo MakeComponent(string name, ref Seed seed)
        {
            var c = new ComponentInfo { Name = name };

            switch (name)
            {
                case "Transform":
                    c.ByteSize = 16;
                    c.Fields.Add(("x", FieldValue.FromNumber(RandRound(ref seed))));
                    c.Fields.Add(("y", FieldValue.FromNumber(RandRound(ref seed))));
                    c.Fields.Add(("z", FieldValue.FromNumber(RandRound(ref seed))));
                    c.Fields.Add(("rot", FieldValue.FromNumber(RandRound(ref seed))));
                    break;
                case "Velocity":
                    c.ByteSize = 12;
                    c.Fields.Add(("vx", FieldValue.FromNumber(RandRound(ref seed) - 50)));
                    c.Fields.Add(("vy", FieldValue.FromNumber(RandRound(ref seed) - 50)));
                    c.Fields.Add(("vz", FieldValue.FromNumber(0)));
                    break;
                case "Health":
                    c.ByteSize = 12;
                    c.Fields.Add(("current", FieldValue.FromNumber(Mathf.FloorToInt(RandRound(ref seed)))));
                    c.Fields.Add(("max", FieldValue.FromNumber(100)));
                    c.Fields.Add(("regen", FieldValue.FromNumber(0.5)));
                    break;
                case "Sprite":
                    c.ByteSize = 24;
                    c.Fields.Add(("texture", FieldValue.FromString("atlas_main.png")));
                    c.Fields.Add(("layer", FieldValue.FromNumber(Mathf.FloorToInt(RandRound(ref seed) / 10))));
                    c.Fields.Add(("tint", FieldValue.FromString("#ffffff")));
                    break;
                case "PlayerController":
                    c.ByteSize = 13;
                    c.Fields.Add(("speed", FieldValue.FromNumber(5.5)));
                    c.Fields.Add(("jumpHeight", FieldValue.FromNumber(2.4)));
                    c.Fields.Add(("grounded", FieldValue.FromBool(true)));
                    break;
                case "AIBrain":
                    c.ByteSize = 20;
                    c.Fields.Add(("state", FieldValue.FromString("patrol")));
                    c.Fields.Add(("aggroRange", FieldValue.FromNumber(12)));
                    c.Fields.Add(("target", FieldValue.FromEntityRef(1000)));
                    break;
                case "Damage":
                    c.ByteSize = 9;
                    c.Fields.Add(("amount", FieldValue.FromNumber(25)));
                    c.Fields.Add(("type", FieldValue.FromString("kinetic")));
                    c.Fields.Add(("crit", FieldValue.FromBool(false)));
                    break;
                case "Lifetime":
                    c.ByteSize = 8;
                    c.Fields.Add(("remaining", FieldValue.FromNumber(RandRound(ref seed) / 10)));
                    c.Fields.Add(("total", FieldValue.FromNumber(5)));
                    break;
                case "Pickup":
                    c.ByteSize = 14;
                    c.Fields.Add(("kind", FieldValue.FromString("ammo")));
                    c.Fields.Add(("amount", FieldValue.FromNumber(10)));
                    c.Fields.Add(("respawn", FieldValue.FromBool(false)));
                    break;
                case "Collider":
                    c.ByteSize = 10;
                    c.Fields.Add(("shape", FieldValue.FromString("box")));
                    c.Fields.Add(("w", FieldValue.FromNumber(1)));
                    c.Fields.Add(("h", FieldValue.FromNumber(1)));
                    c.Fields.Add(("trigger", FieldValue.FromBool(false)));
                    break;
                case "Camera":
                    c.ByteSize = 13;
                    c.Fields.Add(("fov", FieldValue.FromNumber(60)));
                    c.Fields.Add(("near", FieldValue.FromNumber(0.1)));
                    c.Fields.Add(("far", FieldValue.FromNumber(1000)));
                    c.Fields.Add(("active", FieldValue.FromBool(true)));
                    break;
            }
            return c;
        }

        private static float RandRound(ref Seed seed)
        {
            return Mathf.RoundToInt(Rand(ref seed) * 10000) / 100f;
        }

        public static ComponentInfo MakeComponentByName(string name)
        {
            var s = new Seed { V = (uint)(UnityEngine.Random.Range(0, int.MaxValue)) };
            return MakeComponent(name, ref s);
        }

        public static List<ArchetypeInfo> BuildArchetypes(List<EntityInfo> entities)
        {
            var map = new Dictionary<string, ArchetypeInfo>();
            int id = 0;
            foreach (var e in entities)
            {
                var keys = e.components.Select(c => c.Name).ToList();
                keys.Sort();
                var key = string.Join("|", keys);
                if (!map.TryGetValue(key, out var arch))
                {
                    arch = new ArchetypeInfo
                    {
                        id = id++,
                        components = new List<string>(key.Split('|'))
                    };
                    map[key] = arch;
                }
                arch.entityCount++;
                arch.entityIds.Add(e.id);
                arch.chunkCount = Mathf.Max(1, Mathf.CeilToInt((float)arch.entityCount / 16f));
            }
            return map.Values.ToList();
        }

        public static List<QueryInfo> BuildQueries()
        {
            return new List<QueryInfo>
            {
                new QueryInfo { id = 0, name = "MovementQuery", with = { "Transform", "Velocity" }, lastRunMs = 0.42 },
                new QueryInfo { id = 1, name = "RenderQuery", with = { "Transform", "Sprite" }, lastRunMs = 1.18 },
                new QueryInfo { id = 2, name = "AITickQuery", with = { "AIBrain", "Transform" }, without = { "Dead" }, lastRunMs = 0.83 },
                new QueryInfo { id = 3, name = "DamageQuery", with = { "Damage", "Transform" }, lastRunMs = 0.21 },
                new QueryInfo { id = 4, name = "PlayerInputQuery", with = { "PlayerController" }, lastRunMs = 0.07 },
                new QueryInfo { id = 5, name = "LifetimeDecay", with = { "Lifetime" }, lastRunMs = 0.15 },
            };
        }

        public static List<ResourceInfo> BuildResources()
        {
            return new List<ResourceInfo>
            {
                new ResourceInfo
                {
                    name = "Time", type = "TimeRes", isScalar = false,
                    value =
                    {
                        { "delta", FieldValue.FromNumber(0.0166) },
                        { "elapsed", FieldValue.FromNumber(1284.32) },
                        { "frame", FieldValue.FromNumber(77123) }
                    }
                },
                new ResourceInfo
                {
                    name = "Input", type = "InputRes", isScalar = false,
                    value =
                    {
                        { "mouseX", FieldValue.FromNumber(412) },
                        { "mouseY", FieldValue.FromNumber(233) },
                        { "buttons", FieldValue.FromNumber(0) }
                    }
                },
                new ResourceInfo
                {
                    name = "Gravity", type = "Vec3", isScalar = false,
                    value =
                    {
                        { "x", FieldValue.FromNumber(0) },
                        { "y", FieldValue.FromNumber(-9.81) },
                        { "z", FieldValue.FromNumber(0) }
                    }
                },
                new ResourceInfo
                {
                    name = "Score", type = "u32", isScalar = true,
                    scalarValue = FieldValue.FromNumber(14250)
                },
                new ResourceInfo
                {
                    name = "Paused", type = "bool", isScalar = true,
                    scalarValue = FieldValue.FromBool(false)
                },
                new ResourceInfo
                {
                    name = "LevelName", type = "string", isScalar = true,
                    scalarValue = FieldValue.FromString("arena_03")
                },
            };
        }

        public static void UpdateQueryMatches(List<QueryInfo> queries, List<EntityInfo> entities)
        {
            foreach (var q in queries)
            {
                q.matched = 0;
                foreach (var e in entities)
                {
                    var names = new HashSet<string>(e.components.Select(c => c.Name));
                    bool match = q.with.All(w => names.Contains(w)) && q.without.All(w => !names.Contains(w));
                    if (match) q.matched++;
                }
            }
        }

        public static void MutateRandomFields(List<EntityInfo> entities, int touches, Dictionary<string, long> changes)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            for (int i = 0; i < touches; i++)
            {
                if (entities.Count == 0) break;
                var e = entities[UnityEngine.Random.Range(0, entities.Count)];
                if (e.components.Count == 0) continue;
                var c = e.components[UnityEngine.Random.Range(0, e.components.Count)];
                if (c.Fields.Count == 0) continue;
                var fi = UnityEngine.Random.Range(0, c.Fields.Count);
                var k = c.Fields[fi].Key;
                c.SetField(k, Mutate(c.GetField(k)));
                changes[$"{e.id}:{c.Name}:{k}"] = now;
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

#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public unsafe class LiveDataProvider : IEcsDataProvider
    {
        private int _worldIndex;
        private string[] _cachedTypeArray;

        public int SystemCount
        {
            get
            {
                var list = WorldSystems.GetAll(_worldIndex);
                var count = 0;
                foreach (var systems in list)
                    count += systems.runners.Count + systems.fixedRunners.Count;
                return count;
            }
        }

        public int Tick
        {
            get
            {
                try
                {
                    ref var w = ref GetWorld();
                    if (w.IsAlive && w.UnsafeWorld != null)
                        return (int)w.UnsafeWorld->timeData.TickCount;
                }
                catch { }
                return 0;
            }
            set { }
        }

        public WorldInfo WorldInfo
        {
            get
            {
                var names = new List<string>();
                var slots = new List<int>();
                try
                {
                    var capacity = World.WorldCapacity;
                    for (var i = 0; i < capacity; i++)
                    {
                        ref var w = ref World.Get(i);
                        if (w.IsAlive && w.UnsafeWorld != null)
                        {
                            names.Add($"world::{w.UnsafeWorld->Id}");
                            slots.Add(i);
                        }
                    }
                }
                catch { }

                var current = "world::0";
                try
                {
                    ref var cw = ref World.Get(_worldIndex);
                    if (cw.IsAlive && cw.UnsafeWorld != null)
                        current = $"world::{cw.UnsafeWorld->Id}";
                }
                catch { }

                return new WorldInfo
                {
                    Name = current,
                    WorldNames = names.ToArray(),
                    WorldSlots = slots.ToArray()
                };
            }
        }

        public string[] AvailableComponentTypes
        {
            get
            {
                if (_cachedTypeArray != null) return _cachedTypeArray;
                var names = new List<string>();
                foreach (var idx in ComponentTypeMap.TypesIndexes)
                {
                    var t = ComponentTypeMap.GetType(idx);
                    if (t != null)
                        names.Add(t.Name);
                }

                _cachedTypeArray = names.ToArray();
                return _cachedTypeArray;
            }
        }

        public int WorldCount
        {
            get
            {
                var count = 0;
                try
                {
                    var capacity = World.WorldCapacity;
                    for (var i = 0; i < capacity; i++)
                    {
                        ref var w = ref World.Get(i);
                        if (w.IsAlive && w.UnsafeWorld != null) count++;
                    }
                }
                catch { }
                return count;
            }
        }

        public void SetWorld(int worldIndex)
        {
            _worldIndex = worldIndex;
            _cachedTypeArray = null;
        }

        private ref World GetWorld()
        {
            return ref World.Get(_worldIndex);
        }

        private bool IsWorldValid()
        {
            ref var w = ref GetWorld();
            return w.IsAlive && w.UnsafeWorld != null;
        }

        public List<EntityInfo> GetEntities()
        {
            var result = new List<EntityInfo>();
            if (!IsWorldValid()) return result;

            ref var world = ref GetWorld();
            var uw = world.UnsafeWorld;
            var alive = uw->entitiesDens.GetAliveEntities();
            var nameTypeIndex = -1;
            try { nameTypeIndex = ComponentType<Name>.Index; } catch { }

            for (var i = 0; i < alive.Length; i++)
            {
                var entityId = alive[i];
                if (entityId == 0) continue;

                ref var archPtr = ref uw->GetEntityArchetypePtr(entityId);
                ref var arch = ref archPtr.Ref;

                var entityName = $"Entity_{entityId}";
                if (nameTypeIndex >= 0 && arch.Has(nameTypeIndex))
                {
                    try
                    {
                        var boxed = arch.GetObject(entityId, nameTypeIndex);
                        if (boxed is Name nameComp && nameComp.value.Value != null)
                            entityName = nameComp.value.Value;
                    }
                    catch { }
                }

                var archetypeLabel = BuildArchetypeLabel(ref arch);
                var components = ReadComponents(uw, ref arch, entityId);

                result.Add(new EntityInfo
                {
                    Id = entityId,
                    Name = entityName,
                    Archetype = archetypeLabel,
                    Alive = true,
                    Components = components
                });
            }

            return result;
        }

        public List<ArchetypeInfo> GetArchetypes()
        {
            var result = new List<ArchetypeInfo>();
            if (!IsWorldValid()) return result;

            ref var world = ref GetWorld();
            var uw = world.UnsafeWorld;
            for (var i = 1; i < uw->archetypesList.Length; i++)
            {
                ref var arch = ref uw->archetypesList.Ptr[i].Ref;
                var compNames = new List<string>();
                foreach (var typeIdx in arch.types)
                {
                    var t = ComponentTypeMap.GetType(typeIdx);
                    compNames.Add(t?.Name ?? $"Type_{typeIdx}");
                }

                result.Add(new ArchetypeInfo
                {
                    Id = i,
                    Components = compNames,
                    EntityCount = arch.count,
                    ChunkCount = Mathf.Max(1, Mathf.CeilToInt((float)arch.count / 16f))
                });
            }

            return result;
        }

        public List<QueryInfo> GetQueries()
        {
            var result = new List<QueryInfo>();
            if (!IsWorldValid()) return result;

            ref var world = ref GetWorld();
            var uw = world.UnsafeWorld;
            for (var i = 0; i < uw->queries.Length; i++)
            {
                ref var q = ref uw->queries.Ptr[i].Ref;
                var withList = new List<string>();
                var withoutList = new List<string>();

                foreach (var typeIdx in ComponentTypeMap.TypesIndexes)
                {
                    if (q.with.Has(typeIdx))
                    {
                        var t = ComponentTypeMap.GetType(typeIdx);
                        withList.Add(t?.Name ?? $"Type_{typeIdx}");
                    }

                    if (q.none.Has(typeIdx))
                    {
                        var t = ComponentTypeMap.GetType(typeIdx);
                        withoutList.Add(t?.Name ?? $"Type_{typeIdx}");
                    }
                }

                result.Add(new QueryInfo
                {
                    Id = $"q{q.Id}",
                    Name = withList.Count > 0 ? string.Join("+", withList) : $"Query_{q.Id}",
                    With = withList,
                    Without = withoutList,
                    Matched = q.count,
                    LastRunMs = 0
                });
            }

            return result;
        }

        public List<ResourceInfo> GetResources()
        {
            return new List<ResourceInfo>();
        }

        public EntityInfo CreateEntity()
        {
            if (!IsWorldValid()) return null;
            ref var world = ref GetWorld();
            ref var entity = ref world.Entity();
            return new EntityInfo
            {
                Id = entity.id,
                Name = $"Entity_{entity.id}",
                Archetype = "Empty",
                Alive = true,
                Components = new List<ComponentInfo>()
            };
        }

        public void DestroyEntity(int id)
        {
            if (!IsWorldValid()) return;
            ref var world = ref GetWorld();
            ref var entity = ref world.UnsafeWorld->GetEntity(id);
            if (entity.IsValid())
                entity.Destroy();
        }

        public void AddComponent(int entityId, string compName)
        {
            if (!IsWorldValid()) return;
            ref var world = ref GetWorld();
            var typeIndex = FindTypeIndexByName(compName);
            if (typeIndex < 0) return;
            ref var entity = ref world.UnsafeWorld->GetEntity(entityId);
            if (!entity.IsValid()) return;
            entity.AddIndex(typeIndex);
        }

        public void RemoveComponent(int entityId, string compName)
        {
            if (!IsWorldValid()) return;
            ref var world = ref GetWorld();
            var typeIndex = FindTypeIndexByName(compName);
            if (typeIndex < 0) return;
            ref var entity = ref world.UnsafeWorld->GetEntity(entityId);
            if (!entity.IsValid()) return;
            entity.RemoveIndex(typeIndex);
        }

        public void SetFieldValue(int entityId, string compName, string fieldKey, FieldValue value)
        {
            if (!IsWorldValid()) return;
            ref var world = ref GetWorld();
            var typeIndex = FindTypeIndexByName(compName);
            if (typeIndex < 0) return;

            ref var archPtr = ref world.UnsafeWorld->GetEntityArchetypePtr(entityId);
            ref var arch = ref archPtr.Ref;
            var boxed = arch.GetObject(entityId, typeIndex);
            if (boxed == null) return;

            ComponentFieldWriter.WriteField(boxed, fieldKey, value);
            ref var e = ref world.UnsafeWorld->GetEntity(entityId);
            e.SetObject(boxed);
        }

        public void SimulateTick(Dictionary<string, long> changes)
        {
        }

        private int FindTypeIndexByName(string name)
        {
            foreach (var idx in ComponentTypeMap.TypesIndexes)
            {
                var t = ComponentTypeMap.GetType(idx);
                if (t != null && t.Name == name)
                    return idx;
            }

            return -1;
        }

        private static string BuildArchetypeLabel(ref ArchetypeUnsafe arch)
        {
            if (arch.types.length == 0) return "Empty";
            var first = ComponentTypeMap.GetType(arch.types.Ptr[0]);
            if (arch.types.length == 1) return first?.Name ?? "Unknown";
            return $"{first?.Name}+{arch.types.length - 1}";
        }

        private static List<ComponentInfo> ReadComponents(
            World.WorldUnsafe* uw, ref ArchetypeUnsafe arch, int entityId)
        {
            var components = new List<ComponentInfo>();
            foreach (var typeIdx in arch.types)
            {
                var ctData = ComponentTypeMap.GetComponentType(typeIdx);
                var t = ComponentTypeMap.GetType(typeIdx);
                var info = new ComponentInfo
                {
                    Name = t?.Name ?? $"Type_{typeIdx}",
                    ByteSize = ctData.size
                };

                if (ctData.isTag)
                {
                    info.Fields.Add(("#tag", FieldValue.FromBool(true)));
                }
                else
                {
                    try
                    {
                        var boxed = arch.GetObject(entityId, typeIdx);
                        if (boxed != null)
                            ComponentFieldReader.ReadFields(boxed, info.Fields);
                    }
                    catch { }
                }

                components.Add(info);
            }

            return components;
        }
    }

    internal static class ComponentFieldReader
    {
        public static void ReadFields(object obj, List<(string Key, FieldValue Value)> fields)
        {
            var type = obj.GetType();
            var flds = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            foreach (var fi in flds)
            {
                ReadSingleField(fi, obj, fields);
            }
        }

        private static void ReadSingleField(FieldInfo fi, object obj,
            List<(string Key, FieldValue Value)> fields)
        {
            var ft = fi.FieldType;
            var val = fi.GetValue(obj);

            if (ft == typeof(float))
                fields.Add((fi.Name, FieldValue.FromNumber((float)val)));
            else if (ft == typeof(double))
                fields.Add((fi.Name, FieldValue.FromNumber((double)val)));
            else if (ft == typeof(int))
                fields.Add((fi.Name, FieldValue.FromNumber((int)val)));
            else if (ft == typeof(long))
                fields.Add((fi.Name, FieldValue.FromNumber((long)val)));
            else if (ft == typeof(uint))
                fields.Add((fi.Name, FieldValue.FromNumber((uint)val)));
            else if (ft == typeof(short))
                fields.Add((fi.Name, FieldValue.FromNumber((short)val)));
            else if (ft == typeof(byte))
                fields.Add((fi.Name, FieldValue.FromNumber((byte)val)));
            else if (ft == typeof(bool))
                fields.Add((fi.Name, FieldValue.FromBool((bool)val)));
            else if (ft == typeof(string))
                fields.Add((fi.Name, FieldValue.FromString((string)val ?? "")));
            else if (ft == typeof(Entity))
                fields.Add((fi.Name, FieldValue.FromEntityRef(((Entity)val).id)));
            else if (ft.IsEnum)
                fields.Add((fi.Name, FieldValue.FromNumber(Convert.ToInt64(val))));
            else if (ft == typeof(UnityEngine.Vector2) || ft.Name == "float2")
            {
                var v = (UnityEngine.Vector2)val;
                fields.Add(($"{fi.Name}.x", FieldValue.FromNumber(v.x)));
                fields.Add(($"{fi.Name}.y", FieldValue.FromNumber(v.y)));
            }
            else if (ft == typeof(UnityEngine.Vector3) || ft.Name == "float3")
            {
                var v = (UnityEngine.Vector3)val;
                fields.Add(($"{fi.Name}.x", FieldValue.FromNumber(v.x)));
                fields.Add(($"{fi.Name}.y", FieldValue.FromNumber(v.y)));
                fields.Add(($"{fi.Name}.z", FieldValue.FromNumber(v.z)));
            }
            else if (ft == typeof(UnityEngine.Vector4) || ft.Name == "float4")
            {
                var v = (UnityEngine.Vector4)val;
                fields.Add(($"{fi.Name}.x", FieldValue.FromNumber(v.x)));
                fields.Add(($"{fi.Name}.y", FieldValue.FromNumber(v.y)));
                fields.Add(($"{fi.Name}.z", FieldValue.FromNumber(v.z)));
                fields.Add(($"{fi.Name}.w", FieldValue.FromNumber(v.w)));
            }
            else if (ft == typeof(UnityEngine.Quaternion) || ft.Name == "quaternion")
            {
                var v = (UnityEngine.Quaternion)val;
                fields.Add(($"{fi.Name}.x", FieldValue.FromNumber(v.x)));
                fields.Add(($"{fi.Name}.y", FieldValue.FromNumber(v.y)));
                fields.Add(($"{fi.Name}.z", FieldValue.FromNumber(v.z)));
                fields.Add(($"{fi.Name}.w", FieldValue.FromNumber(v.w)));
            }
            else if (ft == typeof(UnityEngine.Color))
            {
                var v = (UnityEngine.Color)val;
                fields.Add(($"{fi.Name}.r", FieldValue.FromNumber(v.r)));
                fields.Add(($"{fi.Name}.g", FieldValue.FromNumber(v.g)));
                fields.Add(($"{fi.Name}.b", FieldValue.FromNumber(v.b)));
                fields.Add(($"{fi.Name}.a", FieldValue.FromNumber(v.a)));
            }
            else if (typeof(IComponent).IsAssignableFrom(ft) && ft.IsValueType)
            {
                try
                {
                    var boxed = (IComponent)val;
                    var subFields = new List<(string, FieldValue)>();
                    ReadFields(boxed, subFields);
                    foreach (var sf in subFields)
                        fields.Add(($"{fi.Name}.{sf.Item1}", sf.Item2));
                }
                catch { }
            }
        }
    }

    internal static class ComponentFieldWriter
    {
        public static void WriteField(object obj, string fieldKey, FieldValue value)
        {
            var parts = fieldKey.Split('.');
            if (parts.Length == 1)
            {
                var fi = obj.GetType().GetField(fieldKey, BindingFlags.Instance | BindingFlags.Public);
                if (fi == null) return;
                WriteValue(fi, obj, value);
            }
            else if (parts.Length == 2)
            {
                var parentFi = obj.GetType().GetField(parts[0], BindingFlags.Instance | BindingFlags.Public);
                if (parentFi == null) return;
                var childObj = parentFi.GetValue(obj);
                if (childObj == null) return;
                var childFi = childObj.GetType().GetField(parts[1], BindingFlags.Instance | BindingFlags.Public);
                if (childFi == null) return;
                WriteValue(childFi, childObj, value);
                parentFi.SetValue(obj, childObj);
            }
        }

        private static void WriteValue(FieldInfo fi, object obj, FieldValue value)
        {
            var ft = fi.FieldType;
            try
            {
                if (ft == typeof(float))
                    fi.SetValue(obj, (float)value.NumberVal);
                else if (ft == typeof(double))
                    fi.SetValue(obj, value.NumberVal);
                else if (ft == typeof(int))
                    fi.SetValue(obj, (int)value.NumberVal);
                else if (ft == typeof(long))
                    fi.SetValue(obj, (long)value.NumberVal);
                else if (ft == typeof(uint))
                    fi.SetValue(obj, (uint)value.NumberVal);
                else if (ft == typeof(short))
                    fi.SetValue(obj, (short)value.NumberVal);
                else if (ft == typeof(byte))
                    fi.SetValue(obj, (byte)value.NumberVal);
                else if (ft == typeof(bool))
                    fi.SetValue(obj, value.BoolVal);
                else if (ft == typeof(string))
                    fi.SetValue(obj, value.StringVal);
                else if (ft.IsEnum)
                    fi.SetValue(obj, Enum.ToObject(ft, (long)value.NumberVal));
            }
            catch { }
        }
    }
}
#endif

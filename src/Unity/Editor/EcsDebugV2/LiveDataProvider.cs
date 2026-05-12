#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Wargon.Nukecs.Collections;
// ReSharper disable EmptyGeneralCatchClause

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public unsafe class LiveDataProvider : IEcsDataProvider
    {
        private int _worldIndex;
        private string[] _cachedTypeArray;
        private List<EntityInfo> _entityList = new List<EntityInfo>();
        private List<ArchetypeInfo> _archetypeList = new List<ArchetypeInfo>();
        private List<QueryInfo> _queryList = new List<QueryInfo>();
        private List<ResourceInfo> _resourceList = new();
        private IRes[] _resources = Array.Empty<IRes>();
        private List<(string Key, FieldValue Value)> _resourceFields = new();
        private WorldInfo _cachedWorldInfo;
        private long _worldInfoTimestamp;
        private const long WorldInfoCacheMs = 1000;

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
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (now - _worldInfoTimestamp < WorldInfoCacheMs && _cachedWorldInfo.name != null)
                    return _cachedWorldInfo;
                _worldInfoTimestamp = now;

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

                _cachedWorldInfo = new WorldInfo
                {
                    name = current,
                    worldNames = names.ToArray(),
                    worldSlots = slots.ToArray()
                };
                return _cachedWorldInfo;
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
            _worldInfoTimestamp = 0;
        }

        public int GetEntityCount()
        {
            return !IsWorldValid() ? 0 : GetWorld().UnsafeWorld->entitiesAmount;
        }

        public int GetArchetypeCount()
        {
            if (!IsWorldValid()) return 0;
            return GetWorld().UnsafeWorld->archetypesList.Length;
        }

        public int GetEntityArchetypeIndex(int id)
        {
            if (!IsWorldValid()) return -1;
            return GetWorld().UnsafeWorld->entityLocations.Ptr[id].archetypeIndex;
        }

        private ref World GetWorld()
        {
            return ref World.Get(_worldIndex);
        }

        private bool IsWorldValid()
        {
            ref var w = ref GetWorld();
            return w.IsAlive && w.unsafeWorldPtr.cached != null;
        }

        public List<EntityInfo> GetEntities()
        {
            return GetEntityList();
        }

        public List<EntityInfo> GetEntityList()
        {
            if (!IsWorldValid())
            {
                _entityList.Clear();
                return _entityList;
            }

            ref var world = ref GetWorld();
            var uw = world.UnsafeWorld;
            var alive = uw->entitiesDens.GetAliveEntities();
            var nameTypeIndex = -1;
            try { nameTypeIndex = ComponentType<Name>.Index; } catch { }

            var aliveCount = 0;
            for (var i = 0; i < alive.Length; i++)
                if (alive[i] != 0) aliveCount++;

            if (_entityList.Count != aliveCount)
            {
                _entityList.Clear();
                for (var i = 0; i < alive.Length; i++)
                {
                    var entityId = alive[i];
                    if (entityId == 0) continue;

                    ref var archPtr = ref uw->GetEntityArchetypePtr(entityId);
                    ref var arch = ref archPtr.Ref;

                    var entityName = $"Entity_{entityId}";
                    if (arch.Has(nameTypeIndex))
                    {
                        try
                        {
                            var boxed = arch.GetObject(entityId, nameTypeIndex);
                            if (boxed is Name nameComp && nameComp.value.Value != null)
                                entityName = nameComp.value.Value;
                        }
                        catch { }
                    }

                    _entityList.Add(new EntityInfo
                    {
                        id = entityId,
                        name = entityName,
                        archetype = BuildArchetypeLabel(ref arch),
                        alive = true,
                        components = null
                    });
                }
            }
            else
            {
                var listIdx = 0;
                for (var i = 0; i < alive.Length; i++)
                {
                    var entityId = alive[i];
                    if (entityId == 0) continue;

                    ref var archPtr = ref uw->GetEntityArchetypePtr(entityId);
                    ref var arch = ref archPtr.Ref;

                    var entityName = $"Entity_{entityId}";
                    if (arch.Has(nameTypeIndex))
                    {
                        try
                        {
                            var boxed = arch.GetObject(entityId, nameTypeIndex);
                            if (boxed is Name nameComp && nameComp.value.Value != null)
                                entityName = nameComp.value.Value;
                        }
                        catch { }
                    }

                    var info = _entityList[listIdx];
                    info.id = entityId;
                    info.name = entityName;
                    info.archetype = BuildArchetypeLabel(ref arch);
                    info.alive = true;
                    listIdx++;
                }
            }

            return _entityList;
        }

        public EntityInfo GetEntityDetails(int entityId)
        {
            if (!World.TryGet(_worldIndex, out var world))
            {
                return null;
            }

            var uw = world.UnsafeWorld;

            ref var archPtr = ref uw->GetEntityArchetypePtr(entityId);
            ref var arch = ref archPtr.Ref;

            var nameTypeIndex = -1;
            try { nameTypeIndex = ComponentType<Name>.Index; } catch { }

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

            return new EntityInfo
            {
                id = entityId,
                name = entityName,
                archetype = archetypeLabel,
                alive = true,
                components = components
            };
        }

        public List<ArchetypeInfo> GetArchetypes()
        {
            if (!IsWorldValid())
            {
                _archetypeList.Clear();
                return _archetypeList;
            }

            ref var world = ref GetWorld();
            var uw = world.UnsafeWorld;
            var archCount = uw->archetypesList.Length - 1;

            if (_archetypeList.Count != archCount)
            {
                _archetypeList.Clear();
                for (var i = 1; i < uw->archetypesList.Length; i++)
                {
                    ref var arch = ref uw->archetypesList.Ptr[i].Ref;
                    var compNames = new List<string>();
                    foreach (var typeIdx in arch.types)
                    {
                        var t = ComponentTypeMap.GetType(typeIdx);
                        compNames.Add(t?.Name ?? $"Type_{typeIdx}");
                    }

                    var entityIds = new List<int>();
                    for (var ei = 0; ei < arch.count; ei++)
                        entityIds.Add(arch.packedEntities.Ptr[ei]);

                    _archetypeList.Add(new ArchetypeInfo
                    {
                        id = i,
                        components = compNames,
                        entityCount = arch.count,
                        chunkCount = Mathf.Max(1, Mathf.CeilToInt((float)arch.count / 16f)),
                        entityIds = entityIds
                    });
                }
            }
            else
            {
                for (var idx = 0; idx < _archetypeList.Count; idx++)
                {
                    var i = idx + 1;
                    ref var arch = ref uw->archetypesList.Ptr[i].Ref;
                    var info = _archetypeList[idx];

                    info.id = i;
                    info.entityCount = arch.count;
                    info.chunkCount = Mathf.Max(1, Mathf.CeilToInt(arch.count / 16f));

                    if (info.components.Count != arch.types.length)
                    {
                        info.components.Clear();
                        foreach (var typeIdx in arch.types)
                        {
                            var t = ComponentTypeMap.GetType(typeIdx);
                            info.components.Add(t?.Name ?? $"Type_{typeIdx}");
                        }
                    }

                    info.entityIds.Clear();
                    for (var ei = 0; ei < arch.count; ei++)
                        info.entityIds.Add(arch.packedEntities.Ptr[ei]);
                }
            }

            return _archetypeList;
        }

        public List<QueryInfo> GetQueries()
        {
            if (!World.TryGet(_worldIndex, out var world))
            {
                _queryList.Clear();
                return _queryList;
            }

            var uw = world.UnsafeWorld;
            var qCount = uw->queries.Length;

            if (_queryList.Count != qCount)
            {
                _queryList.Clear();
                for (var i = 0; i < qCount; i++)
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

                    _queryList.Add(new QueryInfo
                    {
                        id = q.Id,
                        name = withList.Count > 0 ? string.Join("+", withList) : $"Query_{q.Id}",
                        with = withList,
                        without = withoutList,
                        matched = q.count,
                        lastRunMs = 0
                    });
                }
            }
            else
            {
                for (var i = 0; i < qCount; i++)
                {
                    ref var q = ref uw->queries.Ptr[i].Ref;
                    var info = _queryList[i];

                    info.id = q.Id;
                    info.matched = q.count;
                    info.lastRunMs = 0;
                    info.name = info.with.Count > 0 ? string.Join("+", info.with) : $"Query_{q.Id}";
                }
            }

            return _queryList;
        }

        public List<ResourceInfo> GetResources()
        {
            if (!World.TryGet(_worldIndex, out var world)) return _resourceList;
            var (len, resArray) = world.UnsafeWorldRef.resStorage.GetAll(_resources);
            _resources = resArray;

            if (_resourceList.Count > 0 && _resourceList.Count == len)
            {
                for (var i = 0; i < len; i++)
                {
                    var res = _resources[i];
                    var info = _resourceList[i];

                    _resourceFields.Clear();
                    try { ComponentFieldReader.ReadFields(res, _resourceFields); }
                    catch { continue; }

                    if (_resourceFields.Count == 1)
                    {
                        info.isScalar = true;
                        info.scalarValue = _resourceFields[0].Value;
                        info.value.Clear();
                    }
                    else if (_resourceFields.Count == 0)
                    {
                        info.isScalar = true;
                        info.scalarValue = FieldValue.FromBool(true);
                        info.value.Clear();
                    }
                    else
                    {
                        info.isScalar = false;
                        info.value.Clear();
                        foreach (var (key, val) in _resourceFields)
                            info.value[key] = val;
                    }
                }
                return _resourceList;
            }

            _resourceList.Clear();
            for (var i = 0; i < len; i++)
            {
                var res = _resources[i];
                var info = new ResourceInfo
                {
                    name = res.GetType().Name,
                    type = res.GetType().Name
                };
                _resourceFields.Clear();
                try { ComponentFieldReader.ReadFields(res, _resourceFields); }
                catch { }

                if (_resourceFields.Count == 1)
                {
                    info.isScalar = true;
                    info.scalarValue = _resourceFields[0].Value;
                }
                else if (_resourceFields.Count == 0)
                {
                    info.isScalar = true;
                    info.scalarValue = FieldValue.FromBool(true);
                }
                else
                {
                    info.isScalar = false;
                    foreach (var (key, val) in _resourceFields)
                        info.value[key] = val;
                }
                _resourceList.Add(info);
            }

            return _resourceList;
        }

        public EntityInfo CreateEntity()
        {
            if (!IsWorldValid()) return null;
            ref var world = ref GetWorld();
            ref var entity = ref world.Entity();
            return new EntityInfo
            {
                id = entity.id,
                name = $"Entity_{entity.id}",
                archetype = "Empty",
                alive = true,
                components = new List<ComponentInfo>()
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

            try
            {
                var uw = world.UnsafeWorld;
                byte* ptr = uw->GetComponentDataPtr(entityId, typeIndex);
                if (ptr != null)
                {
                    ComponentFieldAccessorCache.WriteFieldPointer(typeIndex, ptr, fieldKey, value);
                    return;
                }
            }
            catch { }

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
            return ComponentTypeMap.Index(name);
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
                    TypeIndex = typeIdx,
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
                        byte* ptr;
                        if (ctData.storageType == StorageType.Pool)
                        {
                            ref var pool = ref uw->GetUntypedPool(typeIdx);
                            ptr = pool.UnsafeGetPtr(entityId);
                        }
                        else
                        {
                            ref var loc = ref uw->entityLocations.Ptr[entityId];
                            ptr = arch.GetComponentDataPtr(typeIdx, loc.row);
                        }

                        if (ptr != null)
                            ComponentFieldAccessorCache.ReadFieldsPointer(typeIdx, ptr, info.Fields);
                    }
                    catch
                    {
                        try
                        {
                            var boxed = arch.GetObject(entityId, typeIdx);
                            if (boxed != null)
                                ComponentFieldReader.ReadFields(boxed, info.Fields);
                        }
                        catch { }
                    }
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

        internal static void ReadSingleField(FieldInfo fi, object obj,
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
            else if (ft == typeof(Vector2) || ft.Name == "float2")
            {
                var v = (Vector2)val;
                fields.Add(($"{fi.Name}.x", FieldValue.FromNumber(v.x)));
                fields.Add(($"{fi.Name}.y", FieldValue.FromNumber(v.y)));
            }
            else if (ft == typeof(Vector3) || ft.Name == "float3")
            {
                var v = (Vector3)val;
                fields.Add(($"{fi.Name}.x", FieldValue.FromNumber(v.x)));
                fields.Add(($"{fi.Name}.y", FieldValue.FromNumber(v.y)));
                fields.Add(($"{fi.Name}.z", FieldValue.FromNumber(v.z)));
            }
            else if (ft == typeof(Vector4) || ft.Name == "float4")
            {
                var v = (Vector4)val;
                fields.Add(($"{fi.Name}.x", FieldValue.FromNumber(v.x)));
                fields.Add(($"{fi.Name}.y", FieldValue.FromNumber(v.y)));
                fields.Add(($"{fi.Name}.z", FieldValue.FromNumber(v.z)));
                fields.Add(($"{fi.Name}.w", FieldValue.FromNumber(v.w)));
            }
            else if (ft == typeof(Quaternion) || ft.Name == "quaternion")
            {
                var v = (Quaternion)val;
                fields.Add(($"{fi.Name}.x", FieldValue.FromNumber(v.x)));
                fields.Add(($"{fi.Name}.y", FieldValue.FromNumber(v.y)));
                fields.Add(($"{fi.Name}.z", FieldValue.FromNumber(v.z)));
                fields.Add(($"{fi.Name}.w", FieldValue.FromNumber(v.w)));
            }
            else if (ft == typeof(Color))
            {
                var v = (Color)val;
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

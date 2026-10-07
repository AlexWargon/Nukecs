using System.Collections.Generic;
using UnityEngine;
using System;
using Object = UnityEngine.Object;
#if UNITY_6000_4_OR_NEWER
using ObjectId = UnityEngine.EntityId;
#else
using ObjectId = System.Int32;
#endif

namespace Wargon.Nukecs.Tests {
    public static class EntityPrefabMap {
        private struct Entry {
            internal Entity Prefab;
            internal int WorldId;
            internal string Name;
        }
        private static Dictionary<ObjectId, Entry> Map = new ();

        public static void Dispose() {
            Map.Clear();
        }
        public static void Add(ObjectId id, Entity entity) {
            Map[id] = new Entry { Prefab = entity, WorldId = entity.world.Id,
                Name = entity.Has<Name>() ? entity.Get<Name>().value.Value : null };
        }

        public static Entity Spawn(ObjectId id) {
            var prefab = GetPrefab(id);
            return prefab.world.SpawnPrefab(in prefab);
        }
        public static Entity Spawn<T>(T obj, ref World world) where T : Object, ICustomConvertor {
            var prefab = GetOrCreatePrefab(obj, ref world);
            return world.SpawnPrefab(in prefab);
        }
        public static Entity GetPrefab(ObjectId id) {
            var entry = Map[id];
            ref var world = ref World.Get(entry.WorldId);
            if (!TryResolve(id, ref world, ref entry, out var prefab))
                throw new InvalidOperationException("The prefab is absent from the loaded world. Resolve it from its source converter.");
            return prefab;
        }
        public static Entity GetOrCreatePrefab<T>(T obj, ref World world) where T : Object, ICustomConvertor {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            var id = UnityObjectId.Of(obj);
            if (!Map.TryGetValue(id, out var entry))
                entry = new Entry { WorldId = world.Id, Name = obj.name };
            else if (entry.Name == null) entry.Name = obj.name;
            if (!TryResolve(id, ref world, ref entry, out var prefab)) {
                var e = world.Entity();
                obj.Convert(ref world, ref e);
                e.Add(new IsPrefab());
                world.Update();
                if (!e.Has<Name>()) {
                    e.Add(new Name(obj.name));
                    world.Update();
                }
                Map[id] = new Entry { Prefab = e, WorldId = world.Id, Name = e.Get<Name>().value.Value };
                return e;
            }

            return prefab;
        }
        public static bool TryGet(ObjectId id, out Entity entity) {
            if (Map.TryGetValue(id, out var entry)) {
                ref var world = ref World.Get(entry.WorldId);
                if (TryResolve(id, ref world, ref entry, out var prefab)) {
                    entity = world.SpawnPrefab(in prefab);
                    return true;
                }
            }
            entity = Entity.Null;
            return false;
        }

        private static bool TryResolve(ObjectId id, ref World world, ref Entry entry, out Entity prefab) {
            prefab = Entity.Null;
            if (!world.IsAlive) return false;
            if (entry.WorldId == world.Id && entry.Prefab.IsValid()) {
                prefab = entry.Prefab;
                return true;
            }
            if (entry.Name == null) return false;
            var candidates = world.Query(withDefaultNoneTypes: false).With<IsPrefab>().With<Name>();
            foreach (var candidate in candidates) {
                if (candidate.Get<Name>().value.Value != entry.Name) continue;
                if (prefab.IsValid())
                    throw new InvalidOperationException("Several prefabs have the same name. Give prefab sources unique names before loading.");
                prefab = candidate;
            }
            if (!prefab.IsValid()) return false;
            entry.Prefab = prefab;
            entry.WorldId = world.Id;
            Map[id] = entry;
            return true;
        }
    }
}

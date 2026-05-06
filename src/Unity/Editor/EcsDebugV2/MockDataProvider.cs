#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public class MockDataProvider : IEcsDataProvider
    {
        private List<EntityInfo> _entities;
        private List<ArchetypeInfo> _archetypes;
        private List<QueryInfo> _queries;
        private List<ResourceInfo> _resources;
        private int _tick;

        public int SystemCount => 12;
        public int Tick { get => _tick; set => _tick = value; }
        public WorldInfo WorldInfo => new WorldInfo { Name = "Empty", WorldNames = new[] { "Empty" }, WorldSlots = new[] { 0 } };
        public string[] AvailableComponentTypes => MockData.ALL_COMPONENT_TYPES;
        public int WorldCount => 1;

        public void Initialize(int entityCount = 72)
        {
            _entities = MockData.BuildMockEntities(entityCount);
            _queries = MockData.BuildQueries();
            _resources = MockData.BuildResources();
            RebuildArchetypes();
            MockData.UpdateQueryMatches(_queries, _entities);
        }
        public void InitializeEmpty()
        {
            _entities = Array.Empty<EntityInfo>().ToList();
            _queries = Array.Empty<QueryInfo>().ToList();
            _resources = Array.Empty<ResourceInfo>().ToList();
            RebuildArchetypes();
            MockData.UpdateQueryMatches(_queries, _entities);
        }
        public List<EntityInfo> GetEntities() => _entities;
        public List<EntityInfo> GetEntityList() => _entities;
        public EntityInfo GetEntityDetails(int entityId)
        {
            if (_entities == null) return null;
            for (int i = 0; i < _entities.Count; i++)
                if (_entities[i].Id == entityId) return _entities[i];
            return null;
        }
        public List<ArchetypeInfo> GetArchetypes() => _archetypes;
        public List<QueryInfo> GetQueries() => _queries;
        public List<ResourceInfo> GetResources() => _resources;

        public EntityInfo CreateEntity()
        {
            int nextId = 1000;
            foreach (var e in _entities)
                if (e.Id >= nextId) nextId = e.Id + 1;

            var newEnt = new EntityInfo
            {
                Id = nextId,
                Name = $"Entity_{nextId}",
                Archetype = "Custom",
                Alive = true,
                Components = new List<ComponentInfo>
                {
                    MockData.MakeComponentByName("Transform")
                }
            };
            _entities.Add(newEnt);
            RebuildArchetypes();
            MockData.UpdateQueryMatches(_queries, _entities);
            return newEnt;
        }

        public void DestroyEntity(int id)
        {
            _entities.RemoveAll(e => e.Id == id);
            RebuildArchetypes();
            MockData.UpdateQueryMatches(_queries, _entities);
        }

        public void AddComponent(int entityId, string compName)
        {
            var entity = _entities.Find(e => e.Id == entityId);
            if (entity == null) return;
            entity.Components.Add(MockData.MakeComponentByName(compName));
            RebuildArchetypes();
            MockData.UpdateQueryMatches(_queries, _entities);
        }

        public void RemoveComponent(int entityId, string compName)
        {
            var entity = _entities.Find(e => e.Id == entityId);
            if (entity == null) return;
            entity.Components.RemoveAll(c => c.Name == compName);
            RebuildArchetypes();
            MockData.UpdateQueryMatches(_queries, _entities);
        }

        public void SetFieldValue(int entityId, string compName, string fieldKey, FieldValue value)
        {
            var entity = _entities.Find(e => e.Id == entityId);
            if (entity == null) return;
            var comp = entity.Components.Find(c => c.Name == compName);
            if (comp == null) return;
            if (comp.HasField(fieldKey))
                comp.SetField(fieldKey, value);
        }

        public void SimulateTick(Dictionary<string, long> changes)
        {
            MockData.MutateRandomFields(_entities, 6 + Random.Range(0, 8), changes);
            MockData.UpdateQueryMatches(_queries, _entities);
        }

        private void RebuildArchetypes()
        {
            _archetypes = MockData.BuildArchetypes(_entities);
        }

        public void SetWorld(int worldIndex) { }

        public int GetEntityCount() => _entities?.Count ?? 0;

        public int GetArchetypeCount() => _archetypes?.Count ?? 0;

        public int GetEntityArchetypeIndex(int id)
        {
            var entity = _entities?.Find(e => e.Id == id);
            return entity != null ? entity.Archetype.GetHashCode() : -1;
        }
    }
}
#endif

#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System.Collections.Generic;
using UnityEngine;

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
        public WorldInfo WorldInfo => new WorldInfo { Name = "world::main", WorldNames = new[] { "world::main" } };
        public string[] AvailableComponentTypes => MockData.ALL_COMPONENT_TYPES;

        public void Initialize(int entityCount = 72)
        {
            _entities = MockData.BuildMockEntities(entityCount);
            _queries = MockData.BuildQueries();
            _resources = MockData.BuildResources();
            RebuildArchetypes();
            MockData.UpdateQueryMatches(_queries, _entities);
        }

        public List<EntityInfo> GetEntities() => _entities;
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
    }
}
#endif

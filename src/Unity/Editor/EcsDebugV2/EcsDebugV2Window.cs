#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public class EcsDebugV2Window : EditorWindow
    {
        public List<MockEntity> Entities;
        public List<MockArchetype> Archetypes;
        public List<MockQuery> Queries;
        public List<MockResource> Resources;
        public Dictionary<string, long> Changes = new Dictionary<string, long>();

        public TabKey CurrentTab = TabKey.Entities;
        public bool Paused;
        public int Tick;
        public int? SelectedEntityId;
        public int? SelectedArchetypeId;
        public string SelectedQueryId;
        public string SelectedResourceName;
        public string SearchQuery;
        public string ArchetypeFilter;

        private VisualElement _topPanel;
        private VisualElement _tabBar;
        private VisualElement _leftPanel;
        private VisualElement _inspectorPanel;
        private VisualElement _footer;

        [MenuItem("Nuke.cs/ECS Debug V2")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<EcsDebugV2Window>();
            wnd.titleContent = new GUIContent("ECS Debug V2");
            wnd.minSize = new Vector2(900, 600);
        }

        public void CreateGUI()
        {
            Entities = MockData.BuildMockEntities(72);
            Queries = MockData.BuildQueries();
            Resources = MockData.BuildResources();
            RebuildArchetypes();
            MockData.UpdateQueryMatches(Queries, Entities);

            SelectedEntityId = Entities.Count > 0 ? Entities[0].Id : (int?)null;
            if (Archetypes.Count > 0) SelectedArchetypeId = Archetypes[0].Id;
            if (Queries.Count > 0) SelectedQueryId = Queries[0].Id;
            if (Resources.Count > 0) SelectedResourceName = Resources[0].Name;

            var root = rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Column;
            root.style.backgroundColor = EcsDebugV2Theme.Background;

            var outerCard = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = EcsDebugV2Theme.PanelBorder,
                    borderBottomColor = EcsDebugV2Theme.PanelBorder,
                    borderLeftColor = EcsDebugV2Theme.PanelBorder,
                    borderRightColor = EcsDebugV2Theme.PanelBorder,
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8,
                    borderBottomRightRadius = 8,
                    overflow = Overflow.Hidden,
                    backgroundColor = EcsDebugV2Theme.Panel
                }
            };
            root.Add(outerCard);

            _topPanel = TopPanel.Create(this);
            outerCard.Add(_topPanel);

            _tabBar = TabBar.Create(this);
            outerCard.Add(_tabBar);

            var mainArea = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 1,
                    overflow = Overflow.Hidden,
                    height = 620
                }
            };

            _leftPanel = new VisualElement
            {
                name = "left-panel",
                style =
                {
                    width = Length.Percent(40),
                    minWidth = 320,
                    borderRightWidth = 1,
                    borderRightColor = EcsDebugV2Theme.PanelBorder,
                    overflow = Overflow.Hidden,
                    flexDirection = FlexDirection.Column
                }
            };
            mainArea.Add(_leftPanel);

            _inspectorPanel = new VisualElement
            {
                name = "inspector-container",
                style =
                {
                    flexGrow = 1,
                    flexDirection = FlexDirection.Column,
                    overflow = Overflow.Hidden,
                    backgroundColor = EcsDebugV2Theme.Background.WithAlpha(0.4f)
                }
            };
            mainArea.Add(_inspectorPanel);
            outerCard.Add(mainArea);

            _footer = Footer.Create(this);
            outerCard.Add(_footer);

            RefreshLeftPanel();
            RefreshInspector();

            root.schedule.Execute(() =>
            {
                if (Paused) return;
                Tick++;
                MockData.MutateRandomFields(Entities, 6 + UnityEngine.Random.Range(0, 8), Changes);
                MockData.UpdateQueryMatches(Queries, Entities);
                RefreshLeftPanel();
                RefreshInspector();
                TopPanel.Update(_topPanel, this);
                Footer.Update(_footer, this);
            }).Every(600);
        }

        public void SetTab(TabKey tab)
        {
            if (CurrentTab == tab) return;
            CurrentTab = tab;
            TabBar.Refresh(_tabBar, this);
            RefreshLeftPanel();
            RefreshInspector();
        }

        public void TogglePause()
        {
            Paused = !Paused;
            TopPanel.Update(_topPanel, this);
        }

        public void SelectEntity(int id)
        {
            SelectedEntityId = id;
            if (CurrentTab == TabKey.Entities)
                RefreshLeftPanel();
            RefreshInspector();
        }

        public void SelectArchetype(int id)
        {
            SelectedArchetypeId = id;
            if (CurrentTab == TabKey.Archetypes)
                RefreshLeftPanel();
            RefreshInspector();
        }

        public void SelectQuery(string id)
        {
            SelectedQueryId = id;
            if (CurrentTab == TabKey.Queries)
                RefreshLeftPanel();
            RefreshInspector();
        }

        public void SelectResource(string name)
        {
            SelectedResourceName = name;
            if (CurrentTab == TabKey.Resources)
                RefreshLeftPanel();
            RefreshInspector();
        }

        public void CreateEntity()
        {
            int nextId = 1000;
            foreach (var e in Entities)
                if (e.Id >= nextId) nextId = e.Id + 1;

            var newEnt = new MockEntity
            {
                Id = nextId,
                Name = $"Entity_{nextId}",
                Archetype = "Custom",
                Alive = true,
                Components = new System.Collections.Generic.List<ComponentInstance>
                {
                    MockData.MakeComponentByName("Transform")
                }
            };
            Entities.Add(newEnt);
            SelectedEntityId = nextId;
            CurrentTab = TabKey.Entities;
            RebuildArchetypes();
            MockData.UpdateQueryMatches(Queries, Entities);
            TabBar.Refresh(_tabBar, this);
            RefreshLeftPanel();
            RefreshInspector();
        }

        public void DestroyEntity(int id)
        {
            Entities.RemoveAll(e => e.Id == id);
            if (SelectedEntityId == id)
                SelectedEntityId = Entities.Count > 0 ? Entities[0].Id : (int?)null;
            RebuildArchetypes();
            MockData.UpdateQueryMatches(Queries, Entities);
            RefreshLeftPanel();
            RefreshInspector();
        }

        public void RemoveComponent(int entityId, string compName)
        {
            var entity = Entities.Find(e => e.Id == entityId);
            if (entity == null) return;
            entity.Components.RemoveAll(c => c.Name == compName);
            RebuildArchetypes();
            MockData.UpdateQueryMatches(Queries, Entities);
            RefreshInspector();
        }

        public void AddComponent(int entityId, string compName)
        {
            var entity = Entities.Find(e => e.Id == entityId);
            if (entity == null) return;
            entity.Components.Add(MockData.MakeComponentByName(compName));
            RebuildArchetypes();
            MockData.UpdateQueryMatches(Queries, Entities);
            RefreshInspector();
        }

        public void SetFieldValue(int entityId, string compName, string fieldKey, FieldValue value)
        {
            var entity = Entities.Find(e => e.Id == entityId);
            if (entity == null) return;
            var comp = entity.Components.Find(c => c.Name == compName);
            if (comp == null) return;
            if (comp.Fields.ContainsKey(fieldKey))
                comp.Fields[fieldKey] = value;
            Changes[$"{entityId}:{compName}:{fieldKey}"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            RefreshInspector();
        }

        private void RefreshLeftPanel()
        {
            _leftPanel.Clear();
            switch (CurrentTab)
            {
                case TabKey.Entities:
                    _leftPanel.Add(EntitiesTab.Create(this));
                    break;
                case TabKey.Archetypes:
                    _leftPanel.Add(ArchetypesList.Create(this));
                    break;
                case TabKey.Queries:
                    _leftPanel.Add(QueriesList.Create(this));
                    break;
                case TabKey.Resources:
                    _leftPanel.Add(ResourcesList.Create(this));
                    break;
            }
        }

        private void RefreshInspector()
        {
            if (_inspectorPanel == null) return;
            _inspectorPanel.Clear();
            _inspectorPanel.Add(InspectorPanel.Create(this));
        }

        private void RebuildArchetypes()
        {
            Archetypes = MockData.BuildArchetypes(Entities);
        }
    }
}
#endif

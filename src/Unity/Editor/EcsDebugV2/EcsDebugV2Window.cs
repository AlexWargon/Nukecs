#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
// ReSharper disable HeapView.CanAvoidClosure
// ReSharper disable EmptyGeneralCatchClause
// ReSharper disable ParameterHidesMember

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public class EcsDebugV2Window : EditorWindow
    {
        public IEcsDataProvider provider;
        public List<EntityInfo> entities;
        public List<ArchetypeInfo> archetypes;
        public List<QueryInfo> queries;
        public List<ResourceInfo> resources;
        public Dictionary<string, long> changes = new ();
        public Dictionary<int, EntityInfo> entityMap = new ();
        public Dictionary<int, ArchetypeInfo> archetypeMap = new ();
        public Dictionary<int, QueryInfo> queryMap = new ();
        public Dictionary<string, ResourceInfo> resourceMap = new ();

        public TabKey currentTab = TabKey.Entities;
        public bool paused;
        public int tick;
        public int systemCount;
        public int? selectedEntityId;
        public EntityInfo selectedEntityDetails;
        public int? selectedArchetypeId;
        public int selectedQueryId;
        public string selectedResourceName;
        public string searchQuery;
        public string archetypeFilter;
        public List<int> filteredEntityIds = new ();

        private VisualElement _topPanel;
        private VisualElement _tabBar;
        private VisualElement _leftPanel;
        private VisualElement _splitter;
        private VisualElement _inspectorPanel;
        private VisualElement _footer;

        private bool _disposed;
        private bool _isDraggingSplitter;
        private float _dragStartX;
        private float _dragStartWidth;
        private float _leftPanelWidth = 380f;
        private int _lastEntityCount = -1;
        private int _lastArchetypeIndex = -1;
        private int _lastArchetypeCount = -1;
        private readonly List<string> _changesCleanupKeys = new List<string>();

        [MenuItem("Nuke.cs/ECS Debug V2")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<EcsDebugV2Window>();
            wnd.titleContent = new GUIContent("ECS Debug V2");
            wnd.minSize = new Vector2(500, 300);
        }

        public void CreateGUI()
        {
            if (provider == null)
            {
                if (EditorApplication.isPlaying && World.HasActiveWorlds())
                    provider = new LiveDataProvider();
                else
                {
                    provider = new MockDataProvider();
                    ((MockDataProvider)provider).InitializeEmpty();
                }
            }

            entities = provider.GetEntityList();
            queries = provider.GetQueries();
            resources = provider.GetResources();
            archetypes = provider.GetArchetypes();
            RebuildMaps();
            systemCount = provider.SystemCount;

            selectedEntityId = entities.Count > 0 ? entities[0].Id : null;
            if (selectedEntityId.HasValue)
                selectedEntityDetails = provider.GetEntityDetails(selectedEntityId.Value);
            if (archetypes.Count > 0) selectedArchetypeId = archetypes[0].Id;
            if (queries.Count > 0) selectedQueryId = queries[0].Id;
            if (resources.Count > 0) selectedResourceName = resources[0].Name;

            var root = rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Column;
            root.style.backgroundColor = EcsDebugV2Theme.Background;

            var outerCard = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    overflow = Overflow.Hidden,
                    backgroundColor = EcsDebugV2Theme.Panel
                }
            };
            outerCard.SetupBorder(EcsDebugV2Theme.PanelBorder);
            outerCard.SetupRadius(EcsDebugV2Theme.CardRadius);
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
                    overflow = Overflow.Hidden
                }
            };

            _leftPanel = new VisualElement
            {
                name = "left-panel",
                style =
                {
                    width = _leftPanelWidth,
                    minWidth = 250,
                    overflow = Overflow.Hidden,
                    flexDirection = FlexDirection.Column
                }
            };
            mainArea.Add(_leftPanel);

            _splitter = new VisualElement
            {
                style =
                {
                    width = 4,
                    backgroundColor = EcsDebugV2Theme.PanelBorder,
                    flexShrink = 0
                }
            };
            _splitter.RegisterCallback<MouseEnterEvent>(_ =>
                _splitter.style.backgroundColor = EcsDebugV2Theme.LimeA05);
            _splitter.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (!_isDraggingSplitter)
                    _splitter.style.backgroundColor = EcsDebugV2Theme.PanelBorder;
            });
            _splitter.RegisterCallback<MouseDownEvent>(evt =>
            {
                _isDraggingSplitter = true;
                _dragStartX = evt.mousePosition.x;
                _dragStartWidth = _leftPanelWidth;
                evt.StopPropagation();
            });
            mainArea.Add(_splitter);

            _inspectorPanel = new VisualElement
            {
                name = "inspector-container",
                style =
                {
                    flexGrow = 1,
                    flexDirection = FlexDirection.Column,
                    overflow = Overflow.Hidden,
                    backgroundColor = EcsDebugV2Theme.BgA04
                }
            };
            mainArea.Add(_inspectorPanel);
            outerCard.Add(mainArea);

            root.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (!_isDraggingSplitter) return;
                var delta = evt.mousePosition.x - _dragStartX;
                _leftPanelWidth = Mathf.Clamp(_dragStartWidth + delta, 250f, 800f);
                _leftPanel.style.width = _leftPanelWidth;
            });
            root.RegisterCallback<MouseUpEvent>(_ =>
            {
                if (_isDraggingSplitter)
                {
                    _isDraggingSplitter = false;
                    _splitter.style.backgroundColor = EcsDebugV2Theme.PanelBorder;
                }
            });

            _footer = Footer.Create(this);
            outerCard.Add(_footer);

            RefreshLeftPanel();
            RefreshInspector();

            root.schedule.Execute(() =>
            {
                if (_disposed || paused) return;
                if (rootVisualElement.panel == null) return;

                try
                {
                    if (provider is MockDataProvider)
                    {
                        tick++;
                        provider.Tick = tick;
                        provider.SimulateTick(changes);
                    }
                    else
                    {
                        tick = provider.Tick;
                    }
                }
                catch { }

                try
                {
                    if (tick % 60 == 0)
                    {
                        var cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 2000;
                        _changesCleanupKeys.Clear();
                        foreach (var kv in changes)
                            if (kv.Value < cutoff) _changesCleanupKeys.Add(kv.Key);
                        for (int i = 0; i < _changesCleanupKeys.Count; i++)
                            changes.Remove(_changesCleanupKeys[i]);
                    }
                }
                catch { }

                var currentEntityCount = provider.GetEntityCount();
                var currentArchCount = provider.GetArchetypeCount();
                var entitiesChanged = currentEntityCount != _lastEntityCount || _lastArchetypeCount != currentArchCount;

                if (entitiesChanged)
                {
                    _lastEntityCount = currentEntityCount;
                    _lastArchetypeCount = currentArchCount;
                    try { entities = provider.GetEntityList(); } catch { }
                    try { archetypes = provider.GetArchetypes(); } catch { }
                    if (currentTab == TabKey.Queries)
                        try { queries = provider.GetQueries(); } catch { }
                    if (currentTab == TabKey.Resources)
                        try { resources = provider.GetResources(); } catch { }
                    RebuildMaps();
                    try { RefreshLeftPanel(); } catch { }
                }

                if (selectedEntityId.HasValue && !EditingTextField(_inspectorPanel))
                {
                    try { selectedEntityDetails = provider.GetEntityDetails(selectedEntityId.Value); } catch { }

                    var archIndex = provider.GetEntityArchetypeIndex(selectedEntityId.Value);
                    if (archIndex != _lastArchetypeIndex)
                    {
                        _lastArchetypeIndex = archIndex;
                        try { RefreshInspector(); } catch { }
                    }
                    else
                    {
                        try { InspectorPanel.UpdateValues(_inspectorPanel, this); } catch { }
                    }
                }

                if (tick % 10 == 0)
                {
                    try { TopPanel.Update(_topPanel, this); } catch { }
                    try { Footer.Update(_footer, this); } catch { }

                    if (currentTab == TabKey.Archetypes)
                        try { ArchetypesList.UpdateValues(_leftPanel, this); } catch { }
                    else if (currentTab == TabKey.Queries)
                        try { QueriesList.UpdateValues(_leftPanel, this); } catch { }
                    else if (currentTab == TabKey.Resources)
                        try { ResourcesList.UpdateValues(_leftPanel, this); } catch { }
                }
            }).Every(100);
        }

        public void SwitchToWorld(int worldIndex)
        {
            if (provider is LiveDataProvider ldp)
                ldp.SetWorld(worldIndex);
            InvalidateEntityCache();
            entities = provider.GetEntityList();
            archetypes = provider.GetArchetypes();
            queries = provider.GetQueries();
            resources = provider.GetResources();
            RebuildMaps();
            systemCount = provider.SystemCount;
            selectedEntityId = entities.Count > 0 ? entities[0].Id : null;
            selectedEntityDetails = selectedEntityId.HasValue ? provider.GetEntityDetails(selectedEntityId.Value) : null;
            if (archetypes.Count > 0) selectedArchetypeId = archetypes[0].Id;
            if (queries.Count > 0) selectedQueryId = queries[0].Id;
            if (resources.Count > 0) selectedResourceName = resources[0].Name;
            RefreshLeftPanel();
            RefreshInspector();
            TopPanel.Update(_topPanel, this);
        }

        void OnDestroy()
        {
            _disposed = true;
        }

        public void SetTab(TabKey tab)
        {
            if (currentTab == tab) return;
            currentTab = tab;
            _lastEntityCount = -1;
            _lastArchetypeIndex = -1;
            if (selectedEntityId.HasValue)
                selectedEntityDetails = provider.GetEntityDetails(selectedEntityId.Value);
            TabBar.Refresh(_tabBar, this);
            RefreshLeftPanel();
            RefreshInspector();
        }

        public void TogglePause()
        {
            paused = !paused;
            TopPanel.Update(_topPanel, this);
            Footer.Update(_footer, this);
        }

        public void SelectEntity(int id)
        {
            selectedEntityId = id;
            selectedEntityDetails = provider.GetEntityDetails(id);
            _lastArchetypeIndex = -1;
            if (currentTab == TabKey.Entities)
                EntitiesTab.RefreshSelection(_leftPanel, this);
            RefreshInspector();
        }

        public void SelectEntityFromArchetype(int id)
        {
            selectedEntityId = id;
            selectedEntityDetails = provider.GetEntityDetails(id);
            _lastArchetypeIndex = -1;
            currentTab = TabKey.Entities;
            TabBar.Refresh(_tabBar, this);
            RefreshLeftPanel();
            RefreshInspector();
        }

        public void SelectArchetype(int id)
        {
            selectedArchetypeId = id;
            if (currentTab == TabKey.Archetypes)
                RefreshLeftPanel();
            RefreshInspector();
        }

        public void SelectQuery(int id)
        {
            selectedQueryId = id;
            if (currentTab == TabKey.Queries)
                RefreshLeftPanel();
            RefreshInspector();
        }

        public void SelectResource(string name)
        {
            selectedResourceName = name;
            if (currentTab == TabKey.Resources)
                RefreshLeftPanel();
            RefreshInspector();
        }

        public void CreateEntity()
        {
            var newEnt = provider.CreateEntity();
            entities = provider.GetEntityList();
            archetypes = provider.GetArchetypes();
            queries = provider.GetQueries();
            RebuildMaps();
            selectedEntityId = newEnt.Id;
            selectedEntityDetails = provider.GetEntityDetails(newEnt.Id);
            currentTab = TabKey.Entities;
            TabBar.Refresh(_tabBar, this);
            RefreshLeftPanel();
            RefreshInspector();
            TopPanel.Update(_topPanel, this);
        }

        public void DestroyEntity(int id)
        {
            provider.DestroyEntity(id);
            entities = provider.GetEntityList();
            archetypes = provider.GetArchetypes();
            queries = provider.GetQueries();
            RebuildMaps();
            if (selectedEntityId == id)
            {
                selectedEntityId = entities.Count > 0 ? entities[0].Id : null;
                selectedEntityDetails = selectedEntityId.HasValue
                    ? provider.GetEntityDetails(selectedEntityId.Value)
                    : null;
            }
            RefreshLeftPanel();
            RefreshInspector();
            TopPanel.Update(_topPanel, this);
        }

        public void RemoveComponent(int entityId, string compName)
        {
            provider.RemoveComponent(entityId, compName);
            entities = provider.GetEntityList();
            archetypes = provider.GetArchetypes();
            queries = provider.GetQueries();
            RebuildMaps();
            selectedEntityDetails = provider.GetEntityDetails(entityId);
            RefreshInspector();
        }

        public void AddComponent(int entityId, string compName)
        {
            provider.AddComponent(entityId, compName);
            entities = provider.GetEntityList();
            archetypes = provider.GetArchetypes();
            queries = provider.GetQueries();
            RebuildMaps();
            selectedEntityDetails = provider.GetEntityDetails(entityId);
            RefreshInspector();
        }

        public void SetFieldValue(int entityId, string compName, string fieldKey, FieldValue value)
        {
            provider.SetFieldValue(entityId, compName, fieldKey, value);
            changes[$"{entityId}:{compName}:{fieldKey}"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            selectedEntityDetails = provider.GetEntityDetails(entityId);
            RefreshInspector();
        }

        public void RebuildMaps()
        {
            entityMap.Clear();
            if (entities != null)
            {
                for (int i = 0; i < entities.Count; i++)
                    entityMap[entities[i].Id] = entities[i];
            }

            archetypeMap.Clear();
            if (archetypes != null)
            {
                for (int i = 0; i < archetypes.Count; i++)
                    archetypeMap[archetypes[i].Id] = archetypes[i];
            }

            queryMap.Clear();
            if (queries != null)
            {
                for (int i = 0; i < queries.Count; i++)
                    queryMap[queries[i].Id] = queries[i];
            }

            resourceMap.Clear();
            if (resources != null)
            {
                for (int i = 0; i < resources.Count; i++)
                    resourceMap[resources[i].Name] = resources[i];
            }
        }

        private void InvalidateEntityCache()
        {
            _lastEntityCount = -1;
            _lastArchetypeIndex = -1;
            _lastArchetypeCount = -1;
        }

        private void RefreshLeftPanel()
        {
            if (currentTab == TabKey.Entities && _leftPanel.Q<ListView>("entity-list") != null)
            {
                EntitiesTab.Refresh(_leftPanel, this);
                return;
            }

            var oldScroll = _leftPanel.Query<ScrollView>().First();
            var savedOffset = oldScroll?.scrollOffset ?? Vector2.zero;

            _leftPanel.Clear();
            switch (currentTab)
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

            var newScroll = _leftPanel.Query<ScrollView>().First();
            if (newScroll != null) newScroll.scrollOffset = savedOffset;
        }

        private void RefreshInspector()
        {
            if (_inspectorPanel == null) return;
            var oldScroll = _inspectorPanel.Query<ScrollView>().First();
            var savedOffset = oldScroll?.scrollOffset ?? Vector2.zero;

            _inspectorPanel.Clear();
            _inspectorPanel.Add(InspectorPanel.Create(this));

            var newScroll = _inspectorPanel.Query<ScrollView>().First();
            if (newScroll != null) newScroll.scrollOffset = savedOffset;
        }

        private bool EditingTextField(VisualElement root)
        {
            try
            {
                if (root?.panel?.focusController == null) return false;
                var focused = root.panel.focusController.focusedElement as VisualElement;
                if (focused == null || !root.Contains(focused)) return false;
                var current = focused;
                while (current != null && current != root)
                {
                    if (current is TextField) return true;
                    current = current.parent;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif

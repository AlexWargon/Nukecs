#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public class EcsDebugV2Window : EditorWindow
    {
        public IEcsDataProvider Provider;
        public List<EntityInfo> Entities;
        public List<ArchetypeInfo> Archetypes;
        public List<QueryInfo> Queries;
        public List<ResourceInfo> Resources;
        public Dictionary<string, long> Changes = new Dictionary<string, long>();

        public TabKey CurrentTab = TabKey.Entities;
        public bool Paused;
        public int Tick;
        public int SystemCount;
        public int? SelectedEntityId;
        public int? SelectedArchetypeId;
        public string SelectedQueryId;
        public string SelectedResourceName;
        public string SearchQuery;
        public string ArchetypeFilter;
        public List<int> FilteredEntityIds = new List<int>();

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
        private int _lastComponentHash = -1;

        [MenuItem("Nuke.cs/ECS Debug V2")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<EcsDebugV2Window>();
            wnd.titleContent = new GUIContent("ECS Debug V2");
            wnd.minSize = new Vector2(500, 300);
        }

        public void CreateGUI()
        {
            if (Provider == null)
            {
                if (EditorApplication.isPlaying && World.HasActiveWorlds())
                    Provider = new LiveDataProvider();
                else
                {
                    Provider = new MockDataProvider();
                    ((MockDataProvider)Provider).Initialize(72);
                }
            }

            Entities = Provider.GetEntities();
            Queries = Provider.GetQueries();
            Resources = Provider.GetResources();
            Archetypes = Provider.GetArchetypes();
            SystemCount = Provider.SystemCount;

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
                _splitter.style.backgroundColor = EcsDebugV2Theme.Lime.WithAlpha(0.5f));
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
                    backgroundColor = EcsDebugV2Theme.Background.WithAlpha(0.4f)
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
            root.RegisterCallback<MouseUpEvent>(evt =>
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
                if (_disposed || Paused) return;

                try
                {
                    if (Provider is MockDataProvider)
                    {
                        Tick++;
                        Provider.Tick = Tick;
                        Provider.SimulateTick(Changes);
                    }
                    else
                    {
                        Tick = Provider.Tick;
                    }
                }
                catch { }

                try
                {
                    if (Tick % 60 == 0)
                    {
                        var cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 2000;
                        var oldKeys = Changes.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList();
                        foreach (var k in oldKeys) Changes.Remove(k);
                    }
                }
                catch { }

                try { Entities = Provider.GetEntities(); } catch { }
                try { Queries = Provider.GetQueries(); } catch { }
                try { Resources = Provider.GetResources(); } catch { }
                try { Archetypes = Provider.GetArchetypes(); } catch { }

                var entityCount = Entities != null ? Entities.Count : 0;
                if (entityCount != _lastEntityCount)
                {
                    _lastEntityCount = entityCount;
                    try { RefreshLeftPanel(); } catch { }
                }

                var compHash = GetSelectedEntityComponentHash();
                if (compHash != _lastComponentHash)
                {
                    _lastComponentHash = compHash;
                    try { RefreshInspector(); } catch { }
                }
                else if (!EditingTextField(_inspectorPanel))
                {
                    try { InspectorPanel.UpdateValues(_inspectorPanel, this); } catch { }
                }

                try { TopPanel.Update(_topPanel, this); } catch { }
                try { Footer.Update(_footer, this); } catch { }
            }).Every(33);
        }

        public void SwitchToWorld(int worldIndex)
        {
            if (Provider is LiveDataProvider ldp)
                ldp.SetWorld(worldIndex);
            InvalidateEntityCache();
            Archetypes = Provider.GetArchetypes();
            Queries = Provider.GetQueries();
            Resources = Provider.GetResources();
            SystemCount = Provider.SystemCount;
            SelectedEntityId = Entities.Count > 0 ? Entities[0].Id : (int?)null;
            if (Archetypes.Count > 0) SelectedArchetypeId = Archetypes[0].Id;
            if (Queries.Count > 0) SelectedQueryId = Queries[0].Id;
            if (Resources.Count > 0) SelectedResourceName = Resources[0].Name;
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
            if (CurrentTab == tab) return;
            CurrentTab = tab;
            _lastEntityCount = -1;
            _lastComponentHash = -1;
            TabBar.Refresh(_tabBar, this);
            RefreshLeftPanel();
            RefreshInspector();
        }

        public void TogglePause()
        {
            Paused = !Paused;
            TopPanel.Update(_topPanel, this);
            Footer.Update(_footer, this);
        }

        public void SelectEntity(int id)
        {
            SelectedEntityId = id;
            _lastComponentHash = -1;
            if (CurrentTab == TabKey.Entities)
                RefreshLeftPanel();
            RefreshInspector();
        }

        public void SelectEntityFromArchetype(int id)
        {
            SelectedEntityId = id;
            _lastComponentHash = -1;
            CurrentTab = TabKey.Entities;
            TabBar.Refresh(_tabBar, this);
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
            var newEnt = Provider.CreateEntity();
            Entities = Provider.GetEntities();
            Archetypes = Provider.GetArchetypes();
            Queries = Provider.GetQueries();
            SelectedEntityId = newEnt.Id;
            CurrentTab = TabKey.Entities;
            TabBar.Refresh(_tabBar, this);
            RefreshLeftPanel();
            RefreshInspector();
            TopPanel.Update(_topPanel, this);
        }

        public void DestroyEntity(int id)
        {
            Provider.DestroyEntity(id);
            Entities = Provider.GetEntities();
            Archetypes = Provider.GetArchetypes();
            Queries = Provider.GetQueries();
            if (SelectedEntityId == id)
                SelectedEntityId = Entities.Count > 0 ? Entities[0].Id : (int?)null;
            RefreshLeftPanel();
            RefreshInspector();
            TopPanel.Update(_topPanel, this);
        }

        public void RemoveComponent(int entityId, string compName)
        {
            Provider.RemoveComponent(entityId, compName);
            Entities = Provider.GetEntities();
            Archetypes = Provider.GetArchetypes();
            Queries = Provider.GetQueries();
            RefreshInspector();
        }

        public void AddComponent(int entityId, string compName)
        {
            Provider.AddComponent(entityId, compName);
            Entities = Provider.GetEntities();
            Archetypes = Provider.GetArchetypes();
            Queries = Provider.GetQueries();
            RefreshInspector();
        }

        public void SetFieldValue(int entityId, string compName, string fieldKey, FieldValue value)
        {
            Provider.SetFieldValue(entityId, compName, fieldKey, value);
            Changes[$"{entityId}:{compName}:{fieldKey}"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            RefreshInspector();
        }

        private int GetSelectedEntityComponentHash()
        {
            if (SelectedEntityId == null) return 0;
            var entity = Entities.FirstOrDefault(e => e.Id == SelectedEntityId.Value);
            if (entity == null) return 0;
            int hash = SelectedEntityId.Value * 31;
            foreach (var c in entity.Components)
                hash ^= c.Name.GetHashCode();
            return hash;
        }

        private void InvalidateEntityCache()
        {
            _lastEntityCount = -1;
            _lastComponentHash = -1;
        }

        private void RefreshLeftPanel()
        {
            var oldScroll = _leftPanel.Query<ScrollView>().First();
            var savedOffset = oldScroll != null ? oldScroll.scrollOffset : Vector2.zero;

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

            var newScroll = _leftPanel.Query<ScrollView>().First();
            if (newScroll != null) newScroll.scrollOffset = savedOffset;
        }

        private void RefreshInspector()
        {
            if (_inspectorPanel == null) return;
            var oldScroll = _inspectorPanel.Query<ScrollView>().First();
            var savedOffset = oldScroll != null ? oldScroll.scrollOffset : Vector2.zero;

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

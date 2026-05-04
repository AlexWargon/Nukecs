#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor
{
    public class NukecsDashboardWindow : EditorWindow
    {
        private World _world;
        private int _selectedWorldId;
        private int? _selectedEntityId;
        private int _selectedArchetypeIndex = -1;
        private string _selectedGroup = "All";
        private bool _paused;
        private int _lastEntityCount = -1;
        private int _lastArchetypeCount = -1;

        private VisualElement _topBar;
        private VisualElement _leftSidebar;
        private VisualElement _centerPanel;
        private VisualElement _rightPanel;
        private VisualElement _bottomBar;
        private ScrollView _archetypeContainer;
        private ScrollView _entityTableContainer;
        private ScrollView _inspectorContainer;
        private VisualElement _archetypeSection;
        private VisualElement _entitySection;

        private Label _entityCountBadge;
        private Label _systemCountBadge;
        private Label _liveIndicator;

        private readonly Dictionary<int, ComponentProxy> _componentProxies = new();
        private readonly Dictionary<string, bool> _foldoutStates = new();
        private int _selectedEntityArchetypeId;

        public World World => _world;
        public int SelectedWorldId => _selectedWorldId;
        public int? SelectedEntityId => _selectedEntityId;
        /// <summary>
        /// Selected Index (0,1,2,3....N), Not HashID (-68371923151)
        /// </summary>
        public int SelectedArchetypeIndex => _selectedArchetypeIndex;
        public string SelectedGroup => _selectedGroup;
        public bool Paused => _paused;
        public Dictionary<int, ComponentProxy> ComponentProxies => _componentProxies;
        public Dictionary<string, bool> FoldoutStates => _foldoutStates;

        [MenuItem("Nuke.cs/ECS Dashboard")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<NukecsDashboardWindow>();
            wnd.titleContent = new GUIContent("ECS Dashboard");
            wnd.minSize = new Vector2(1100, 700);
        }

        public void CreateGUI()
        {
            _selectedWorldId = EditorPrefs.GetInt("NukecsDashboard.WorldId", 0);
            _world = World.Get(_selectedWorldId);

            var root = rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Column;
            root.style.backgroundColor = DashboardTheme.BgDark;

            var topGlowBar = DashboardStyles.CreateGradientLine(3);
            topGlowBar.style.flexShrink = 0;
            root.Add(topGlowBar);

            _topBar = DashboardTopBar.Create(this);
            root.Add(_topBar);

            var mainArea = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 1,
                    overflow = Overflow.Hidden
                }
            };
            root.Add(mainArea);

            _leftSidebar = DashboardLeftSidebar.Create(this);
            mainArea.Add(_leftSidebar);

            var separator1 = DashboardStyles.NeonSeparator(DashboardTheme.AccentPurple.WithAlpha(0.2f));
            separator1.style.width = 1;
            mainArea.Add(separator1);

            _centerPanel = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    flexDirection = FlexDirection.Column,
                    backgroundColor = DashboardTheme.BgDark
                }
            };
            mainArea.Add(_centerPanel);

            _archetypeSection = new VisualElement
            {
                style =
                {
                    minHeight = 150,
                    maxHeight = 200,
                    paddingBottom = 4
                }
            };
            var archTitle = DashboardStyles.SectionTitle("ARCHETYPES", DashboardTheme.TextPrimary);
            _archetypeSection.Add(archTitle);

            _archetypeContainer = new ScrollView(ScrollViewMode.Horizontal)
            {
                style =
                {
                    flexGrow = 1,
                    paddingLeft = 6,
                    paddingRight = 6
                }
            };
            _archetypeSection.Add(_archetypeContainer);
            _centerPanel.Add(_archetypeSection);

            var archSep = DashboardStyles.NeonSeparator(DashboardTheme.AccentPurple.WithAlpha(0.3f));
            _centerPanel.Add(archSep);

            _entitySection = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    flexDirection = FlexDirection.Column
                }
            };
            var entityTitle = DashboardStyles.SectionTitle("ENTITIES", DashboardTheme.TextPrimary);
            _entitySection.Add(entityTitle);
            _entityTableContainer = DashboardEntityTable.Create(this);
            _entitySection.Add(_entityTableContainer);
            _centerPanel.Add(_entitySection);

            var separator2 = DashboardStyles.NeonSeparator(DashboardTheme.AccentPurple.WithAlpha(0.2f));
            separator2.style.width = 1;
            mainArea.Add(separator2);

            _rightPanel = new VisualElement
            {
                style =
                {
                    width = 320,
                    minWidth = 280,
                    flexDirection = FlexDirection.Column,
                    backgroundColor = DashboardTheme.BgPanel
                }
            };

            var inspectorHeader = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    paddingTop = 8,
                    paddingBottom = 8,
                    paddingLeft = 12,
                    paddingRight = 12,
                    borderBottomWidth = 1,
                    borderBottomColor = DashboardTheme.Separator,
                    alignItems = Align.Center
                }
            };

            var cyanDot = DashboardStyles.GlowDot(DashboardTheme.AccentCyan, 8);
            cyanDot.style.marginRight = 8;
            inspectorHeader.Add(cyanDot);

            var inspectorTitle = new Label("INSPECTOR")
            {
                style =
                {
                    fontSize = DashboardTheme.FontSize.TitleMedium,
                    color = DashboardTheme.TextPrimary,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    letterSpacing = 2
                }
            };
            inspectorHeader.Add(inspectorTitle);
            _rightPanel.Add(inspectorHeader);

            _inspectorContainer = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
                {
                    flexGrow = 1,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 8
                }
            };
            _rightPanel.Add(_inspectorContainer);
            mainArea.Add(_rightPanel);

            _bottomBar = DashboardBottomPanel.Create(this);
            root.Add(_bottomBar);

            RefreshAll();

            root.schedule.Execute(() =>
            {
                if (!_paused && EditorApplication.isPlaying)
                {
                    _world = World.Get(_selectedWorldId);
                    if (!_world.IsAlive) return;
                    unsafe
                    {
                        var ec = _world.UnsafeWorld->entitiesAmount;
                        var ac = _world.UnsafeWorld->archetypesList.Length;
                        if (ec != _lastEntityCount || ac != _lastArchetypeCount)
                        {
                            _lastEntityCount = ec;
                            _lastArchetypeCount = ac;
                            RefreshAll();
                        }
                    }
                    DashboardTopBar.Update(_topBar, this);
                }
            }).Every(250);

            root.schedule.Execute(() =>
            {
                if (!EditorApplication.isPlaying)
                {
                    _inspectorContainer.Clear();
                    _selectedEntityId = null;
                    RefreshAll();
                    return;
                }

                _world = World.Get(_selectedWorldId);
                if (!_world.IsAlive) return;

                if (_selectedEntityId.HasValue)
                {
                    var e = _world.GetEntity(_selectedEntityId.Value);
                    if (e == Entity.Null)
                    {
                        _selectedEntityId = null;
                        _inspectorContainer.Clear();
                    }
                    else
                    {
                        DashboardEntityInspector.UpdateInspector(_inspectorContainer, this);
                    }
                }
            }).Every(33);
        }

        public void RefreshAll()
        {
            _world = World.Get(_selectedWorldId);
            if (!_world.IsAlive) return;

            DashboardArchetypePanel.Refresh(_archetypeContainer, this);
            DashboardEntityTable.Refresh(_entityTableContainer, this);
            DashboardLeftSidebar.Refresh(_leftSidebar, this);
            DashboardBottomPanel.Refresh(_bottomBar, this);
        }

        public void SelectEntity(int? entityId)
        {
            _selectedEntityId = entityId;
            _inspectorContainer.Clear();
            _componentProxies.Clear();
            if (entityId.HasValue)
            {
                DashboardEntityInspector.DrawInspector(_inspectorContainer, this);
            }
        }

        public void SelectArchetype(int archetypeId)
        {
            _selectedArchetypeIndex = _selectedArchetypeIndex == archetypeId ? -1 : archetypeId;
            DashboardEntityTable.Refresh(_entityTableContainer, this);
            DashboardArchetypePanel.Refresh(_archetypeContainer, this);
        }

        public void SelectGroup(string group)
        {
            _selectedGroup = _selectedGroup == group ? "All" : group;
            _selectedArchetypeIndex = -1;
            DashboardEntityTable.Refresh(_entityTableContainer, this);
            DashboardLeftSidebar.Refresh(_leftSidebar, this);
        }

        public void TogglePause()
        {
            _paused = !_paused;
        }

        public void SetWorld(int worldId)
        {
            _selectedWorldId = worldId;
            EditorPrefs.SetInt("NukecsDashboard.WorldId", worldId);
            _selectedEntityId = null;
            _selectedArchetypeIndex = -1;
            _selectedGroup = "All";
            _lastEntityCount = -1;
            _lastArchetypeCount = -1;
            _componentProxies.Clear();
            CreateGUI();
        }

        public ComponentProxy GetOrCreateProxy(int typeIndex)
        {
            if (_componentProxies.TryGetValue(typeIndex, out var proxy))
                return proxy;

            var type = ComponentTypeMap.GetType(typeIndex);
            var drawer = ComponentDrawerGenerator.GetDrawer(type);
            proxy = new ComponentProxy
            {
                drawer = drawer,
                typeIndex = typeIndex,
                entity = -1
            };
            proxy.imgui = new IMGUIContainer(() => ComponentInspector(proxy));
            _componentProxies[typeIndex] = proxy;
            return proxy;
        }

        private void ComponentInspector(ComponentProxy proxy)
        {
            if (proxy.boxedComponent != null && proxy.drawer != null)
            {
                EditorGUI.BeginChangeCheck();
                proxy.boxedComponent = (IComponent)proxy.drawer.Invoke(proxy.boxedComponent);
                if (EditorGUI.EndChangeCheck())
                {
                    if (proxy.entity >= 0 && ECSDebugWindowUI.CanWriteToWorld)
                    {
                        var arch = _world.UnsafeWorldRef.GetEntityArchetypePtr(proxy.entity);
                        arch.Ref.SetObject(proxy.entity, proxy.typeIndex, proxy.boxedComponent);
                    }
                }
            }
        }

        public bool GetFoldoutState(string key)
        {
            if (_foldoutStates.TryGetValue(key, out var state)) return state;
            _foldoutStates[key] = true;
            return true;
        }

        public static Label CreateBadge(string text, Color bgColor, Color textColor, float fontSize = 10)
        {
            return DashboardStyles.PillBadge(text, bgColor, textColor, fontSize);
        }

        public static VisualElement CreateGlowCard(Color borderColor, float radius = 12)
        {
            var card = new VisualElement
            {
                style =
                {
                    backgroundColor = DashboardTheme.BgCard,
                    borderTopLeftRadius = radius,
                    borderTopRightRadius = radius,
                    borderBottomLeftRadius = radius,
                    borderBottomRightRadius = radius,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = borderColor,
                    borderBottomColor = borderColor,
                    borderLeftColor = borderColor,
                    borderRightColor = borderColor,
                    overflow = Overflow.Hidden,
                    position = Position.Relative
                }
            };

            var shine = DashboardStyles.ShineLine();
            card.Add(shine);

            return card;
        }
    }
}
#endif

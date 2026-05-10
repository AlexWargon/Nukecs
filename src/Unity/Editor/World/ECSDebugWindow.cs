using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

#pragma warning disable CS0618 // Type or member is obsolete
#if UNITY_EDITOR && NUKECS_DEBUG
namespace Wargon.Nukecs.Editor
{
    public unsafe class ECSDebugWindowUI : EditorWindow
    {
        private const int ENTITY_NULL = -1;
        public static bool CanWriteToWorld = true;
        private static ECSDebugWindowUI _instance;
        private readonly List<DebugListItem> _items = new();
        private readonly Dictionary<int, string> _queryNames = new();
        private readonly Dictionary<string, bool> foldoutStates = new();
        private readonly HashSet<int> _contextMenuItems = new();
        private Tab _activeTab = Tab.Entities;
        private bool _archetypeChanged;

        private readonly Dictionary<int, ComponentProxy> _componentProxies = new();
        private Label _inspectorTitle;
        private Label _inspectorSubtitle;
        private VisualElement _inspectorHeaderBar;

        private ScrollView _inspectorView;
        private int _lastEntitiesCount = ENTITY_NULL;
        private int? _lastEntityId;
        private string _lastSearchValue = "";
        private ListView _listView;

        private ToolbarSearchField _searchField;
        private int _selectedEntityArchetypeId;
        private int? _selectedEntityId;
        private World _world;
        private Button _tabEntitiesBtn;
        private Button _tabArchetypesBtn;
        private Button _tabQueriesBtn;

        private int _selectedWorldId;
        private ToolbarMenu _themeMenuBtn;
        private static IECSTheme Theme => ECSThemeRegistry.Current;

        internal static ECSDebugWindowUI Instance
        {
            get
            {
                if (_instance == null)
                    _instance = GetWindow<ECSDebugWindowUI>();
                return _instance;
            }
        }

        public void CreateGUI()
        {
            _selectedWorldId = EditorPrefs.GetInt("ECSDebugger.WorldId", 0);
            var savedTheme = EditorPrefs.GetString("ECSDebugger.Theme", "Default Dark");
            ECSThemeRegistry.SetTheme(savedTheme);

            _world = World.Get(_selectedWorldId);
            if (!_world.IsAlive) return;
            var root = rootVisualElement;
            root.Clear();
            root.style.flexDirection = FlexDirection.Row;
            root.style.backgroundColor = Theme.BgPanelLeft;

            var leftPanel = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    flexDirection = FlexDirection.Column,
                    minWidth = 260,
                    maxWidth = 420,
                    backgroundColor = Theme.BgPanelLeft
                }
            };

            // World Selector
            var worldSelectorRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginTop = 4,
                    marginLeft = 6,
                    marginRight = 6,
                    marginBottom = 2,
                    alignItems = Align.Center
                }
            };
            var worldLabel = new Label("World:")
            {
                style =
                {
                    fontSize = 11,
                    color = Theme.TextSecondary,
                    marginRight = 4,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };
            worldSelectorRow.Add(worldLabel);
            Button worldDropdown = null;
            worldDropdown = new Button(() =>
            {
                var gm = new GenericMenu();
                for (var i = 0; i < 1; i++)
                {
                    var w = World.Get(i);
                    if (!w.IsAlive) continue;
                    var id = i;
                    var label = $"World {id}" + (w.EntitiesAmount > 0 ? $" ({w.EntitiesAmount} entities)" : "");
                    gm.AddItem(new GUIContent(label), id == _selectedWorldId, () =>
                    {
                        _selectedWorldId = id;
                        EditorPrefs.SetInt("ECSDebugger.WorldId", id);
                        worldDropdown.text = $"World {id}";
                        _selectedEntityId = null;
                        _lastEntityId = null;
                        RefreshList();
                        _inspectorView.Clear();
                        _inspectorTitle.text = "Inspector";
                        _inspectorSubtitle.text = "";
                    });
                }
                gm.ShowAsContext();
            })
            {
                text = $"World {_selectedWorldId}",
                style =
                {
                    flexGrow = 1,
                    fontSize = 11,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    backgroundColor = Theme.BgCard,
                    color = Theme.TextPrimary,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 2,
                    paddingBottom = 2,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };
            worldSelectorRow.Add(worldDropdown);
            leftPanel.Add(worldSelectorRow);

            // Segmented Tab Bar
            var tabBarContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginTop = 2,
                    marginLeft = 6,
                    marginRight = 6,
                    marginBottom = 4,
                    backgroundColor = Theme.BgTabInactive,
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8,
                    borderBottomRightRadius = 8,
                    paddingTop = 2,
                    paddingBottom = 2,
                    paddingLeft = 2,
                    paddingRight = 2
                }
            };

            _tabEntitiesBtn = CreateTabButton("Entities", true, () => SwitchTab(Tab.Entities));
            _tabArchetypesBtn = CreateTabButton("Archetypes", false, () => SwitchTab(Tab.Archetypes));
            _tabQueriesBtn = CreateTabButton("Queries", false, () => SwitchTab(Tab.Queries));
            tabBarContainer.Add(_tabEntitiesBtn);
            tabBarContainer.Add(_tabArchetypesBtn);
            tabBarContainer.Add(_tabQueriesBtn);
            leftPanel.Add(tabBarContainer);

            // Search
            var searchWrapper = new VisualElement
            {
                style =
                {
                    marginLeft = 6,
                    marginRight = 6,
                    marginTop = 2,
                    marginBottom = 4,
                    backgroundColor = Theme.BgCard,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    paddingLeft = 4,
                    paddingRight = 4,
                    paddingTop = 2,
                    paddingBottom = 2
                }
            };
            _searchField = new ToolbarSearchField
            {
                style = { flexGrow = 1 }
            };
            _searchField.RegisterValueChangedCallback(_ => RefreshList());
            searchWrapper.Add(_searchField);
            leftPanel.Add(searchWrapper);

            // ListView
            _listView = new ListView(_items, 24, MakeListItem, BindListItem)
            {
                selectionType = SelectionType.Single,
                style =
                {
                    flexGrow = 1,
                    backgroundColor = Theme.BgPanelLeft,
                    borderTopWidth = 1,
                    borderTopColor = Theme.Border
                }
            };
            _listView.onSelectionChange += OnItemSelected;

            leftPanel.Add(_listView);

            // Create Entity Button
            var createEntityBtn = new Button(() =>
            {
                _world = World.Get(_selectedWorldId);
                if (!_world.IsAlive) return;
                var e = _world.Entity();
                RefreshList();
                SwitchTab(Tab.Entities);
                SelectEntityById(e.id);
            })
            {
                text = "+ Create Entity",
                style =
                {
                    marginTop = 4,
                    marginLeft = 6,
                    marginRight = 6,
                    marginBottom = 6,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    backgroundColor = Theme.Accent,
                    color = Theme.TextWhite,
                    fontSize = 12,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    paddingTop = 5,
                    paddingBottom = 5
                }
            };
            leftPanel.Add(createEntityBtn);

            root.Add(leftPanel);

            // SEPARATOR
            var separator = new VisualElement
            {
                style =
                {
                    width = 1,
                    backgroundColor = Theme.Separator
                }
            };
            root.Add(separator);

            // RIGHT PANEL
            var rightPanel = new VisualElement
            {
                style =
                {
                    flexGrow = 2,
                    flexDirection = FlexDirection.Column,
                    backgroundColor = Theme.BgPanelRight
                }
            };

            _inspectorHeaderBar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    backgroundColor = Theme.BgHeader,
                    paddingTop = 10,
                    paddingBottom = 10,
                    paddingLeft = 12,
                    paddingRight = 12,
                    borderBottomWidth = 1,
                    borderBottomColor = Theme.Border,
                    alignItems = Align.Center
                }
            };

            var headerTextCol = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    flexGrow = 1
                }
            };

            _inspectorTitle = new Label("Inspector")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 15,
                    color = Theme.TextPrimary,
                    marginBottom = 2
                }
            };
            _inspectorSubtitle = new Label("")
            {
                style =
                {
                    fontSize = 11,
                    color = Theme.TextSecondary
                }
            };
            headerTextCol.Add(_inspectorTitle);
            headerTextCol.Add(_inspectorSubtitle);
            _inspectorHeaderBar.Add(headerTextCol);

            // Theme Selector
            _themeMenuBtn = new ToolbarMenu
            {
                text = savedTheme,
                style =
                {
                    fontSize = 10,
                    width = 120
                }
            };
            _inspectorHeaderBar.Add(_themeMenuBtn);

            rightPanel.Add(_inspectorHeaderBar);

            _inspectorView = new ScrollView
            {
                style =
                {
                    flexGrow = 1,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 8
                }
            };
            rightPanel.Add(_inspectorView);

            root.Add(rightPanel);

            PopulateThemeMenu(_themeMenuBtn);

            RefreshList();
            root.schedule.Execute(() =>
            {
                _world = World.Get(_selectedWorldId);
                if (!_world.IsAlive || !EditorApplication.isPlaying)
                    return;

                if (_lastEntitiesCount != _world.UnsafeWorld->entitiesAmount || _lastSearchValue != _searchField.value)
                {
                    _lastEntitiesCount = _world.UnsafeWorld->entitiesAmount;
                    _lastSearchValue = _searchField.value;
                    RefreshList();
                }

                _tabEntitiesBtn.text = $"[{_world.EntitiesAmount}] Entities";
                ApplyTabStyle(_tabEntitiesBtn, _activeTab == Tab.Entities);
            }).Every(100);

            root.schedule.Execute(() =>
            {
                _world = World.Get(_selectedWorldId);
                if (!_world.IsAlive || !EditorApplication.isPlaying)
                {
                    RefreshList();
                    _inspectorView.Clear();
                    _selectedEntityId = null;
                }

                if (_selectedEntityId.HasValue)
                {
                    _archetypeChanged = NeedRepaintEntityInspector();
                    if (_archetypeChanged)
                    {
                        DrawEntityInspector(_selectedEntityId.Value);
                        _archetypeChanged = false;
                    }
                    else
                    {
                        UpdateProxies(_selectedEntityId.Value);
                    }
                }
            }).Every(33);
        }

        private Button CreateTabButton(string text, bool active, Action onClick)
        {
            var btn = new Button(onClick)
            {
                text = text,
                style =
                {
                    flexGrow = 1,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    paddingTop = 4,
                    paddingBottom = 4,
                    paddingLeft = 8,
                    paddingRight = 8,
                    fontSize = 12,
                    color = active ? Theme.TextWhite : Theme.TextSecondary,
                    backgroundColor = active ? Theme.BgTabActive : Color.clear
                }
            };
            return btn;
        }

        private void ApplyTabStyle(Button btn, bool active)
        {
            if (active)
            {
                btn.style.backgroundColor = Theme.BgTabActive;
                btn.style.color = Theme.TextWhite;
                btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            else
            {
                btn.style.backgroundColor = Color.clear;
                btn.style.color = Theme.TextSecondary;
                btn.style.unityFontStyleAndWeight = FontStyle.Normal;
            }
        }

        private void PopulateThemeMenu(ToolbarMenu menu)
        {
            foreach (var name in ECSThemeRegistry.ThemeNames)
            {
                var n = name;
                menu.menu.AppendAction(n, _ =>
                {
                    ECSThemeRegistry.SetTheme(n);
                    EditorPrefs.SetString("ECSDebugger.Theme", n);
                    menu.text = n;
                    CreateGUI();
                });
            }
        }

        private void SelectEntityById(int entityId)
        {
            var sel = _items.FirstOrDefault(x => x.id == entityId);
            var idx = _items.IndexOf(sel);
            if (idx >= 0)
            {
                _listView.SetSelection(idx);
                _listView.ScrollToItem(idx);
            }
        }

        private void UpdateEntityHeaderInfo(int entityId)
        {
            var e = _world.GetEntity(entityId);
            if (e == Entity.Null)
            {
                _inspectorTitle.text = "Inspector";
                _inspectorSubtitle.text = "";
                return;
            }

            var eName = e.Has<Name>() ? e.Get<Name>().value.Value : "";
            _inspectorTitle.text = string.IsNullOrEmpty(eName) ? $"Entity {entityId}" : eName;

            ref var arch = ref _world.UnsafeWorldRef.GetEntityArchetypePtr(entityId).Ref;
            var compCount = 0;
            foreach (var _ in arch.types) compCount++;
            _inspectorSubtitle.text = $"ID: {entityId}   Archetype: {arch.hashId}   Components: {compCount}";
        }

        private void DrawArchetypeInspector(int archetypeId)
        {
            _inspectorView.Clear();
            _inspectorTitle.text = $"Archetype {archetypeId}";

            ref var arch = ref _world.UnsafeWorld->archetypesMap[archetypeId].ptr.Ref;
            _inspectorSubtitle.text = $"Entities: {arch.count}";

            var sectionLabel = new Label("Component Types")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    color = Theme.TextPrimary,
                    marginTop = 4,
                    marginBottom = 4
                }
            };
            _inspectorView.Add(sectionLabel);

            foreach (var typeIndex in arch.types)
            {
                var t = ComponentTypeMap.GetType(typeIndex);
                var typeName = t != null ? t.Name : $"Type {typeIndex}";
                var accentColor = Theme.AccentForType(typeName);

                var chip = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        backgroundColor = Theme.BgCard,
                        borderTopLeftRadius = 6,
                        borderTopRightRadius = 6,
                        borderBottomLeftRadius = 6,
                        borderBottomRightRadius = 6,
                        paddingLeft = 8,
                        paddingRight = 8,
                        paddingTop = 4,
                        paddingBottom = 4,
                        marginBottom = 3,
                        overflow = Overflow.Hidden,
                        position = Position.Relative
                    }
                };

                var accentBar = new VisualElement
                {
                    style =
                    {
                        position = Position.Absolute,
                        left = 0, top = 0, bottom = 0,
                        width = 3,
                        backgroundColor = accentColor,
                        borderTopLeftRadius = 6,
                        borderBottomLeftRadius = 6
                    }
                };
                chip.Add(accentBar);

                chip.Add(new Label(typeName)
                {
                    style =
                    {
                        fontSize = 11,
                        color = Theme.TextPrimary,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        paddingLeft = 4
                    }
                });
                _inspectorView.Add(chip);
            }

            var entitiesLabel = new Label($"Entities ({arch.count})")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    color = Theme.TextPrimary,
                    marginTop = 12,
                    marginBottom = 4
                }
            };
            _inspectorView.Add(entitiesLabel);

            var entityCount = arch.count;
            for (var i = 0; i < entityCount; i++)
            {
                var entityId = arch.packedEntities.Ptr[i];
                var e = _world.GetEntity(entityId);
                var eName = (e.IsValid() && e.Has<Name>()) ? e.Get<Name>().value.Value : $"Entity";

                var entityRow = new Button(() =>
                {
                    SwitchTab(Tab.Entities);
                    SelectEntityById(entityId);
                })
                {
                    text = $"[{entityId}] {eName}",
                    style =
                    {
                        borderTopLeftRadius = 4,
                        borderTopRightRadius = 4,
                        borderBottomLeftRadius = 4,
                        borderBottomRightRadius = 4,
                        borderLeftWidth = 0,
                        borderRightWidth = 0,
                        borderTopWidth = 0,
                        borderBottomWidth = 0,
                        backgroundColor = Theme.BgCard,
                        color = Theme.AccentGreen,
                        fontSize = 11,
                        paddingLeft = 8,
                        paddingTop = 2,
                        paddingBottom = 2,
                        marginBottom = 2,
                        alignItems = Align.FlexStart,
                        unityTextAlign = TextAnchor.MiddleLeft
                    }
                };
                _inspectorView.Add(entityRow);
            }
        }

        private void DrawQueryInspector(int queryId)
        {
            _inspectorView.Clear();
            _inspectorTitle.text = $"Query {queryId}";

            if (queryId < 0 || queryId >= _world.UnsafeWorld->queries.Length)
            {
                _inspectorSubtitle.text = "Invalid";
                return;
            }

            ref var q = ref _world.UnsafeWorld->queries.Ptr[queryId].Ref;
            _inspectorSubtitle.text = $"Entities: {q.count}";

            // With components
            var withLabel = new Label("With Components")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    color = Theme.TextPrimary,
                    marginTop = 4,
                    marginBottom = 4
                }
            };
            _inspectorView.Add(withLabel);

            foreach (var typeIdx in ComponentTypeMap.TypesIndexes)
            {
                if (!q.with.IsCreated || !q.with.Has(typeIdx)) continue;
                var t = ComponentTypeMap.GetType(typeIdx);
                var typeName = t != null ? t.Name : $"Type {typeIdx}";
                AddComponentChip(typeName, Theme.Accent);
            }

            // None components
            var noneLabel = new Label("None (Excluded)")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    color = Theme.TextPrimary,
                    marginTop = 12,
                    marginBottom = 4
                }
            };
            _inspectorView.Add(noneLabel);

            foreach (var typeIdx in ComponentTypeMap.TypesIndexes)
            {
                if (!q.none.IsCreated || !q.none.Has(typeIdx)) continue;
                var t = ComponentTypeMap.GetType(typeIdx);
                var typeName = t != null ? t.Name : $"Type {typeIdx}";
                AddComponentChip(typeName, Theme.RemoveBtn);
            }

            // Matching Archetypes
            var matchLabel = new Label($"Matching Archetypes ({q.matchingArchetypes.length})")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    color = Theme.TextPrimary,
                    marginTop = 12,
                    marginBottom = 4
                }
            };
            _inspectorView.Add(matchLabel);

            for (var i = 0; i < q.matchingArchetypes.length; i++)
            {
                var archIdx = q.matchingArchetypes.Ptr[i];
                if (archIdx < 0 || archIdx >= _world.UnsafeWorld->archetypesList.Length) continue;
                ref var arch = ref _world.UnsafeWorld->archetypesList.Ptr[archIdx].Ref;

                var archRow = new Button(() =>
                {
                    SwitchTab(Tab.Archetypes);
                    SelectItemById(archIdx);
                })
                {
                    text = $"Archetype {archIdx}  ({arch.count} entities)",
                    style =
                    {
                        borderTopLeftRadius = 4,
                        borderTopRightRadius = 4,
                        borderBottomLeftRadius = 4,
                        borderBottomRightRadius = 4,
                        borderLeftWidth = 0,
                        borderRightWidth = 0,
                        borderTopWidth = 0,
                        borderBottomWidth = 0,
                        backgroundColor = Theme.BgCard,
                        color = Theme.Accent,
                        fontSize = 11,
                        paddingLeft = 8,
                        paddingTop = 2,
                        paddingBottom = 2,
                        marginBottom = 2,
                        alignItems = Align.FlexStart,
                        unityTextAlign = TextAnchor.MiddleLeft
                    }
                };
                _inspectorView.Add(archRow);
            }
        }

        private void AddComponentChip(string typeName, Color accentColor)
        {
            var chip = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    backgroundColor = Theme.BgCard,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 4,
                    paddingBottom = 4,
                    marginBottom = 3,
                    overflow = Overflow.Hidden,
                    position = Position.Relative
                }
            };

            var accentBar = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = 0, top = 0, bottom = 0,
                    width = 3,
                    backgroundColor = accentColor,
                    borderTopLeftRadius = 6,
                    borderBottomLeftRadius = 6
                }
            };
            chip.Add(accentBar);
            chip.Add(new Label(typeName)
            {
                style =
                {
                    fontSize = 11,
                    color = Theme.TextPrimary,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    paddingLeft = 4
                }
            });
            _inspectorView.Add(chip);
        }

        private void SelectItemById(int id)
        {
            var sel = _items.FirstOrDefault(x => x.id == id);
            var idx = _items.IndexOf(sel);
            if (idx >= 0)
            {
                _listView.SetSelection(idx);
                _listView.ScrollToItem(idx);
            }
        }

        private bool GetFoldoutState(string key)
        {
            if (foldoutStates.TryGetValue(key, out var state)) return state;
            foldoutStates[key] = true;
            return true;
        }

        [MenuItem("Nuke.cs/ECS Debug")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<ECSDebugWindowUI>();
            wnd.titleContent = new GUIContent("ECS Debugger");
            _instance = wnd;
            wnd.minSize = new Vector2(800, 500);
        }

        private VisualElement MakeListItem()
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 2,
                    paddingBottom = 2,
                    height = 24
                }
            };

            var badge = new VisualElement
            {
                style =
                {
                    backgroundColor = Theme.AccentGreen,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    paddingTop = 1,
                    paddingBottom = 1,
                    paddingLeft = 5,
                    paddingRight = 5,
                    marginRight = 6
                }
            };
            var badgeLabel = new Label
            {
                style =
                {
                    fontSize = 10,
                    color = Theme.TextWhite,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            badge.Add(badgeLabel);
            row.Add(badge);

            var label = new Label
            {
                style =
                {
                    flexGrow = 1,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    fontSize = 12,
                    color = Theme.TextPrimary
                }
            };
            row.Add(label);

            var prefabBadge = new Label("P")
            {
                style =
                {
                    fontSize = 9,
                    color = Theme.TextPrefab,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginLeft = 4,
                    display = DisplayStyle.None
                }
            };
            row.Add(prefabBadge);

            row.userData = new ListItemData(badge, badgeLabel, label, prefabBadge);

            row.RegisterCallback<MouseEnterEvent>(OnRowHover);
            row.RegisterCallback<MouseLeaveEvent>(OnRowLeave);

            return row;
        }

        private static void OnRowHover(MouseEnterEvent e) => ((VisualElement)e.target).style.backgroundColor = Theme.RowHoverBg;
        private static void OnRowLeave(MouseLeaveEvent e) => ((VisualElement)e.target).style.backgroundColor = Color.clear;

        private void BindListItem(VisualElement element, int index)
        {
            if (index >= _items.Count) return;
            var data = (ListItemData)element.userData;
            var badge = data.badge;
            var badgeLabel = data.badgeLabel;
            var label = data.label;
            var prefabBadge = data.prefabBadge;
            var item = _items[index];

            badge.style.display = item.type == DebugListItem.ItemType.Entity ? DisplayStyle.Flex : DisplayStyle.None;
            if (item.type == DebugListItem.ItemType.Entity)
                badgeLabel.text = item.id.ToString();

            label.text = item.displayName;
            label.style.color = Theme.TextPrimary;

            if (item.isPrefab)
            {
                prefabBadge.style.display = DisplayStyle.Flex;
                label.style.color = Theme.TextPrefab;
            }
            else
            {
                prefabBadge.style.display = DisplayStyle.None;
            }

            if (item.type != DebugListItem.ItemType.Entity)
            {
                badge.style.display = DisplayStyle.Flex;
                badge.style.backgroundColor = Color.clear;
                badgeLabel.text = "";
            }
            else
            {
                badge.style.backgroundColor = Theme.AccentGreen;
            }

            if (item.type == DebugListItem.ItemType.Entity && _contextMenuItems.Add(item.id))
                ShowContextMenuEntity(element, World.Get(_selectedWorldId).GetEntity(item.id));
        }

        private static void ShowContextMenuEntity(VisualElement targetElement, Entity e)
        {
            var menu = new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Copy", action => { e.Copy(); });
                evt.menu.AppendAction("Destroy", action => { e.Destroy(); });
            });
            targetElement.AddManipulator(menu);
        }

        private Texture2D GetIconForTab(Tab tab)
        {
            return tab switch
            {
                Tab.Entities => EditorGUIUtility.IconContent("greenLight").image as Texture2D,
                Tab.Archetypes => EditorGUIUtility.IconContent("Prefab Icon").image as Texture2D,
                Tab.Queries => EditorGUIUtility.IconContent("Search Icon").image as Texture2D,
                _ => null
            };
        }

        private void SwitchTab(Tab tab)
        {
            _activeTab = tab;
            _searchField.value = "";
            _selectedEntityId = null;
            _inspectorView.Clear();
            _inspectorTitle.text = "Inspector";
            _inspectorSubtitle.text = "";

            ApplyTabStyle(_tabEntitiesBtn, tab == Tab.Entities);
            ApplyTabStyle(_tabArchetypesBtn, tab == Tab.Archetypes);
            ApplyTabStyle(_tabQueriesBtn, tab == Tab.Queries);

            RefreshList();
        }

        private void RefreshList()
        {
            _world = World.Get(_selectedWorldId);
            if (!_world.IsAlive || !EditorApplication.isPlaying)
            {
                _items.Clear();
                _listView.Rebuild();
                return;
            }

            _items.Clear();
            _contextMenuItems.Clear();

            var search = _searchField.value?.ToLower();

            switch (_activeTab)
            {
                case Tab.Entities:
                    var entities = _world.UnsafeWorld->entitiesDens.GetAliveEntities();
                    for (var i = 0; i < entities.Length; i++)
                    {
                        var eId = entities[i];
                        var e = _world.GetEntity(eId);
                        if (!e.IsValid()) continue;

                        string displayName;
                        if (e.Has<Name>())
                            displayName = e.Get<Name>().value.Value;
                        else
                            displayName = $"Entity";
                        if (!string.IsNullOrEmpty(search) && !displayName.ToLower().Contains(search) && !eId.ToString().Contains(search)) continue;
                        _items.Add(new DebugListItem(DebugListItem.ItemType.Entity, e.id, displayName,
                            e.Has<IsPrefab>()));
                    }

                    break;

                case Tab.Archetypes:
                    for (var i = 0; i < _world.UnsafeWorld->archetypesList.Length; i++)
                    {
                        var a = _world.UnsafeWorld->archetypesList.ElementAt(i).Ref;
                        var displayName = $"Archetype {a.hashId}";
                        if (!string.IsNullOrEmpty(search) && !displayName.ToLower().Contains(search)) continue;
                        _items.Add(new DebugListItem(DebugListItem.ItemType.Archetype, a.hashId, displayName));
                    }

                    break;

                case Tab.Queries:
                    for (var i = 0; i < _world.UnsafeWorld->queries.Length; i++)
                    {
                        var q = _world.UnsafeWorld->queries.ElementAt(i).Ref;
                        if (!_queryNames.ContainsKey(q.Id))
                            _queryNames[q.Id] = $"Query {q.Id} ({q.count} entities)";
                        var queryName = _queryNames[q.Id];
                        if (!string.IsNullOrEmpty(search) && !queryName.ToLower().Contains(search)) continue;
                        _items.Add(new DebugListItem(DebugListItem.ItemType.Query, q.Id, queryName));
                    }

                    break;
            }

            _listView.Rebuild();
        }

        private void OnItemSelected(IEnumerable<object> selection)
        {
            _inspectorView.Clear();

            var sel = selection.FirstOrDefault() as DebugListItem;
            if (sel == null)
            {
                _inspectorTitle.text = "Inspector";
                _inspectorSubtitle.text = "";
                _selectedEntityId = null;
                return;
            }

            switch (sel.type)
            {
                case DebugListItem.ItemType.Entity:
                    _selectedEntityId = sel.id;
                    UpdateEntityHeaderInfo(sel.id);
                    DrawEntityInspector(sel.id);
                    break;

                case DebugListItem.ItemType.Archetype:
                    _selectedEntityId = null;
                    DrawArchetypeInspector(sel.id);
                    break;

                case DebugListItem.ItemType.Query:
                    _selectedEntityId = null;
                    DrawQueryInspector(sel.id);
                    break;
            }
        }

        private bool NeedRepaintEntityInspector()
        {
            _world = World.Get(_selectedWorldId);
            ref var arch = ref _world.UnsafeWorldRef.GetEntityArchetypePtr(_selectedEntityId.Value).Ref;
            var archChanged = arch.hashId != _selectedEntityArchetypeId;
            _selectedEntityArchetypeId = arch.hashId;
            return archChanged;
        }

        private VisualElement CreateComponentCard(string typeName, Color accentColor, out Foldout foldout)
        {
            var card = new VisualElement
            {
                style =
                {
                    marginBottom = 6,
                    backgroundColor = Theme.BgCard,
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8,
                    borderBottomRightRadius = 8,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = Theme.Border,
                    borderBottomColor = Theme.Border,
                    borderLeftColor = Theme.Border,
                    borderRightColor = Theme.Border,
                    position = Position.Relative,
                    overflow = Overflow.Hidden
                }
            };

            var accentBar = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = 0,
                    top = 0,
                    bottom = 0,
                    width = 5,
                    backgroundColor = accentColor,
                    borderTopLeftRadius = 8,
                    borderBottomLeftRadius = 8
                }
            };
            card.Add(accentBar);

            foldout = new Foldout
            {
                text = typeName,
                value = GetFoldoutState(typeName),
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    color = Theme.TextPrimary,
                    paddingLeft = 8,
                    paddingTop = 2,
                    paddingBottom = 2
                }
            };

            foldout.RegisterValueChangedCallback(evt => foldoutStates[typeName] = evt.newValue);

            card.Add(foldout);
            return card;
        }

        private Button CreateRemoveButton(Action onClick)
        {
            var btn = new Button(onClick)
            {
                text = "\u2715",
                style =
                {
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    backgroundColor = Color.clear,
                    color = Theme.TextSecondary,
                    paddingTop = 1,
                    paddingBottom = 1,
                    paddingLeft = 4,
                    paddingRight = 4,
                    fontSize = 11,
                    width = 20,
                    height = 20
                }
            };

            btn.RegisterCallback<MouseEnterEvent>(_ =>
            {
                btn.style.backgroundColor = Theme.RemoveBtnHoverBg;
                btn.style.color = Theme.RemoveBtn;
            });
            btn.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                btn.style.backgroundColor = Color.clear;
                btn.style.color = Theme.TextSecondary;
            });

            return btn;
        }

        private void DrawEntityInspector(int entityId)
        {
            var realE = _world.GetEntity(entityId);

            if (realE == Entity.Null)
            {
                _selectedEntityId = null;
                _lastEntityId = ENTITY_NULL;
                _inspectorTitle.text = "Inspector";
                _inspectorSubtitle.text = "";
                _inspectorView.Clear();
                _listView.ClearSelection();
                return;
            }

            if (_lastEntityId == entityId && !_archetypeChanged)
            {
                UpdateProxies(entityId);
                return;
            }

            _lastEntityId = entityId;

            _inspectorView.Clear();
            ref var arch = ref _world.UnsafeWorldRef.GetEntityArchetypePtr(entityId).Ref;
            var existingTypes = new HashSet<int>();
            foreach (var ti in arch.types) existingTypes.Add(ti);

            foreach (var typeIndex in arch.types)
            {
                var boxedComponent = arch.GetObject(entityId, typeIndex);
                if (boxedComponent == null)
                    continue;

                if (TryDrawComponentArrayBox(boxedComponent))
                    continue;

                var nm = boxedComponent.GetType().Name;
                var accentColor = Theme.AccentForType(nm);

                var card = CreateComponentCard(nm, accentColor, out var foldout);

                var proxy = GetOrCreateProxy(typeIndex);
                proxy.entity = entityId;
                proxy.boxedComponent = boxedComponent;

                var removeBtn = CreateRemoveButton(() =>
                {
                    _world.GetEntity(entityId).RemoveIndex(typeIndex);
                    DrawEntityInspector(entityId);
                });

                var header = foldout.Q<Toggle>();
                header.style.flexDirection = FlexDirection.Row;
                var headerLabel = header.Q<Label>();
                headerLabel.style.flexGrow = 1;
                headerLabel.style.color = Theme.TextPrimary;
                header.Add(removeBtn);

                var imguiContainer = new VisualElement
                {
                    style = { paddingLeft = 8, paddingRight = 4 }
                };
                imguiContainer.Add(proxy.imgui);
                foldout.Add(imguiContainer);

                _inspectorView.Add(card);
            }

            UpdateEntityHeaderInfo(entityId);

            var addCompBtn = new Button(() =>
            {
                var menu = new GenericMenu();
                foreach (var typeIdx in ComponentTypeMap.TypesIndexes)
                {
                    var t = ComponentTypeMap.GetType(typeIdx);
                    if (t == null) continue;
                    if (existingTypes.Contains(typeIdx)) continue;
                    var idx = typeIdx;
                    menu.AddItem(new GUIContent(t.Name), false, () =>
                    {
                        _world.GetEntity(entityId).worldPointer->ECB.Add(entityId, idx);
                    });
                }
                menu.ShowAsContext();
            })
            {
                text = "+ Add Component",
                style =
                {
                    marginTop = 8,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    backgroundColor = Theme.BgCard,
                    color = Theme.TextSecondary,
                    fontSize = 11,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    paddingTop = 6,
                    paddingBottom = 6
                }
            };
            addCompBtn.RegisterCallback<MouseEnterEvent>(_ =>
            {
                addCompBtn.style.backgroundColor = Theme.BgCardHover;
                addCompBtn.style.color = Theme.Accent;
            });
            addCompBtn.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                addCompBtn.style.backgroundColor = Theme.BgCard;
                addCompBtn.style.color = Theme.TextSecondary;
            });
            _inspectorView.Add(addCompBtn);
        }

        private void UpdateProxies(int entityId, bool forceUpdate = false)
        {
            ref var arch = ref _world.UnsafeWorldRef.GetEntityArchetypePtr(entityId).Ref;

            foreach (var typeIndex in arch.types)
            {
                var boxedComponentFromWorld = arch.GetObject(entityId, typeIndex);
                if (boxedComponentFromWorld != null && _componentProxies.TryGetValue(typeIndex, out var proxy))
                {
                    if (!EditorGUIUtility.editingTextField && !forceUpdate)
                        proxy.boxedComponent = arch.GetObject(entityId, typeIndex);

                    proxy.typeIndex = typeIndex;
                    proxy.entity = entityId;
                    proxy.imgui.MarkDirtyRepaint();
                }
            }
        }

        internal void SelectEntityFromField(Entity entity)
        {
            if (!entity.IsValid()) return;

            ref var arch = ref _world.UnsafeWorldRef.GetEntityArchetypePtr(entity.id).Ref;
            foreach (var typeIndex in arch.types)
            {
                var boxedComponent = arch.GetObject(entity.id, typeIndex);
                if (boxedComponent == null) continue;

                var proxy = GetOrCreateProxy(typeIndex);

                proxy.boxedComponent = boxedComponent;
                proxy.entity = entity.id;
                proxy.typeIndex = typeIndex;
            }

            var sel = _items.FirstOrDefault(x => x.id == entity.id);
            var idx = _items.IndexOf(sel);
            if (idx >= 0)
            {
                _listView.SetSelection(idx);
                _listView.ScrollToItem(idx);
            }

            DrawEntityInspector(entity.id);
        }

        private bool TryDrawComponentArrayBox(object boxedComponent)
        {
            var type = boxedComponent.GetType();
            var typeData = type_db.get_type_data(type);
            if (!typeData.is_generic || typeData.generic_type_definition != typeof(ComponentArray<>))
                return false;

            var elemType = typeData.generic_argument00;
            var readAt =
                (Func<object, int, object>)boxedComponent.GetMethodDelegate<int>(type,
                    nameof(ComponentArray<Child>.ReadAt), elemType.val);
            var length = (int)boxedComponent.GetPropertyValue(type, nameof(ComponentArray<Child>.Length));

            var headerText = $"ComponentArray({elemType.name}) [{length}]";
            var card = CreateComponentCard(headerText, Theme.AccentBarArray, out var foldout);

            if (length > 0)
            {
                var container = new VisualElement
                {
                    style =
                    {
                        paddingLeft = 8,
                        paddingRight = 4,
                        paddingTop = 2,
                        paddingBottom = 2
                    }
                };

                var imgui = new IMGUIContainer(() =>
                {
                    EditorGUI.indentLevel++;
                    using (new EditorGUI.DisabledScope(true))
                    {
                        type = boxedComponent.GetType();
                        length = (int)boxedComponent.GetPropertyValue(type, nameof(ComponentArray<Child>.Length));
                        for (var i = 0; i < length; i++)
                        {
                            var elem = readAt(boxedComponent, i);
                            ComponentDrawerProxyEditor.DrawField($"[{i}]", elemType, elem);
                        }
                    }

                    EditorGUI.indentLevel--;
                });
                container.Add(imgui);
                foldout.Add(container);
            }

            _inspectorView.Add(card);
            return true;
        }

        private ComponentProxy GetOrCreateProxy(int typeIndex)
        {
            if (_componentProxies.TryGetValue(typeIndex, out var proxy))
                return proxy;

            var type = ComponentTypeMap.GetType(typeIndex);
            var drawer = ComponentDrawerGenerator.GetDrawer(type);
            proxy = new ComponentProxy
            {
                drawer = drawer,
                typeIndex = typeIndex,
                entity = ENTITY_NULL
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
                    if (proxy.entity != ENTITY_NULL && CanWriteToWorld)
                    {
                        var e = _world.GetEntity(proxy.entity);
                        e.SetObject(proxy.boxedComponent);
                    }
            }

            CanWriteToWorld = true;
        }

        private enum Tab
        {
            Entities,
            Archetypes,
            Queries
        }

        public class DebugListItem
        {
            public enum ItemType
            {
                Entity,
                Archetype,
                Query
            }

            public readonly string displayName;
            public readonly int id;

            public readonly ItemType type;
            public bool isPrefab;

            public DebugListItem(ItemType type, int id, string displayName, bool isPrefab = false)
            {
                this.type = type;
                this.id = id;
                this.displayName = displayName;
                this.isPrefab = isPrefab;
            }

            public override string ToString()
            {
                return displayName;
            }
        }
    }

    public class ListItemData
    {
        public VisualElement badge;
        public Label badgeLabel;
        public Label label;
        public Label prefabBadge;

        public ListItemData(VisualElement badge, Label badgeLabel, Label label, Label prefabBadge)
        {
            this.badge = badge;
            this.badgeLabel = badgeLabel;
            this.label = label;
            this.prefabBadge = prefabBadge;
        }

        public static explicit operator ListItemData((VisualElement, Label, Label, Label) tuple) =>
            new ListItemData(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
    }

    public class ComponentProxy
    {
        public IComponent boxedComponent;
        public Func<object, object> drawer;
        public int entity;
        public IMGUIContainer imgui;
        public int typeIndex;
    }
}
#endif

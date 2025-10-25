#pragma warning disable CS0618 // Type or member is obsolete
#if UNITY_EDITOR && NUKECS_DEBUG
namespace Wargon.Nukecs.Editor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;
    
    public unsafe class ECSDebugWindowUI : EditorWindow
    {
        public static bool CanWriteToWorld = true;
        private const int BORDER_RADIUS = 6;
        private static ECSDebugWindowUI _instance;

        internal static ECSDebugWindowUI Instance
        {
            get
            {
                if (_instance == null)
                    _instance = GetWindow<ECSDebugWindowUI>();
                return _instance;
            }
        }

        private byte worldId = 0;
        private World _world;

        private ToolbarSearchField _searchField;
        private ListView _listView;

        private ScrollView _inspectorView;
        private Label _inspectorTitle;

        private enum Tab { Entities, Archetypes, Queries }
        private Tab _activeTab = Tab.Entities;
        private ToolbarButton entitiesBtn;
        private readonly List<DebugListItem> _items = new();
        private readonly Dictionary<int, string> _queryNames = new();
        // private readonly Dictionary<int, ComponentDrawerProxy> _proxyCache = new();
        // private readonly Dictionary<int, UnityEditor.Editor> _editorCache = new();
        private readonly Dictionary<string, bool> foldoutStates = new();
        private bool GetFoldoutState(string key)
        {
            if (foldoutStates.TryGetValue(key, out var state))
            {
                return state;
            }
            foldoutStates[key] = true;
            return true;
        }
        private int? _lastEntityId;
        private const int ENTITY_NULL = -1;
        private int _lastEntitiesCount = ENTITY_NULL;
        private string _lastSearchValue = "";
        private int? _selectedEntityId = null;
        private int _selectedEntityArchetypeId;
        private bool _archetypeChanged = false;
        
        [MenuItem("Nuke.cs/ECS Debugger")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<ECSDebugWindowUI>();
            wnd.titleContent = new GUIContent("ECS Debugger");
            _instance = wnd;
            wnd.minSize = new Vector2(800, 500);
        }

        public class DebugListItem
        {
            public enum ItemType { Entity, Archetype, Query }

            public readonly ItemType type;
            public readonly int id;
            public readonly string displayName;
            public bool isPrefab;
            public DebugListItem(ItemType type, int id, string displayName, bool isPrefab = false)
            {
                this.type = type;
                this.id = id;
                this.displayName = displayName;
                this.isPrefab = isPrefab;
            }

            public override string ToString() => displayName;
        }

        public void CreateGUI()
        {
            _world = World.Get(worldId);
            
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Row;

            // LEFT PANEL
            var leftPanel = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    flexDirection = FlexDirection.Column,
                    minWidth = 250,
                    maxWidth = 400
                }
            };

            // Tabs
            var tabs = new Toolbar();
            entitiesBtn = new ToolbarButton(() => SwitchTab(Tab.Entities)) { text = "Entities" };
            var archetypesBtn = new ToolbarButton(() => SwitchTab(Tab.Archetypes)) { text = "Archetypes" };
            var queriesBtn = new ToolbarButton(() => SwitchTab(Tab.Queries)) { text = "Queries" };
            tabs.Add(entitiesBtn);
            tabs.Add(archetypesBtn);
            tabs.Add(queriesBtn);
            leftPanel.Add(tabs);

            // Search
            _searchField = new ToolbarSearchField();
            _searchField.RegisterValueChangedCallback(_ => RefreshList());
            leftPanel.Add(_searchField);

            // ListView for DebugListItem
            _listView = new ListView(_items, 20, MakeListItem, BindListItem)
            {
                selectionType = SelectionType.Single,
                style = { flexGrow = 1 }
            };
            _listView.onSelectionChange += OnItemSelected;
            
            leftPanel.Add(_listView);

            root.Add(leftPanel);

            // RIGHT PANEL
            var rightPanel = new VisualElement
            {
                style =
                {
                    flexGrow = 2,
                    flexDirection = FlexDirection.Column,
                    backgroundColor = new Color(0.14f, 0.14f, 0.14f),
                    paddingLeft = 5,
                    paddingTop = 5
                }
            };

            _inspectorTitle = new Label("Inspector")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 14,
                    paddingBottom = 6
                }
            };
            rightPanel.Add(_inspectorTitle);

            _inspectorView = new ScrollView { style = { flexGrow = 1 } };
            rightPanel.Add(_inspectorView);

            root.Add(rightPanel);

            RefreshList();

            root.schedule.Execute(() =>
            {
                _world = World.Get(worldId);
                if (!_world.IsAlive || !EditorApplication.isPlaying)
                    return;

                if (_lastEntitiesCount != _world.UnsafeWorld->entitiesAmount || _lastSearchValue != _searchField.value)
                {
                    _lastEntitiesCount = _world.UnsafeWorld->entitiesAmount;
                    _lastSearchValue = _searchField.value;
                    RefreshList();
                }


            }).Every(100);

            root.schedule.Execute(() =>
            {
                _world = World.Get(worldId);
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
                        //_inspectorView.MarkDirtyRepaint();
                    }

                }

                entitiesBtn.text = $"[{_world.EntitiesAmount}]Entities";
            }).Every(33);
        }

        private VisualElement MakeListItem()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            var icon = new Image
            {
                style =
                {
                    width = 16,
                    height = 16,
                    marginRight = 4
                }
            };
            row.Add(icon);

            var label = new Label
            {
                style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleLeft }
            };
            row.Add(label);

            row.userData = (icon, label);
            return row;
        }

        private void BindListItem(VisualElement element, int index)
        {
            if (index >= _items.Count) return;
            var (icon, label) = ((Image, Label))element.userData;
            var item = _items[index];
            label.text = item.displayName;
            if(item.isPrefab)
                label.style.color = new Color(0f, 0.56f, 0.78f);
            
            icon.image = GetIconForTab(item.type switch
            {
                DebugListItem.ItemType.Entity => Tab.Entities,
                DebugListItem.ItemType.Archetype => Tab.Archetypes,
                DebugListItem.ItemType.Query => Tab.Queries,
                _ => Tab.Entities
            });
            
            element.RegisterCallback<MouseDownEvent>(evn => {
                switch (item.type) {
                    case DebugListItem.ItemType.Entity: {
                        if (evn.button == 1) {
                            ShowContextMenuEntity(element, World.Get(worldId).GetEntity(item.id));
                            evn.StopPropagation();
                        }
                        break;
                    }
                    case DebugListItem.ItemType.Archetype:
                        break;
                    case DebugListItem.ItemType.Query:
                        break;
                }
            });
        }
        
        private static void ShowContextMenuEntity(VisualElement targetElement, Entity e)
        {
            var menu = new ContextualMenuManipulator(evt =>
            {
                evt.menu.AppendAction("Copy", action =>
                {
                    e.Copy();
                });
                
                evt.menu.AppendAction("Destroy", action =>
                {
                    e.Destroy();
                });

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
            RefreshList();
        }

        private void RefreshList()
        {
            if (!_world.IsAlive || !EditorApplication.isPlaying)
            {
                _items.Clear();
                _listView.Rebuild();
                return;
            }

            _items.Clear();

            string search = _searchField.value?.ToLower();

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
                        {
                            displayName = $"(e:{e.id}) {e.Get<Name>().value.Value}";
                        }
                        else
                        {
                            displayName = $"(e:{e.id})";
                        }
                        if (!string.IsNullOrEmpty(search) && !displayName.ToLower().Contains(search)) continue;
                        _items.Add(new DebugListItem(DebugListItem.ItemType.Entity, e.id, displayName, e.Has<IsPrefab>()));
                            
                    }
                    break;

                case Tab.Archetypes:
                    for (int i = 0; i < _world.UnsafeWorld->archetypesList.Length; i++)
                    {
                        var a = _world.UnsafeWorld->archetypesList.ElementAt(i).Ref;
                        var displayName = $"Archetype {a.id}";
                        if (!string.IsNullOrEmpty(search) && !displayName.ToLower().Contains(search)) continue;
                        _items.Add(new DebugListItem(DebugListItem.ItemType.Archetype, a.id, displayName));
                    }
                    break;

                case Tab.Queries:
                    for (int i = 0; i < _world.UnsafeWorld->queries.Length; i++)
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
                _selectedEntityId = null;
                return;
            }

            _inspectorTitle.text = $"{sel.displayName}";

            switch (sel.type)
            {
                case DebugListItem.ItemType.Entity:
                    _selectedEntityId = sel.id;
                    DrawEntityInspector(sel.id);
                    //UpdateProxies(sel.id);
                    break;

                case DebugListItem.ItemType.Archetype:
                    _inspectorView.Add(new Label("Archetype inspector not implemented yet"));
                    _selectedEntityId = null;
                    break;

                case DebugListItem.ItemType.Query:
                    _inspectorView.Add(new Label("Query inspector not implemented yet"));
                    _selectedEntityId = null;
                    break;
            }
        }

        // private ComponentDrawerProxy GetOrCreateProxy(int typeIndex, IComponent boxedComponent)
        // {
        //     if (!_proxyCache.TryGetValue(typeIndex, out var proxy) || proxy == null)
        //     {
        //         proxy = CreateInstance<ComponentDrawerProxy>();
        //         proxy.hideFlags = HideFlags.HideAndDontSave;
        //         _proxyCache[typeIndex] = proxy;
        //     }
        //
        //     proxy.boxedComponent = boxedComponent;
        //     return proxy;
        // }
        
        // private Editor GetOrCreateEditor(ComponentDrawerProxy proxy, int typeIndex)
        // {
        //     if (!_editorCache.TryGetValue(typeIndex, out var editor) || editor == null)
        //     {
        //         editor = Editor.CreateEditor(proxy);
        //         _editorCache[typeIndex] = editor;
        //     }
        //     return editor;
        // }

        private bool NeedRepaintEntityInspector()
        {
            ref var arch = ref _world.UnsafeWorldRef.entitiesArchetypes.ElementAt(_selectedEntityId.Value).ptr.Ref;
            var archChanged = arch.id != _selectedEntityArchetypeId;
            _selectedEntityArchetypeId = arch.id;
            return archChanged;
        }
        
        private void DrawEntityInspector(int entityId)
        {
            var realE = _world.GetEntity(entityId);
            
            
            if (realE == Entity.Null)
            {
                _selectedEntityId = null;
                _lastEntityId = ENTITY_NULL;
                _inspectorTitle.text = "Inspector";
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
            //dbug.log("redraw all inspector", Color.red);
            
            _inspectorView.Clear();
            ref var arch = ref _world.UnsafeWorldRef.entitiesArchetypes.ElementAt(entityId).ptr.Ref;

            foreach (var typeIndex in arch.types)
            {
                var boxedComponent = _world.UnsafeWorldRef.GetUntypedPool(typeIndex).GetObject(entityId);
                if (boxedComponent == null)
                    continue;
                
                if(TryDrawComponentArrayBox(boxedComponent))
                    continue;

                var componentContainer = new VisualElement
                {
                    style =
                    {
                        marginBottom = 4,
                        borderTopWidth = 1,
                        borderBottomWidth = 1,
                        borderLeftWidth = 1,
                        borderRightWidth = 1,
                        borderTopColor = new Color(0.25f, 0.25f, 0.25f),
                        borderBottomColor = new Color(0.25f, 0.25f, 0.25f),
                        borderLeftColor = new Color(0.25f, 0.25f, 0.25f),
                        borderRightColor = new Color(0.25f, 0.25f, 0.25f),
                        backgroundColor = new Color(0.17f, 0.22f, 0.18f),
                        borderTopLeftRadius = BORDER_RADIUS,
                        borderTopRightRadius = BORDER_RADIUS,
                        borderBottomLeftRadius = BORDER_RADIUS,
                        borderBottomRightRadius = BORDER_RADIUS
                    }
                };
                
                var nm = boxedComponent.GetType().Name;
                
                var foldout = new Foldout
                {
                    text = nm,
                    value = GetFoldoutState(nm),
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Bold,
                        fontSize = 12
                    }
                };
                
                foldout.RegisterValueChangedCallback(evt => foldoutStates[nm] = evt.newValue);
                
                var proxy = GetOrCreateProxy(typeIndex);
                proxy.entity = entityId;
                proxy.boxedComponent = boxedComponent;
                
                var btn = new Button(() =>
                {
                    _world.GetEntity(entityId).RemoveIndex(typeIndex);
                    DrawEntityInspector(entityId);
                })
                {
                    text = "  \u2715",
                    style = {                         
                        borderTopLeftRadius = BORDER_RADIUS,
                        borderTopRightRadius = BORDER_RADIUS,
                        borderBottomLeftRadius = BORDER_RADIUS,
                        borderBottomRightRadius = BORDER_RADIUS 
                    }
                };
                
                var header = foldout.Q<Toggle>();
                header.style.flexDirection = FlexDirection.Row;
                header.Q<Label>().style.flexGrow = 1;
                
                header.Add(btn);
                foldout.Add(proxy.imgui);
                componentContainer.Add(foldout);
                _inspectorView.Add(componentContainer);
            }
        }

        private void UpdateProxies(int entityId, bool forceUpdate = false)
        {
            ref var arch = ref _world.UnsafeWorldRef.entitiesArchetypes.ElementAt(entityId).ptr.Ref;
            
            foreach (var typeIndex in arch.types)
            {
                ref var pool = ref _world.UnsafeWorldRef.GetUntypedPool(typeIndex);
                var boxedComponentFromWorld = pool.GetObject(entityId);
                if (boxedComponentFromWorld != null && _componentProxies.TryGetValue(typeIndex, out var proxy))
                {
                    if (!EditorGUIUtility.editingTextField && !forceUpdate)
                    {
                        proxy.boxedComponent = pool.GetObject(entityId);
                    }

                    proxy.typeIndex = typeIndex;
                    proxy.entity = entityId;
                    proxy.imgui.MarkDirtyRepaint();
                }
            }
            
        }
        internal void SelectEntityFromField(Entity entity)
        {
            if (!entity.IsValid()) return;
            
            ref var arch = ref _world.UnsafeWorldRef.entitiesArchetypes.ElementAt(entity.id).ptr.Ref;
            foreach (var typeIndex in arch.types)
            {
                ref var pool = ref _world.UnsafeWorldRef.GetUntypedPool(typeIndex);
                var boxedComponent = pool.GetObject(entity.id);
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
                _inspectorTitle.text = $"{sel.displayName}";
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
            var readAt = (Func<object, int, object>)boxedComponent.GetMethodDelegate<int>(type, nameof(ComponentArray<Child>.ReadAt), elemType.val); // public ref T ElementAt(int index)
            var length = (int)boxedComponent.GetPropertyValue(type, nameof(ComponentArray<Child>.Length));

            var componentContainer = new VisualElement
            {
                style =
                {
                    marginBottom = 4,
                    borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                    borderTopColor = new Color(0.25f, 0.25f, 0.25f),
                    borderBottomColor = new Color(0.25f, 0.25f, 0.25f),
                    borderLeftColor = new Color(0.25f, 0.25f, 0.25f),
                    borderRightColor = new Color(0.25f, 0.25f, 0.25f),
                    backgroundColor = new Color(0.22f, 0.22f, 0.22f),
                    borderTopLeftRadius = BORDER_RADIUS,
                    borderTopRightRadius = BORDER_RADIUS,
                    borderBottomLeftRadius = BORDER_RADIUS,
                    borderBottomRightRadius = BORDER_RADIUS
                }
            };
            
            var foldout = new Foldout
            {
                text = $"ComponentArray({elemType.name}) [{length}]",
                value = true,
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 12 }
            };
            if (length > 0)
            {
                var container = new VisualElement
                {
                    style =
                    {
                        marginBottom = 4,
                        borderTopWidth = 2,
                        borderBottomWidth = 2,
                        borderLeftWidth = 2,
                        borderRightWidth = 2,
                        borderTopColor = new Color(0.18f, 0.18f, 0.18f),
                        borderBottomColor = new Color(0.18f, 0.18f, 0.18f),
                        borderLeftColor = new Color(0.18f, 0.18f, 0.18f),
                        borderRightColor = new Color(0.18f, 0.18f, 0.18f),
                        backgroundColor = new Color(0.20f, 0.20f, 0.20f),
                        borderTopLeftRadius = BORDER_RADIUS,
                        borderTopRightRadius = BORDER_RADIUS,
                        borderBottomLeftRadius = BORDER_RADIUS,
                        borderBottomRightRadius = BORDER_RADIUS
                    }
                };

                //draw readonly list of elements
                var imgui = new IMGUIContainer(() =>
                {
                    // ElementAt via reflection returns ref, but we dont change it, so its ok.
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
            componentContainer.Add(foldout);
            _inspectorView.Add(componentContainer);
            return true;
        }

        private Dictionary<int, ComponentProxy> _componentProxies = new();

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
            if (proxy.boxedComponent != null)
            {
                EditorGUI.BeginChangeCheck();
                proxy.boxedComponent = (IComponent)proxy.drawer.Invoke(proxy.boxedComponent);
                if (EditorGUI.EndChangeCheck())
                {
                    if (proxy.entity != ENTITY_NULL && CanWriteToWorld)
                    {
                        _world.unsafeWorldPtr.Ref.GetUntypedPool(proxy.typeIndex).SetObject(proxy.entity, proxy.boxedComponent);
                    }
                }
            }
            ECSDebugWindowUI.CanWriteToWorld = true;
        }
    }
    public class ComponentProxy
    {
        public IComponent boxedComponent;
        public IMGUIContainer imgui;
        public Func<object, object> drawer;
        public int typeIndex;
        public int entity;
    }
}
#endif
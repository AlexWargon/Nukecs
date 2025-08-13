#if UNITY_EDITOR && NUKECS_DEBUG
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor
{
    public unsafe class ECSDebugWindowUI : EditorWindow
    {
        private World world;

        private ToolbarSearchField searchField;
        private ListView listView;

        private ScrollView inspectorView;
        private Label inspectorTitle;

        private enum Tab { Entities, Archetypes, Queries }
        private Tab activeTab = Tab.Entities;

        // Вместо List<string> — теперь список структурированных элементов
        private readonly List<DebugListItem> items = new();
        private readonly Dictionary<int, string> queryNames = new();
        private readonly Dictionary<int, ComponentDrawerProxy> proxyCache = new();
        private readonly Dictionary<int, UnityEditor.Editor> editorCache = new();
        private int? lastEntityId;
        private int lastEntitiesCount = -1;
        private string lastSearchValue = "";
        private int? selectedEntityId = null;
        private int selectedEntityArchetypeId;
        private bool archetypeChanged = false;
        
        [MenuItem("Nuke.cs/ECS Debugger")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<ECSDebugWindowUI>();
            wnd.titleContent = new GUIContent("ECS Debugger");
            wnd.minSize = new Vector2(800, 500);
        }

        // Класс элемента списка с типом и id
        public class DebugListItem
        {
            public enum ItemType { Entity, Archetype, Query }

            public ItemType Type;
            public int Id;
            public string DisplayName;

            public DebugListItem(ItemType type, int id, string displayName)
            {
                Type = type;
                Id = id;
                DisplayName = displayName;
            }

            public override string ToString() => DisplayName;
        }

        public void CreateGUI()
        {
            world = World.Get(0);

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
            var entitiesBtn = new ToolbarButton(() => SwitchTab(Tab.Entities)) { text = "Entities" };
            var archetypesBtn = new ToolbarButton(() => SwitchTab(Tab.Archetypes)) { text = "Archetypes" };
            var queriesBtn = new ToolbarButton(() => SwitchTab(Tab.Queries)) { text = "Queries" };
            tabs.Add(entitiesBtn);
            tabs.Add(archetypesBtn);
            tabs.Add(queriesBtn);
            leftPanel.Add(tabs);

            // Search
            searchField = new ToolbarSearchField();
            searchField.RegisterValueChangedCallback(_ => RefreshList());
            leftPanel.Add(searchField);

            // ListView for DebugListItem
            listView = new ListView(items, 20, MakeListItem, BindListItem)
            {
                selectionType = SelectionType.Single,
                style = { flexGrow = 1 }
            };
            listView.onSelectionChange += OnItemSelected;
            leftPanel.Add(listView);

            root.Add(leftPanel);

            // RIGHT PANEL
            var rightPanel = new VisualElement
            {
                style =
                {
                    flexGrow = 2,
                    flexDirection = FlexDirection.Column,
                    backgroundColor = new Color(0.2f, 0.2f, 0.2f),
                    paddingLeft = 5,
                    paddingTop = 5
                }
            };

            inspectorTitle = new Label("Inspector")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 14,
                    paddingBottom = 6
                }
            };
            rightPanel.Add(inspectorTitle);

            inspectorView = new ScrollView { style = { flexGrow = 1 } };
            rightPanel.Add(inspectorView);

            root.Add(rightPanel);

            RefreshList();

            root.schedule.Execute(() =>
            {
                if (!world.IsAlive || !EditorApplication.isPlaying)
                    return;

                if (lastEntitiesCount != world.UnsafeWorld->entitiesAmount || lastSearchValue != searchField.value)
                {
                    lastEntitiesCount = world.UnsafeWorld->entitiesAmount;
                    lastSearchValue = searchField.value;
                    RefreshList();
                }


            }).Every(100);

            root.schedule.Execute(() =>
            {
                if (!world.IsAlive || !EditorApplication.isPlaying)
                {
                    RefreshList();
                    inspectorView.Clear();
                    selectedEntityId = null;
                }
                if (selectedEntityId.HasValue)
                {
                    archetypeChanged = NeedRepaintEntityInspector();
                    if (archetypeChanged)
                    {
                        DrawEntityInspector(selectedEntityId.Value);
                        archetypeChanged = false;
                    }
                    else
                    {
                        UpdateProxies(selectedEntityId.Value);
                        inspectorView.MarkDirtyRepaint();
                    }

                }

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
            if (index >= items.Count) return;
            var (icon, label) = ((Image, Label))element.userData;
            var item = items[index];
            label.text = item.DisplayName;
            icon.image = GetIconForTab(item.Type switch
            {
                DebugListItem.ItemType.Entity => Tab.Entities,
                DebugListItem.ItemType.Archetype => Tab.Archetypes,
                DebugListItem.ItemType.Query => Tab.Queries,
                _ => Tab.Entities
            });
        }

        private Texture2D GetIconForTab(Tab tab)
        {
            return tab switch
            {
                Tab.Entities => EditorGUIUtility.IconContent("GameObject Icon").image as Texture2D,
                Tab.Archetypes => EditorGUIUtility.IconContent("Prefab Icon").image as Texture2D,
                Tab.Queries => EditorGUIUtility.IconContent("Search Icon").image as Texture2D,
                _ => null
            };
        }

        private void SwitchTab(Tab tab)
        {
            activeTab = tab;
            searchField.value = "";
            selectedEntityId = null;
            inspectorView.Clear();
            inspectorTitle.text = "Inspector";
            RefreshList();
        }

        private void RefreshList()
        {
            if (!world.IsAlive || !EditorApplication.isPlaying)
            {
                items.Clear();
                listView.Rebuild();
                return;
            }

            items.Clear();

            string search = searchField.value?.ToLower();

            switch (activeTab)
            {
                case Tab.Entities:
                    var entities = world.UnsafeWorld->entitiesDens.GetAliveEntities();
                    for (var i = 0; i < entities.Length; i++)
                    {
                        var eId = entities[i];
                        var e = world.GetEntity(eId);
                        if (!e.IsValid()) continue;
                        string name;
                        if (e.Has<Name>())
                        {
                            name = $"e:{e.id}|{e.Get<Name>().value.Value}";
                        }
                        else
                        {
                            name = $"e:{e.id}";
                        }
                        if (!string.IsNullOrEmpty(search) && !name.ToLower().Contains(search)) continue;
                        items.Add(new DebugListItem(DebugListItem.ItemType.Entity, e.id, name));
                    }
                    break;

                case Tab.Archetypes:
                    for (int i = 0; i < world.UnsafeWorld->archetypesList.Length; i++)
                    {
                        var a = world.UnsafeWorld->archetypesList.ElementAt(i).Ref;
                        var name = $"Archetype {a.id}";
                        if (!string.IsNullOrEmpty(search) && !name.ToLower().Contains(search)) continue;
                        items.Add(new DebugListItem(DebugListItem.ItemType.Archetype, a.id, name));
                    }
                    break;

                case Tab.Queries:
                    for (int i = 0; i < world.UnsafeWorld->queries.Length; i++)
                    {
                        var q = world.UnsafeWorld->queries.ElementAt(i).Ref;
                        if (!queryNames.ContainsKey(q.Id))
                            queryNames[q.Id] = $"Query {q.Id} ({q.count} entities)";
                        var name = queryNames[q.Id];
                        if (!string.IsNullOrEmpty(search) && !name.ToLower().Contains(search)) continue;
                        items.Add(new DebugListItem(DebugListItem.ItemType.Query, q.Id, name));
                    }
                    break;
            }

            listView.Rebuild();
        }

        private void OnItemSelected(IEnumerable<object> selection)
        {
            inspectorView.Clear();

            var sel = selection.FirstOrDefault() as DebugListItem;
            if (sel == null)
            {
                inspectorTitle.text = "Inspector";
                selectedEntityId = null;
                return;
            }

            inspectorTitle.text = $"{sel.Type}: {sel.DisplayName}";

            switch (sel.Type)
            {
                case DebugListItem.ItemType.Entity:
                    selectedEntityId = sel.Id;
                    DrawEntityInspector(sel.Id);
                    UpdateProxies(sel.Id); // <-- вызываем явно обновление прокси
                    break;

                case DebugListItem.ItemType.Archetype:
                    inspectorView.Add(new Label("Archetype inspector not implemented yet"));
                    selectedEntityId = null;
                    break;

                case DebugListItem.ItemType.Query:
                    inspectorView.Add(new Label("Query inspector not implemented yet"));
                    selectedEntityId = null;
                    break;
            }
        }

        private ComponentDrawerProxy GetOrCreateProxy(int typeIndex, object boxedComponent)
        {
            if (!proxyCache.TryGetValue(typeIndex, out var proxy) || proxy == null)
            {
                proxy = ScriptableObject.CreateInstance<ComponentDrawerProxy>();
                proxy.hideFlags = HideFlags.HideAndDontSave;
                proxyCache[typeIndex] = proxy;
            }

            proxy.boxedComponent = boxedComponent;
            return proxy;
        }
        
        private UnityEditor.Editor GetOrCreateEditor(ComponentDrawerProxy proxy, int typeIndex)
        {
            if (!editorCache.TryGetValue(typeIndex, out var editor) || editor == null)
            {
                editor = UnityEditor.Editor.CreateEditor(proxy);
                editorCache[typeIndex] = editor;
            }
            return editor;
        }

        private bool NeedRepaintEntityInspector()
        {
            ref var arch = ref world.UnsafeWorldRef.entitiesArchetypes.ElementAt(selectedEntityId.Value).ptr.Ref;
            var archChanged = arch.id != selectedEntityArchetypeId;
            selectedEntityArchetypeId = arch.id;
            return archChanged;
        }
        
        private void DrawEntityInspector(int entityId)
        {
            if (lastEntityId == entityId && !archetypeChanged)
            {
                UpdateProxies(entityId);
                return;
            }
            lastEntityId = entityId;
            inspectorView.Clear();
            ref var arch = ref world.UnsafeWorldRef.entitiesArchetypes.ElementAt(entityId).ptr.Ref;

            foreach (var typeIndex in arch.types)
            {
                var boxedComponent = world.UnsafeWorldRef.GetUntypedPool(typeIndex).GetObject(entityId);
                if (boxedComponent == null)
                    continue;

                var proxy = GetOrCreateProxy(typeIndex, boxedComponent);
                var editor = GetOrCreateEditor(proxy, typeIndex);

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
                        backgroundColor = new Color(0.22f, 0.22f, 0.22f)
                    }
                };

                var foldout = new Foldout
                {
                    text = boxedComponent.GetType().Name,
                    value = true,
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Bold,
                        fontSize = 12
                    }
                };

                var imgui = new IMGUIContainer(() =>
                {
                    EditorGUI.BeginChangeCheck();
                    editor.OnInspectorGUI();
                    if (EditorGUI.EndChangeCheck())
                    {
                        //sync ecs
                    }
                });

                foldout.Add(imgui);
                componentContainer.Add(foldout);
                inspectorView.Add(componentContainer);
            }
        }

        private void UpdateProxies(int entityId)
        {
            ref var arch = ref world.UnsafeWorldRef.entitiesArchetypes.ElementAt(entityId).ptr.Ref;
            
            foreach (var typeIndex in arch.types)
            {
                ref var pool = ref world.UnsafeWorldRef.GetUntypedPool(typeIndex);
                var boxedComponentFromWorld = pool.GetObject(entityId);
                if (boxedComponentFromWorld != null && proxyCache.TryGetValue(typeIndex, out var proxy))
                {
                    if (!EditorGUIUtility.editingTextField)
                    {
                        proxy.boxedComponent = pool.GetObject(entityId);
                    }

                    proxy.typeIndex = typeIndex;
                    proxy.entity = entityId;
                    proxy.world = world.UnsafeWorldRef.Id;
                    editorCache[typeIndex].Repaint();
                }
            }
        }
    }
}
#endif
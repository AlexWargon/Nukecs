#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor
{
    public static class DashboardEntityTable
    {
        private static string _searchFilter = "";
        private static string _prevSearchFilter = "__init__";
        private static string _sortColumn = "Entity";
        private static bool _sortAscending = true;
        private static string _prevSortColumn = "Entity";
        private static bool _prevSortAscending = true;
        private const int COMPONENT_MAX_NAME_LEN = 6;
        private const float DEFAULT_ENTITY = 100;
        private const float DEFAULT_NAME = 100;
        private const float DEFAULT_ARCH = 80;
        private const float DEFAULT_COMP = 130;
        private const float MIN_COL = 40;
        private const float ROW_PAD = 0;
        private const float RowHeight = 28f;
        private const int Overscan = 4;

        private static readonly Dictionary<string, float> ColWidths = new Dictionary<string, float>();
        private static VisualElement _listRef;
        private static ScrollView _scrollRef;
        private static NukecsDashboardWindow _windowRef;
        private static string _dragColId;
        private static float _dragStartX;
        private static float _dragStartW;

        private static readonly List<EntityRowData> CachedRows = new List<EntityRowData>();
        private static VisualElement _virtualContent;
        private static readonly List<VisualElement> RowPool = new List<VisualElement>();
        private static int _lastVisibleStart = -1;
        private static int _lastVisibleEnd = -1;
        private static int _lastRowCount = -1;
        private static bool _isArchetypeView;
        private static bool _isQueryView;
        private static int _lastArchetypeIndex = int.MinValue;
        private static int _lastQueryId = int.MinValue;

        private static readonly List<int> CompTypeIndices = new List<int>();
        private static readonly List<string> CompColIds = new List<string>();
        private static readonly List<bool> CompIsTag = new List<bool>();
        private static readonly List<Type> CompFirstFieldTypes = new List<Type>();
        private static readonly List<FieldInfo> CompFirstFieldInfos = new List<FieldInfo>();
        private static readonly List<Type> CompTypes = new List<Type>();

        private static float GetWidth(string id, float def)
        {
            if (!ColWidths.TryGetValue(id, out var w))
            {
                w = def;
                ColWidths[id] = w;
            }
            return w;
        }

        private static bool IsEditableType(Type t)
        {
            return t == typeof(int) || t == typeof(float) || t == typeof(bool) || t == typeof(string);
        }

        private static float ComputeContentWidth()
        {
            if (!_isArchetypeView) return 0;
            var w = GetWidth("Entity", DEFAULT_ENTITY) + GetWidth("Name", DEFAULT_NAME);
            for (var ci = 0; ci < CompColIds.Count; ci++)
                w += GetWidth(CompColIds[ci], DEFAULT_COMP);
            return w;
        }

        public static ScrollView Create(NukecsDashboardWindow window)
        {
            return new ScrollView(ScrollViewMode.VerticalAndHorizontal)
            {
                name = "entity-table",
                style = { flexGrow = 1 }
            };
        }

        public static void Refresh(ScrollView container, NukecsDashboardWindow window)
        {
            var world = window.World;
            if (!world.IsAlive) return;

            _scrollRef = container;
            _windowRef = window;
            _isArchetypeView = window.SelectedArchetypeIndex >= 0;
            _isQueryView = window.SelectedQueryId >= 0 && window.SelectedArchetypeIndex < 0;

            container.Clear();

            var toolbarRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingBottom = 4,
                    flexShrink = 0
                }
            };

            var searchField = DashboardStyles.CreateSearchField("Search entities...", filter =>
            {
                _searchFilter = filter;
            });

            var searchInput = searchField.Q<TextField>("search-input");
            if (searchInput != null)
                searchInput.SetValueWithoutNotify(_searchFilter);
            var placeholder = searchField.Q<Label>("search-placeholder");
            if (placeholder != null)
                placeholder.style.display = string.IsNullOrEmpty(_searchFilter)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

            toolbarRow.Add(searchField);

            var createBtn = new Button(() =>
            {
                var w = World.Get(window.SelectedWorldId);
                if (!w.IsAlive) return;
                var e = w.Entity();
                window.RefreshAll();
                window.SelectEntity(e.id);
            })
            {
                text = "+ Create",
                style =
                {
                    fontSize = DashboardTheme.FontSize.Small,
                    borderTopLeftRadius = 14,
                    borderTopRightRadius = 14,
                    borderBottomLeftRadius = 14,
                    borderBottomRightRadius = 14,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderTopColor = DashboardTheme.AccentPurple.WithAlpha(0.3f),
                    borderBottomColor = DashboardTheme.AccentPurple.WithAlpha(0.3f),
                    borderLeftColor = DashboardTheme.AccentPurple.WithAlpha(0.3f),
                    borderRightColor = DashboardTheme.AccentPurple.WithAlpha(0.3f),
                    backgroundColor = DashboardTheme.AccentPurple.WithAlpha(0.15f),
                    color = DashboardTheme.AccentPurple,
                    paddingTop = 4,
                    paddingBottom = 4,
                    paddingLeft = 12,
                    paddingRight = 12,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            toolbarRow.Add(createBtn);
            container.Add(toolbarRow);

            var listContainer = new VisualElement
            {
                name = "entity-list",
                style = { overflow = Overflow.Hidden }
            };

            if (_isArchetypeView)
            {
                var cw = ComputeContentWidth();
                listContainer.style.minWidth = cw;
                listContainer.style.flexShrink = 0;
            }
            else if (_isQueryView)
            {
                var cw = GetWidth("Entity", DEFAULT_ENTITY) + GetWidth("Name", DEFAULT_NAME + 30) + GetWidth("Archetype", DEFAULT_ARCH) + DEFAULT_COMP;
                listContainer.style.minWidth = cw;
                listContainer.style.flexShrink = 0;
            }

            container.Add(listContainer);

            listContainer.RegisterCallback<MouseMoveEvent>(OnListMouseMove);
            listContainer.RegisterCallback<MouseUpEvent>(OnListMouseUp);
            _listRef = listContainer;

            listContainer.Add(BuildHeaders(window, world, _isArchetypeView, container));

            CacheComponentColumns(window, world);

            _virtualContent = new VisualElement
            {
                name = "virtual-content",
                style =
                {
                    position = Position.Relative,
                    overflow = Overflow.Hidden
                }
            };
            if (_isArchetypeView)
                _virtualContent.style.minWidth = ComputeContentWidth();
            listContainer.Add(_virtualContent);

            ClearPool();

            _lastVisibleStart = -1;
            _lastVisibleEnd = -1;
            _lastRowCount = -1;
            _prevSearchFilter = "__init__";
            _prevSortColumn = _sortColumn;
            _prevSortAscending = _sortAscending;
            _lastArchetypeIndex = window.SelectedArchetypeIndex;
            _lastQueryId = window.SelectedQueryId;

            unsafe { RefreshData(window, world); }
            UpdateVirtualHeight();
            UpdateVisibleRows(window);
        }

        private static unsafe void CacheComponentColumns(NukecsDashboardWindow window, World world)
        {
            CompTypeIndices.Clear();
            CompColIds.Clear();
            CompIsTag.Clear();
            CompFirstFieldTypes.Clear();
            CompFirstFieldInfos.Clear();
            CompTypes.Clear();
            if (window.SelectedArchetypeIndex < 0) return;

            var archetypes = world.UnsafeWorldRef.archetypesList;
            for (var i = 0; i < archetypes.Length; i++)
            {
                ref var arch = ref archetypes.Ptr[i].Ref;
                if (arch.index != window.SelectedArchetypeIndex) continue;
                for (var ti = 0; ti < arch.types.length; ti++)
                {
                    var typeIndex = arch.types.Ptr[ti];
                    CompTypeIndices.Add(typeIndex);
                    var t = ComponentTypeMap.GetType(typeIndex);
                    var typeName = t != null ? t.Name : $"T{typeIndex}";
                    CompColIds.Add($"comp_{typeName}");
                    CompTypes.Add(t);

                    var td = ComponentTypeMap.GetComponentType(typeIndex);
                    CompIsTag.Add(td.isTag);

                    if (td.isTag || t == null)
                    {
                        CompFirstFieldTypes.Add(null);
                        CompFirstFieldInfos.Add(null);
                    }
                    else
                    {
                        var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
                        if (fields.Length > 0)
                        {
                            CompFirstFieldTypes.Add(fields[0].FieldType);
                            CompFirstFieldInfos.Add(fields[0]);
                        }
                        else
                        {
                            CompFirstFieldTypes.Add(null);
                            CompFirstFieldInfos.Add(null);
                        }
                    }
                }
                break;
            }
        }

        public static unsafe void Update(ScrollView container, NukecsDashboardWindow window)
        {
            if (container == null || window == null) return;
            var world = window.World;
            if (!world.IsAlive) return;

            _scrollRef = container;
            _windowRef = window;

            if (window.SelectedArchetypeIndex != _lastArchetypeIndex)
            {
                Refresh(container, window);
                return;
            }

            if (window.SelectedQueryId != _lastQueryId)
            {
                _lastQueryId = window.SelectedQueryId;
                Refresh(container, window);
                return;
            }

            if (_isQueryView)
            {
                unsafe
                {
                    var w = window.World;
                    var queries = w.UnsafeWorld->queries;
                    for (var i = 0; i < queries.Length; i++)
                    {
                        if (queries.Ptr[i].Ptr->Id == window.SelectedQueryId)
                        {
                            if (!queries.Ptr[i].Ptr->IsDirty())
                                return;
                            break;
                        }
                    }
                }
            }

            var sortChanged = _prevSortColumn != _sortColumn || _prevSortAscending != _sortAscending;
            var filterChanged = _prevSearchFilter != _searchFilter;

            RefreshData(window, world);

            var countChanged = CachedRows.Count != _lastRowCount;
            if (countChanged)
                UpdateVirtualHeight();

            if (filterChanged || sortChanged || countChanged)
            {
                _lastVisibleStart = -1;
                _lastVisibleEnd = -1;
            }

            _prevSearchFilter = _searchFilter;
            _prevSortColumn = _sortColumn;
            _prevSortAscending = _sortAscending;

            UpdateVisibleRows(window);
        }

        private static unsafe void RefreshData(NukecsDashboardWindow window, World world)
        {
            CachedRows.Clear();
            _isArchetypeView = window.SelectedArchetypeIndex >= 0;
            _isQueryView = window.SelectedQueryId >= 0 && window.SelectedArchetypeIndex < 0;
            var hasFilter = !string.IsNullOrEmpty(_searchFilter);
            var filter = hasFilter ? _searchFilter.ToLower() : null;

            if (_isArchetypeView)
                RefreshDataArchetype(window, world, hasFilter, filter);
            else if (_isQueryView)
                RefreshDataQuery(window, world, hasFilter, filter);
            else
                RefreshDataAll(window, world, hasFilter, filter);

            CachedRows.Sort((a, b) =>
            {
                int cmp;
                switch (_sortColumn)
                {
                    case "Name":
                        cmp = string.Compare(a.name, b.name, StringComparison.Ordinal);
                        break;
                    case "Archetype":
                        cmp = a.archHash.CompareTo(b.archHash);
                        break;
                    case "Components":
                        cmp = a.componentNames.Count.CompareTo(b.componentNames.Count);
                        break;
                    default:
                        cmp = a.id.CompareTo(b.id);
                        break;
                }
                return _sortAscending ? cmp : -cmp;
            });

            _lastRowCount = CachedRows.Count;
        }

        private static unsafe void RefreshDataArchetype(
            NukecsDashboardWindow window, World world, bool hasFilter, string filter)
        {
            var archetypes = world.UnsafeWorldRef.archetypesList;
            var nameIdx = ComponentType<Name>.Data.index;
            var nameSize = ComponentType<Name>.Data.size;
            var isPrefabIdx = ComponentType<IsPrefab>.Data.index;

            for (var ai = 0; ai < archetypes.Length; ai++)
            {
                ref var arch = ref archetypes.Ptr[ai].Ref;
                if (arch.index != window.SelectedArchetypeIndex) continue;

                int nameLocal = -1;
                bool hasPrefab = false;
                for (var ti = 0; ti < arch.types.length; ti++)
                {
                    if (arch.types.Ptr[ti] == nameIdx) nameLocal = ti;
                    if (arch.types.Ptr[ti] == isPrefabIdx) hasPrefab = true;
                }

                var count = arch.count;
                for (var row = 0; row < count; row++)
                {
                    var eId = arch.packedEntities.Ptr[row];

                    string eName = "";
                    if (nameLocal >= 0)
                    {
                        var off = arch.componentOffsets.Ptr[nameLocal];
                        var p = (Name*)(arch.data.Ptr + off + row * nameSize);
                        eName = p->value.Value;
                    }

                    if (hasFilter)
                    {
                        var match = eName.ToLower().Contains(filter)
                                    || eId.ToString().Contains(filter)
                                    || arch.hashId.ToString().Contains(filter);
                        if (!match) continue;
                    }

                    var componentNames = new List<string>();
                    var componentIndices = new List<int>();
                    var componentCellTexts = new List<string>();

                    for (var ti = 0; ti < arch.types.length; ti++)
                    {
                        var typeIndex = arch.types.Ptr[ti];
                        var t = ComponentTypeMap.GetType(typeIndex);
                        var typeName = t != null ? t.Name : $"T{typeIndex}";
                        componentNames.Add(typeName);
                        componentIndices.Add(typeIndex);

                        var td = ComponentTypeMap.GetComponentType(typeIndex);
                        if (td.isTag)
                        {
                            componentCellTexts.Add("#tag");
                        }
                        else
                        {
                            var localTi = ti;
                            var isEditable = localTi < CompFirstFieldTypes.Count
                                             && CompFirstFieldTypes[localTi] != null
                                             && IsEditableType(CompFirstFieldTypes[localTi]);

                            if (isEditable)
                            {
                                componentCellTexts.Add("");
                            }
                            else
                            {
                                var boxed = arch.GetObject(eId, typeIndex);
                                if (boxed != null)
                                {
                                    var val = boxed.GetFieldValue(0);
                                    componentCellTexts.Add(val != null ? Truncate(FormatValue(val), 22) : "");
                                }
                                else
                                {
                                    componentCellTexts.Add("");
                                }
                            }
                        }
                    }

                    CachedRows.Add(new EntityRowData
                    {
                        id = eId,
                        name = eName,
                        archHash = arch.hashId,
                        archIndex = arch.index,
                        isPrefab = hasPrefab,
                        componentNames = componentNames,
                        componentIndices = componentIndices,
                        componentCellTexts = componentCellTexts
                    });
                }
                break;
            }
        }

        private static unsafe void RefreshDataAll(
            NukecsDashboardWindow window, World world, bool hasFilter, string filter)
        {
            var entities = world.UnsafeWorldRef.entitiesDens.GetAliveEntities();
            var nameIdx = ComponentType<Name>.Data.index;
            var nameSize = ComponentType<Name>.Data.size;
            var isPrefabIdx = ComponentType<IsPrefab>.Data.index;

            for (var i = 0; i < entities.Length; i++)
            {
                var eId = entities[i];
                ref var arch = ref world.UnsafeWorldRef.GetEntityArchetypePtr(eId).Ref;
                ref var loc = ref world.UnsafeWorldRef.entityLocations.Ptr[eId];
                var row = loc.row;

                int nameLocal = -1;
                bool hasPrefab = false;
                for (var ti = 0; ti < arch.types.length; ti++)
                {
                    if (arch.types.Ptr[ti] == nameIdx) nameLocal = ti;
                    if (arch.types.Ptr[ti] == isPrefabIdx) hasPrefab = true;
                }

                string eName = "";
                if (nameLocal >= 0)
                {
                    var off = arch.componentOffsets.Ptr[nameLocal];
                    var p = (Name*)(arch.data.Ptr + off + row * nameSize);
                    eName = p->value.Value;
                }

                if (hasFilter)
                {
                    var match = eName.ToLower().Contains(filter)
                                || eId.ToString().Contains(filter)
                                || arch.hashId.ToString().Contains(filter);
                    if (!match) continue;
                }

                var componentNames = new List<string>();
                var componentIndices = new List<int>();

                for (var ti = 0; ti < arch.types.length; ti++)
                {
                    var typeIndex = arch.types.Ptr[ti];
                    var t = ComponentTypeMap.GetType(typeIndex);
                    var typeName = t != null ? t.Name : $"T{typeIndex}";
                    componentNames.Add(typeName);
                    componentIndices.Add(typeIndex);
                }

                CachedRows.Add(new EntityRowData
                {
                    id = eId,
                    name = eName,
                    archHash = arch.hashId,
                    archIndex = arch.index,
                    isPrefab = hasPrefab,
                    componentNames = componentNames,
                    componentIndices = componentIndices,
                    componentCellTexts = null
                });
            }
        }

        private static unsafe void RefreshDataQuery(
            NukecsDashboardWindow window, World world, bool hasFilter, string filter)
        {
            var queries = world.UnsafeWorld->queries;
            QueryUnsafe* selectedQuery = null;
            for (var i = 0; i < queries.Length; i++)
            {
                if (queries.Ptr[i].Ptr->Id == window.SelectedQueryId)
                {
                    selectedQuery = queries.Ptr[i].Ptr;
                    break;
                }
            }
            if (selectedQuery == null) return;

            var nameIdx = ComponentType<Name>.Data.index;
            var nameSize = ComponentType<Name>.Data.size;
            var isPrefabIdx = ComponentType<IsPrefab>.Data.index;

            for (var ai = 0; ai < selectedQuery->matchingArchetypes.length; ai++)
            {
                var archetypeIndex = selectedQuery->matchingArchetypes.Ptr[ai];
                ref var arch = ref world.UnsafeWorld->archetypesList.Ptr[archetypeIndex].Ref;

                int nameLocal = -1;
                bool hasPrefab = false;
                for (var ti = 0; ti < arch.types.length; ti++)
                {
                    if (arch.types.Ptr[ti] == nameIdx) nameLocal = ti;
                    if (arch.types.Ptr[ti] == isPrefabIdx) hasPrefab = true;
                }

                var count = arch.count;
                for (var row = 0; row < count; row++)
                {
                    var eId = arch.packedEntities.Ptr[row];

                    string eName = "";
                    if (nameLocal >= 0)
                    {
                        var off = arch.componentOffsets.Ptr[nameLocal];
                        var p = (Name*)(arch.data.Ptr + off + row * nameSize);
                        eName = p->value.Value;
                    }

                    if (hasFilter)
                    {
                        var match = eName.ToLower().Contains(filter)
                                    || eId.ToString().Contains(filter)
                                    || arch.hashId.ToString().Contains(filter);
                        if (!match) continue;
                    }

                    var componentNames = new List<string>();
                    var componentIndices = new List<int>();

                    for (var ti = 0; ti < arch.types.length; ti++)
                    {
                        var typeIndex = arch.types.Ptr[ti];
                        var t = ComponentTypeMap.GetType(typeIndex);
                        var typeName = t != null ? t.Name : $"T{typeIndex}";
                        componentNames.Add(typeName);
                        componentIndices.Add(typeIndex);
                    }

                    CachedRows.Add(new EntityRowData
                    {
                        id = eId,
                        name = eName,
                        archHash = arch.hashId,
                        archIndex = arch.index,
                        isPrefab = hasPrefab,
                        componentNames = componentNames,
                        componentIndices = componentIndices,
                        componentCellTexts = null
                    });
                }
            }
        }

        private static void UpdateVirtualHeight()
        {
            if (_virtualContent == null) return;
            _virtualContent.style.height = CachedRows.Count * RowHeight;
        }

        private static void UpdateVisibleRows(NukecsDashboardWindow window)
        {
            if (_virtualContent == null || _scrollRef == null) return;

            var scrollY = _scrollRef.scrollOffset.y;
            var viewportHeight = _scrollRef.layout.height;
            if (viewportHeight <= 0) viewportHeight = 600f;

            var headerOffset = 80f;
            var adjustedScroll = Mathf.Max(0, scrollY - headerOffset);

            var totalRows = CachedRows.Count;
            var firstVisible = Mathf.Max(0, (int)(adjustedScroll / RowHeight) - Overscan);
            var visibleCount = Mathf.CeilToInt(viewportHeight / RowHeight) + Overscan * 2 + 4;
            var lastVisible = Mathf.Min(totalRows - 1, firstVisible + visibleCount);

            if (firstVisible == _lastVisibleStart && lastVisible == _lastVisibleEnd)
            {
                var idx = firstVisible;
                for (var p = 0; p < RowPool.Count; p++)
                {
                    if (RowPool[p].parent == _virtualContent && idx <= lastVisible)
                    {
                        ResetRow(RowPool[p], CachedRows[idx], idx, window);
                        idx++;
                    }
                }
                return;
            }

            _lastVisibleStart = firstVisible;
            _lastVisibleEnd = lastVisible;

            for (var p = 0; p < RowPool.Count; p++)
            {
                if (RowPool[p].parent == _virtualContent)
                    _virtualContent.Remove(RowPool[p]);
            }

            for (var idx = firstVisible; idx <= lastVisible; idx++)
            {
                var row = GetOrCreatePooledRow();
                ResetRow(row, CachedRows[idx], idx, window);
                row.style.top = idx * RowHeight;
                _virtualContent.Add(row);
            }
        }

        private static void ClearPool()
        {
            for (var i = 0; i < RowPool.Count; i++)
            {
                if (RowPool[i].parent != null)
                    RowPool[i].RemoveFromHierarchy();
            }
            RowPool.Clear();
        }

        private static VisualElement GetOrCreatePooledRow()
        {
            for (var i = 0; i < RowPool.Count; i++)
            {
                if (RowPool[i].parent != _virtualContent)
                    return RowPool[i];
            }

            var row = CreatePooledRowTemplate();
            RowPool.Add(row);
            return row;
        }

        private static VisualElement CreatePooledRowTemplate()
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = ROW_PAD,
                    paddingRight = ROW_PAD,
                    paddingTop = 5,
                    paddingBottom = 5,
                    height = RowHeight,
                    overflow = Overflow.Hidden,
                    position = Position.Absolute,
                    left = 0,
                    right = 0
                }
            };

            if (_isArchetypeView)
            {
                row.style.width = ComputeContentWidth();

                var entityLabel = new Label { name = "cell-Entity" };
                ApplyCellTextStyle(entityLabel, DashboardTheme.FontSize.Body, DashboardTheme.TextSecondary, FontStyle.Bold);
                row.Add(MakeTableCell(entityLabel, "Entity", DEFAULT_ENTITY));

                var nameLabel = new Label { name = "cell-Name" };
                ApplyCellTextStyle(nameLabel, DashboardTheme.FontSize.Body, DashboardTheme.TextPrimary, FontStyle.Normal);
                row.Add(MakeTableCell(nameLabel, "Name", DEFAULT_NAME));

                for (var ci = 0; ci < CompColIds.Count; ci++)
                {
                    var colId = CompColIds[ci];
                    var fieldType = ci < CompFirstFieldTypes.Count ? CompFirstFieldTypes[ci] : null;

                    if (ci < CompIsTag.Count && CompIsTag[ci])
                    {
                        var cellLabel = new Label { name = $"cell-{colId}" };
                        ApplyCellTextStyle(cellLabel, DashboardTheme.FontSize.Small, DashboardTheme.TextSecondary, FontStyle.Normal);
                        cellLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                        row.Add(MakeTableCell(cellLabel, colId, DEFAULT_COMP));
                    }
                    else if (fieldType == typeof(int))
                    {
                        var input = new IntegerField("") { name = $"cell-{colId}" };
                        StyleFieldInput(input);
                        var captured = ci;
                        input.RegisterValueChangedCallback(evt => OnFieldChanged(input, captured, evt.newValue));
                        row.Add(MakeTableCell(input, colId, DEFAULT_COMP));
                    }
                    else if (fieldType == typeof(float))
                    {
                        var input = new FloatField("") { name = $"cell-{colId}" };
                        StyleFieldInput(input);
                        var captured = ci;
                        input.RegisterValueChangedCallback(evt => OnFieldChanged(input, captured, evt.newValue));
                        row.Add(MakeTableCell(input, colId, DEFAULT_COMP));
                    }
                    else if (fieldType == typeof(bool))
                    {
                        var input = new Toggle { name = $"cell-{colId}" };
                        input.style.marginTop = 0;
                        input.style.marginBottom = 0;
                        input.style.alignSelf = Align.Center;
                        var captured = ci;
                        input.RegisterValueChangedCallback(evt => OnFieldChanged(input, captured, evt.newValue));
                        row.Add(MakeTableCell(input, colId, DEFAULT_COMP));
                    }
                    else if (fieldType == typeof(string))
                    {
                        var input = new TextField("") { name = $"cell-{colId}" };
                        StyleFieldInput(input);
                        var captured = ci;
                        input.RegisterValueChangedCallback(evt => OnFieldChanged(input, captured, evt.newValue));
                        row.Add(MakeTableCell(input, colId, DEFAULT_COMP));
                    }
                    else
                    {
                        var cellLabel = new Label { name = $"cell-{colId}" };
                        ApplyCellTextStyle(cellLabel, DashboardTheme.FontSize.Small, DashboardTheme.TextSecondary, FontStyle.Normal);
                        cellLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                        row.Add(MakeTableCell(cellLabel, colId, DEFAULT_COMP));
                    }
                }
            }
            else
            {
                var entityLabel = new Label { name = "cell-Entity" };
                entityLabel.style.flexShrink = 0;
                entityLabel.style.overflow = Overflow.Hidden;
                entityLabel.style.fontSize = DashboardTheme.FontSize.Body;
                entityLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                row.Add(MakeFixedCell(entityLabel, GetWidth("Entity", DEFAULT_ENTITY)));

                var nameLabel = new Label { name = "cell-Name" };
                nameLabel.style.flexShrink = 0;
                nameLabel.style.overflow = Overflow.Hidden;
                nameLabel.style.fontSize = DashboardTheme.FontSize.Body;
                row.Add(MakeFixedCell(nameLabel, GetWidth("Name", DEFAULT_NAME + 30)));

                var archLabel = new Label { name = "cell-Archetype" };
                archLabel.style.flexShrink = 0;
                archLabel.style.overflow = Overflow.Hidden;
                archLabel.style.fontSize = DashboardTheme.FontSize.Small;
                row.Add(MakeFixedCell(archLabel, GetWidth("Archetype", DEFAULT_ARCH)));

                var badges = new VisualElement
                {
                    name = "cell-badges",
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        overflow = Overflow.Hidden,
                        flexGrow = 1
                    }
                };
                row.Add(badges);
            }

            RegisterRowCallbacks(row);
            return row;
        }

        private static void StyleFieldInput(VisualElement field)
        {
            field.style.flexGrow = 1;
            field.style.marginTop = 0;
            field.style.marginBottom = 0;
            field.style.paddingTop = 0;
            field.style.paddingBottom = 0;

            var inputEl = field.Q(className: "unity-base-field__input");
            if (inputEl != null)
            {
                inputEl.style.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
                inputEl.style.borderTopWidth = 0;
                inputEl.style.borderBottomWidth = 1;
                inputEl.style.borderLeftWidth = 0;
                inputEl.style.borderRightWidth = 0;
                inputEl.style.borderBottomColor = DashboardTheme.Separator.WithAlpha(0.3f);
                inputEl.style.color = DashboardTheme.TextPrimary;
                inputEl.style.fontSize = DashboardTheme.FontSize.Small;
            }

            var labelEl = field.Q<Label>(className: "unity-base-field__label");
            if (labelEl != null)
                labelEl.style.display = DisplayStyle.None;
        }

        private static void RegisterRowCallbacks(VisualElement row)
        {
            row.RegisterCallback<MouseUpEvent>(evt =>
            {
                var idx = (int)row.userData;
                if (idx < 0 || idx >= CachedRows.Count) return;
                if (_windowRef == null) return;
                if (evt.target is Toggle || evt.target is IntegerField || evt.target is FloatField || evt.target is TextField)
                    return;
                _windowRef.SelectEntity(CachedRows[idx].id);
                _lastVisibleStart = -1;
                _lastVisibleEnd = -1;
            });

            row.RegisterCallback<MouseEnterEvent>(evt =>
            {
                var idx = (int)row.userData;
                if (idx < 0 || idx >= CachedRows.Count) return;
                if (_windowRef == null) return;
                var eid = CachedRows[idx].id;
                if (_windowRef.SelectedEntityId == eid) return;

                row.style.backgroundColor = DashboardTheme.BgCard.WithAlpha(0.5f);
                if (row.Q<VisualElement>("hover-glow") == null)
                {
                    var accent = DashboardTheme.AccentForArchetype(CachedRows[idx].archHash);
                    row.Add(new VisualElement
                    {
                        name = "hover-glow",
                        style =
                        {
                            position = Position.Absolute,
                            left = 0, top = 0, bottom = 0,
                            width = 1,
                            backgroundColor = accent.WithAlpha(0.6f)
                        }
                    });
                }
            });

            row.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                var idx = (int)row.userData;
                if (idx < 0 || idx >= CachedRows.Count) return;
                if (_windowRef == null) return;
                if (_windowRef.SelectedEntityId == CachedRows[idx].id) return;

                row.style.backgroundColor = Color.clear;
                var hoverBar = row.Q<VisualElement>("hover-glow");
                if (hoverBar != null)
                    row.Remove(hoverBar);
            });
        }

        private static void ResetRow(VisualElement row, EntityRowData item, int rowIndex, NukecsDashboardWindow window)
        {
            var selected = window.SelectedEntityId == item.id;
            row.userData = rowIndex;

            row.style.backgroundColor = selected ? DashboardTheme.BgCardSelected : Color.clear;

            if (_isArchetypeView)
                row.style.width = ComputeContentWidth();

            var sel = row.Q<VisualElement>("sel-bar");
            if (sel != null) row.Remove(sel);
            var hov = row.Q<VisualElement>("hover-glow");
            if (hov != null) row.Remove(hov);

            if (selected)
            {
                row.Add(new VisualElement
                {
                    name = "sel-bar",
                    style =
                    {
                        position = Position.Absolute,
                        left = 0, top = 0, bottom = 0,
                        width = _isArchetypeView ? 3 : 4,
                        backgroundColor = DashboardTheme.AccentPurple,
                        borderTopLeftRadius = _isArchetypeView ? 0 : 4,
                        borderBottomLeftRadius = _isArchetypeView ? 0 : 4
                    }
                });
            }

            if (_isArchetypeView)
                ResetArchetypeRow(row, item, selected);
            else
                ResetAllRow(row, item, selected);        }

        private static unsafe void ResetArchetypeRow(VisualElement row, EntityRowData item, bool selected)
        {
            var entityLabel = row.Q<Label>("cell-Entity");
            if (entityLabel != null)
            {
                entityLabel.text = $"#:{item.id:D7}";
                entityLabel.style.color = selected ? DashboardTheme.AccentCyan : DashboardTheme.TextSecondary;
                entityLabel.style.width = GetWidth("Entity", DEFAULT_ENTITY);
            }

            var nameLabel = row.Q<Label>("cell-Name");
            if (nameLabel != null)
            {
                nameLabel.text = string.IsNullOrEmpty(item.name) ? "Entity" : item.name;
                nameLabel.style.color = item.isPrefab ? DashboardTheme.AccentOrange : DashboardTheme.TextPrimary;
                nameLabel.style.width = GetWidth("Name", DEFAULT_NAME);
            }

            for (var ci = 0; ci < CompColIds.Count; ci++)
            {
                var colId = CompColIds[ci];
                var isTag = ci < CompIsTag.Count && CompIsTag[ci];
                var fieldType = ci < CompFirstFieldTypes.Count ? CompFirstFieldTypes[ci] : null;

                if (isTag)
                {
                    var cellLabel = row.Q<Label>($"cell-{colId}");
                    if (cellLabel != null)
                    {
                        cellLabel.text = "#tag";
                        cellLabel.style.color = DashboardTheme.AccentGreen;
                        cellLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                        cellLabel.style.width = GetWidth(colId, DEFAULT_COMP);
                    }
                }
                else if (fieldType == typeof(int))
                {
                    var input = row.Q<IntegerField>($"cell-{colId}");
                    if (input != null)
                    {
                        var val = ReadFieldValue(item.id, ci);
                        if (val != null) input.SetValueWithoutNotify((int)val);
                        input.style.width = GetWidth(colId, DEFAULT_COMP);
                    }
                }
                else if (fieldType == typeof(float))
                {
                    var input = row.Q<FloatField>($"cell-{colId}");
                    if (input != null)
                    {
                        var val = ReadFieldValue(item.id, ci);
                        if (val != null) input.SetValueWithoutNotify((float)val);
                        input.style.width = GetWidth(colId, DEFAULT_COMP);
                    }
                }
                else if (fieldType == typeof(bool))
                {
                    var input = row.Q<Toggle>($"cell-{colId}");
                    if (input != null)
                    {
                        var val = ReadFieldValue(item.id, ci);
                        if (val != null) input.SetValueWithoutNotify((bool)val);
                        input.style.width = GetWidth(colId, DEFAULT_COMP);
                    }
                }
                else if (fieldType == typeof(string))
                {
                    var input = row.Q<TextField>($"cell-{colId}");
                    if (input != null)
                    {
                        var val = ReadFieldValue(item.id, ci);
                        if (val != null) input.SetValueWithoutNotify((string)val ?? "");
                        input.style.width = GetWidth(colId, DEFAULT_COMP);
                    }
                }
                else
                {
                    var cellLabel = row.Q<Label>($"cell-{colId}");
                    if (cellLabel != null)
                    {
                        var cellText = ci < item.componentCellTexts.Count ? item.componentCellTexts[ci] : "";
                        cellLabel.text = cellText;
                        cellLabel.style.color = DashboardTheme.TextSecondary;
                        cellLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
                        cellLabel.style.width = GetWidth(colId, DEFAULT_COMP);
                    }
                }
            }

            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = DashboardTheme.Separator.WithAlpha(0.12f);
            row.style.marginBottom = 0;
            row.style.borderTopLeftRadius = 0;
            row.style.borderTopRightRadius = 0;
            row.style.borderBottomLeftRadius = 0;
            row.style.borderBottomRightRadius = 0;
        }

        private static void ResetAllRow(VisualElement row, EntityRowData item, bool selected)
        {
            var entityLabel = row.Q<Label>("cell-Entity");
            if (entityLabel != null)
            {
                entityLabel.text = $"#:{item.id:D7}";
                entityLabel.style.color = selected ? DashboardTheme.AccentCyan : DashboardTheme.TextSecondary;
                entityLabel.style.width = GetWidth("Entity", DEFAULT_ENTITY);
            }

            var nameLabel = row.Q<Label>("cell-Name");
            if (nameLabel != null)
            {
                nameLabel.text = string.IsNullOrEmpty(item.name) ? "Entity" : item.name;
                nameLabel.style.color = item.isPrefab ? DashboardTheme.AccentOrange : DashboardTheme.TextPrimary;
                nameLabel.style.width = GetWidth("Name", DEFAULT_NAME + 30);
            }

            var archLabel = row.Q<Label>("cell-Archetype");
            if (archLabel != null)
            {
                var accentColor = DashboardTheme.AccentForArchetype(item.archHash);
                archLabel.text = $"#{item.archIndex}";
                archLabel.style.color = accentColor;
                archLabel.style.width = GetWidth("Archetype", DEFAULT_ARCH);
            }

            var badges = row.Q<VisualElement>("cell-badges");
            if (badges != null)
            {
                badges.Clear();
                for (var ci = 0; ci < item.componentNames.Count; ci++)
                {
                    var compName = item.componentNames[ci];
                    var chipColor = DashboardTheme.AccentForType(compName);
                    var shortName = compName.Length > COMPONENT_MAX_NAME_LEN
                        ? compName.Substring(0, 7) + ".."
                        : compName;
                    badges.Add(new Label(shortName)
                    {
                        style =
                        {
                            fontSize = DashboardTheme.FontSize.Micro,
                            color = chipColor,
                            backgroundColor = chipColor.WithAlpha(0.18f),
                            paddingTop = 2,
                            paddingBottom = 2,
                            paddingLeft = 8,
                            paddingRight = 8,
                            borderTopLeftRadius = 6,
                            borderTopRightRadius = 6,
                            borderBottomLeftRadius = 6,
                            borderBottomRightRadius = 6,
                            marginRight = 2,
                            marginTop = 1,
                            marginBottom = 1
                        }
                    });
                }
            }

            row.style.borderTopLeftRadius = 4;
            row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = 4;
            row.style.borderBottomRightRadius = 4;
            row.style.marginBottom = 1;
            row.style.borderBottomWidth = 0;
        }

        private static object ReadFieldValue(int entityId, int colIndex)
        {
            var world = _windowRef.World;
            if (!world.IsAlive) return null;
            ref var arch = ref world.UnsafeWorldRef.GetEntityArchetypePtr(entityId).Ref;
            var typeIndex = CompTypeIndices[colIndex];
            var td = ComponentTypeMap.GetComponentType(typeIndex);
            if (td.isTag) return null;
            var boxed = arch.GetObject(entityId, typeIndex);
            if (boxed == null) return null;
            return boxed.GetFieldValue(0);
        }

        private static unsafe void OnFieldChanged(VisualElement input, int colIndex, object newValue)
        {
            if (!ECSDebugWindowUI.CanWriteToWorld) return;
            if (_windowRef == null) return;
            var world = _windowRef.World;
            if (!world.IsAlive) return;

            var rowEl = input.parent;
            if (rowEl == null) return;
            var idx = (int)rowEl.userData;
            if (idx < 0 || idx >= CachedRows.Count) return;
            var entityId = CachedRows[idx].id;

            ref var arch = ref world.UnsafeWorldRef.GetEntityArchetypePtr(entityId).Ref;
            var typeIndex = CompTypeIndices[colIndex];
            var boxed = arch.GetObject(entityId, typeIndex);
            if (boxed == null) return;

            FastReflectionAccessor.SetValue(CompTypes[colIndex], 0, boxed, newValue);
            arch.SetObject(entityId, typeIndex, (IComponent)boxed);
        }

        private static unsafe VisualElement BuildHeaders(
            NukecsDashboardWindow window, World world, bool isArchetypeView, ScrollView container)
        {
            var headerRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    backgroundColor = DashboardTheme.BgPanel,
                    borderBottomWidth = 1,
                    borderBottomColor = DashboardTheme.Separator.WithAlpha(0.5f),
                    paddingLeft = ROW_PAD,
                    paddingRight = ROW_PAD,
                    paddingTop = 5,
                    paddingBottom = 5,
                    flexShrink = 0
                }
            };

            if (isArchetypeView)
            {
                headerRow.style.minWidth = ComputeContentWidth();
                headerRow.style.flexShrink = 0;

                headerRow.Add(MakeResizableHeader("Entity", "Entity", DEFAULT_ENTITY, TextAnchor.MiddleLeft));
                headerRow.Add(MakeResizableHeader("Name", "Name", DEFAULT_NAME, TextAnchor.MiddleLeft));

                for (var ci = 0; ci < CompColIds.Count; ci++)
                {
                    var colId = CompColIds[ci];
                    var typeName = colId.StartsWith("comp_") ? colId.Substring(5) : colId;
                    var shortName = typeName.Length > 14 ? typeName.Substring(0, 12) + ".." : typeName;
                    headerRow.Add(MakeResizableHeader(shortName, colId, DEFAULT_COMP, TextAnchor.MiddleCenter));
                }
            }
            else
            {
                headerRow.Add(MakeResizableHeader("Entity", "Entity", DEFAULT_ENTITY, TextAnchor.MiddleLeft));
                headerRow.Add(MakeResizableHeader("Name", "Name", DEFAULT_NAME + 30, TextAnchor.MiddleLeft));
                headerRow.Add(MakeResizableHeader("Archetype", "Archetype", DEFAULT_ARCH, TextAnchor.MiddleLeft));

                var flexHeader = new VisualElement
                {
                    style = { flexGrow = 1, overflow = Overflow.Hidden }
                };
                var compHeader = CreateSortLabel("Components", "Components", TextAnchor.MiddleLeft);
                compHeader.style.flexGrow = 1;
                flexHeader.Add(compHeader);
                headerRow.Add(flexHeader);
            }

            return headerRow;
        }

        private static VisualElement MakeResizableHeader(
            string text, string colId, float defaultWidth, TextAnchor align)
        {
            var w = GetWidth(colId, defaultWidth);

            var cell = new VisualElement
            {
                name = $"header-{colId}",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    width = w,
                    flexShrink = 0,
                    overflow = Overflow.Hidden,
                    position = Position.Relative,
                    borderRightWidth = 1,
                    borderRightColor = DashboardTheme.Separator.WithAlpha(0.25f)
                }
            };

            var label = CreateSortLabel(text, colId, align);
            label.style.flexGrow = 1;
            cell.Add(label);

            var handle = new VisualElement
            {
                name = $"handle-{colId}",
                style =
                {
                    position = Position.Absolute,
                    right = -3,
                    top = 0,
                    bottom = 0,
                    width = 7
                }
            };

            handle.RegisterCallback<MouseDownEvent>(e =>
            {
                e.StopPropagation();
                _dragColId = colId;
                _dragStartX = e.mousePosition.x;
                _dragStartW = GetWidth(colId, defaultWidth);
            });

            handle.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (string.IsNullOrEmpty(_dragColId))
                    handle.style.backgroundColor = DashboardTheme.AccentCyan.WithAlpha(0.3f);
            });
            handle.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (_dragColId != colId)
                    handle.style.backgroundColor = Color.clear;
            });

            cell.Add(handle);
            return cell;
        }

        private static void OnListMouseMove(MouseMoveEvent e)
        {
            if (string.IsNullOrEmpty(_dragColId)) return;

            var delta = e.mousePosition.x - _dragStartX;
            var newW = Mathf.Max(MIN_COL, _dragStartW + delta);
            ColWidths[_dragColId] = newW;

            var header = _listRef?.Q<VisualElement>($"header-{_dragColId}");
            if (header != null)
                header.style.width = newW;

            _listRef?.Query<VisualElement>(name: $"cell-{_dragColId}").ForEach(c => c.style.width = newW);

            if (_isArchetypeView)
            {
                var cw = ComputeContentWidth();
                _listRef.style.minWidth = cw;
                if (_virtualContent != null) _virtualContent.style.minWidth = cw;
            }
        }

        private static void OnListMouseUp(MouseUpEvent e)
        {
            if (string.IsNullOrEmpty(_dragColId)) return;
            _dragColId = null;
        }

        private static Label CreateSortLabel(string text, string column, TextAnchor align)
        {
            var isActive = _sortColumn == column;
            var label = new Label(isActive ? $"{text} {(_sortAscending ? "\u25B2" : "\u25BC")}" : text)
            {
                style =
                {
                    fontSize = 11,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = isActive ? DashboardTheme.AccentCyan : DashboardTheme.TextSecondary,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 2,
                    paddingBottom = isActive ? 0 : 2,
                    borderBottomWidth = isActive ? 2 : 0,
                    borderBottomColor = DashboardTheme.AccentCyan,
                    unityTextAlign = align,
                    overflow = Overflow.Hidden
                }
            };
            label.RegisterCallback<MouseUpEvent>(ev =>
            {
                if (!string.IsNullOrEmpty(_dragColId)) return;
                if (_sortColumn == column) _sortAscending = !_sortAscending;
                else { _sortColumn = column; _sortAscending = true; }
                _lastVisibleStart = -1;
                _lastVisibleEnd = -1;
            });
            return label;
        }

        private static VisualElement MakeTableCell(VisualElement inner, string colId, float defaultWidth)
        {
            var w = GetWidth(colId, defaultWidth);
            inner.name = $"cell-{colId}";
            inner.style.width = w;
            inner.style.flexShrink = 0;
            inner.style.overflow = Overflow.Hidden;
            inner.style.paddingLeft = 6;
            inner.style.paddingRight = 6;
            inner.style.borderRightWidth = 1;
            inner.style.borderRightColor = DashboardTheme.Separator.WithAlpha(0.15f);
            return inner;
        }

        private static VisualElement MakeFixedCell(VisualElement inner, float width)
        {
            inner.style.width = width;
            inner.style.flexShrink = 0;
            inner.style.overflow = Overflow.Hidden;
            return inner;
        }

        private static void ApplyCellTextStyle(Label label, float fontSize, Color color, FontStyle weight)
        {
            label.style.fontSize = fontSize;
            label.style.color = color;
            label.style.unityFontStyleAndWeight = weight;
            label.style.overflow = Overflow.Hidden;
            label.style.paddingLeft = 6;
            label.style.paddingRight = 6;
        }

        private static string FormatValue(object value)
        {
            if (value == null) return "";
            var s = value.ToString();

            if (value is Enum)
            {
                var dot = s.LastIndexOf('.');
                return dot >= 0 ? s.Substring(dot + 1) : s;
            }

            var parenIdx = s.IndexOf('(');
            if (parenIdx > 0 && char.IsLetter(s[0]))
                return s.Substring(parenIdx);

            return s;
        }

        private static string Truncate(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= maxLen ? s : s.Substring(0, maxLen - 1) + "..";
        }

        public struct EntityRowData
        {
            public int id;
            public string name;
            public int archHash;
            public int archIndex;
            public bool isPrefab;
            public List<string> componentNames;
            public List<int> componentIndices;
            public List<string> componentCellTexts;
        }
    }
}
#endif

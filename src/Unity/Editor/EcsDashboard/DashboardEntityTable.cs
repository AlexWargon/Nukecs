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
        private static string _sortColumn = "Entity";
        private static bool _sortAscending = true;
        private const int COMPONENT_MAX_NAME_LEN = 6;
        private const float DEFAULT_ENTITY = 100;
        private const float DEFAULT_NAME = 100;
        private const float DEFAULT_ARCH = 80;
        private const float DEFAULT_COMP = 110;
        private const float MIN_COL = 40;
        private const float ROW_PAD = 0;

        private static readonly Dictionary<string, float> ColWidths = new Dictionary<string, float>();
        private static VisualElement _listRef;
        private static ScrollView _scrollRef;
        private static NukecsDashboardWindow _windowRef;
        private static string _dragColId;
        private static float _dragStartX;
        private static float _dragStartW;

        private static float GetWidth(string id, float def)
        {
            if (!ColWidths.TryGetValue(id, out var w))
            {
                w = def;
                ColWidths[id] = w;
            }
            return w;
        }

        public static ScrollView Create(NukecsDashboardWindow window)
        {
            var container = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "entity-table",
                style = { flexGrow = 1 }
            };
            return container;
        }

        public static void Refresh(ScrollView container, NukecsDashboardWindow window)
        {
            var world = window.World;
            if (!world.IsAlive) return;

            _scrollRef = container;
            _windowRef = window;

            container.Clear();

            var toolbarRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingBottom = 4
                }
            };

            var searchField = DashboardStyles.CreateSearchField("Search entities...", filter =>
            {
                _searchFilter = filter;
                RefreshList(container, window, world, window.SelectedArchetypeIndex >= 0);
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

            var isArchetypeView = window.SelectedArchetypeIndex >= 0;
            RefreshList(container, window, world, isArchetypeView);
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
                    paddingBottom = 5
                }
            };

            if (isArchetypeView)
            {
                headerRow.Add(MakeResizableHeader("Entity", "Entity", DEFAULT_ENTITY, TextAnchor.MiddleLeft));
                headerRow.Add(MakeResizableHeader("Name", "Name", DEFAULT_NAME, TextAnchor.MiddleLeft));

                var archetypes = world.UnsafeWorldRef.archetypesList;
                for (var i = 0; i < archetypes.Length; i++)
                {
                    var archPtr = archetypes.Ptr[i];
                    ref var arch = ref archPtr.Ref;
                    if (arch.index != window.SelectedArchetypeIndex) continue;

                    foreach (var typeIndex in arch.types)
                    {
                        var t = ComponentTypeMap.GetType(typeIndex);
                        var typeName = t != null ? t.Name : $"T{typeIndex}";
                        var shortName = typeName.Length > 14 ? typeName.Substring(0, 12) + ".." : typeName;
                        headerRow.Add(MakeResizableHeader(shortName, $"comp_{typeName}", DEFAULT_COMP, TextAnchor.MiddleCenter));
                    }
                    break;
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
        }

        private static void OnListMouseUp(MouseUpEvent e)
        {
            if (string.IsNullOrEmpty(_dragColId)) return;
            _dragColId = null;
            if (_scrollRef != null && _windowRef != null)
                RefreshList(_scrollRef, _windowRef, _windowRef.World, _windowRef.SelectedArchetypeIndex >= 0);
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
                if (_scrollRef != null && _windowRef != null)
                    RefreshList(_scrollRef, _windowRef, _windowRef.World, _windowRef.SelectedArchetypeIndex >= 0);
            });
            return label;
        }

        private static unsafe void RefreshList(ScrollView container, NukecsDashboardWindow window, World world, bool isArchetypeView)
        {
            if (!world.IsAlive) return;

            _scrollRef = container;
            _windowRef = window;

            var listContainer = container.Q<VisualElement>("entity-list");
            if (listContainer != null)
            {
                listContainer.Clear();
                listContainer.UnregisterCallback<MouseMoveEvent>(OnListMouseMove);
                listContainer.UnregisterCallback<MouseUpEvent>(OnListMouseUp);
            }
            else
            {
                listContainer = new VisualElement
                {
                    name = "entity-list",
                    style = { overflow = Overflow.Hidden }
                };
                container.Add(listContainer);
            }

            listContainer.RegisterCallback<MouseMoveEvent>(OnListMouseMove);
            listContainer.RegisterCallback<MouseUpEvent>(OnListMouseUp);
            _listRef = listContainer;

            listContainer.Add(BuildHeaders(window, world, isArchetypeView, container));

            var entities = world.UnsafeWorldRef.entitiesDens.GetAliveEntities();
            var items = new List<EntityRowData>();

            for (var i = 0; i < entities.Length; i++)
            {
                var eId = entities[i];
                var e = world.GetEntity(eId);
                if (!e.IsValid()) continue;

                var eName = e.Has<Name>() ? e.Get<Name>().value.Value : "";
                ref var arch = ref world.UnsafeWorldRef.GetEntityArchetypePtr(eId).Ref;

                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    var filter = _searchFilter.ToLower();
                    var matchName = eName.ToLower().Contains(filter);
                    var matchId = eId.ToString().Contains(filter);
                    var matchArch = arch.hashId.ToString().Contains(filter);
                    if (!matchName && !matchId && !matchArch) continue;
                }

                if (window.SelectedArchetypeIndex >= 0 && arch.index != window.SelectedArchetypeIndex)
                    continue;

                var isPrefab = e.Has<IsPrefab>();
                var componentNames = new List<string>();
                var componentIndices = new List<int>();
                var componentCellTexts = new List<string>();

                foreach (var typeIndex in arch.types)
                {
                    var t = ComponentTypeMap.GetType(typeIndex);
                    var typeName = t != null ? t.Name : $"T{typeIndex}";
                    componentNames.Add(typeName);
                    componentIndices.Add(typeIndex);

                    if (isArchetypeView)
                    {
                        var typeData = ComponentTypeMap.GetComponentType(typeIndex);
                        if (typeData.isTag)
                        {
                            componentCellTexts.Add("#tag");
                        }
                        else
                        {
                            var boxed = arch.GetObject(eId, typeIndex);
                            if (boxed != null)
                            {
                                var fields = boxed.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
                                componentCellTexts.Add(fields.Length > 0
                                    ? Truncate(FormatValue(fields[0].GetValue(boxed)), 22)
                                    : "");
                            }
                            else
                            {
                                componentCellTexts.Add("");
                            }
                        }
                    }
                }

                items.Add(new EntityRowData
                {
                    id = eId,
                    name = eName,
                    archHash = arch.hashId,
                    archIndex = arch.index,
                    isPrefab = isPrefab,
                    componentNames = componentNames,
                    componentIndices = componentIndices,
                    componentCellTexts = componentCellTexts
                });
            }

            items.Sort((a, b) =>
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

            foreach (var item in items)
            {
                VisualElement row;
                if (isArchetypeView)
                    row = CreateArchetypeViewRow(item, window);
                else
                    row = CreateAllViewRow(item, window);
                listContainer.Add(row);
            }
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

        private static VisualElement CreateAllViewRow(EntityRowData item, NukecsDashboardWindow window)
        {
            var selected = window.SelectedEntityId == item.id;
            var accentColor = DashboardTheme.AccentForArchetype(item.archHash);

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
                    backgroundColor = selected ? DashboardTheme.BgCardSelected : Color.clear,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    marginBottom = 1,
                    overflow = Overflow.Hidden,
                    position = Position.Relative
                }
            };

            if (selected)
            {
                row.Add(new VisualElement
                {
                    style =
                    {
                        position = Position.Absolute,
                        left = 0, top = 0, bottom = 0,
                        width = 4,
                        backgroundColor = DashboardTheme.AccentPurple,
                        borderTopLeftRadius = 4,
                        borderBottomLeftRadius = 4
                    }
                });
            }

            var entityLabel = new Label($"#:{item.id:D7}")
            {
                style =
                {
                    fontSize = DashboardTheme.FontSize.Body,
                    color = selected ? DashboardTheme.AccentCyan : DashboardTheme.TextSecondary,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            row.Add(MakeFixedCell(entityLabel, GetWidth("Entity", DEFAULT_ENTITY)));

            var nameText = string.IsNullOrEmpty(item.name) ? "Entity" : item.name;
            var nameLabel = new Label(nameText)
            {
                style =
                {
                    fontSize = DashboardTheme.FontSize.Body,
                    color = item.isPrefab ? DashboardTheme.AccentOrange : DashboardTheme.TextPrimary,
                    overflow = Overflow.Hidden
                }
            };
            row.Add(MakeFixedCell(nameLabel, GetWidth("Name", DEFAULT_NAME + 30)));

            var archLabel = new Label($"#{item.archIndex}")
            {
                style =
                {
                    fontSize = DashboardTheme.FontSize.Small,
                    color = accentColor
                }
            };
            row.Add(MakeFixedCell(archLabel, GetWidth("Archetype", DEFAULT_ARCH)));

            var badgesContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    overflow = Overflow.Hidden,
                    flexGrow = 1
                }
            };

            foreach (var compName in item.componentNames)
            {
                var chipColor = DashboardTheme.AccentForType(compName);
                var chip = new Label(compName.Length > COMPONENT_MAX_NAME_LEN ? compName.Substring(0, 7) + ".." : compName)
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
                };
                badgesContainer.Add(chip);
            }

            row.Add(badgesContainer);

            AddRowInteractions(row, item.id, accentColor, selected, window);
            return row;
        }

        private static VisualElement CreateArchetypeViewRow(EntityRowData item, NukecsDashboardWindow window)
        {
            var selected = window.SelectedEntityId == item.id;
            var accentColor = DashboardTheme.AccentForArchetype(item.archHash);

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
                    backgroundColor = selected ? DashboardTheme.BgCardSelected : Color.clear,
                    borderBottomWidth = 1,
                    borderBottomColor = DashboardTheme.Separator.WithAlpha(0.12f),
                    overflow = Overflow.Hidden,
                    position = Position.Relative
                }
            };

            if (selected)
            {
                row.Add(new VisualElement
                {
                    style =
                    {
                        position = Position.Absolute,
                        left = 0, top = 0, bottom = 0,
                        width = 3,
                        backgroundColor = DashboardTheme.AccentPurple
                    }
                });
            }

            var entityLabel = new Label($"#:{item.id:D7}")
            {
                style =
                {
                    fontSize = DashboardTheme.FontSize.Body,
                    color = selected ? DashboardTheme.AccentCyan : DashboardTheme.TextSecondary,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            row.Add(MakeTableCell(entityLabel, "Entity", DEFAULT_ENTITY));

            var nameText = string.IsNullOrEmpty(item.name) ? "Entity" : item.name;
            var nameLabel = new Label(nameText)
            {
                style =
                {
                    fontSize = DashboardTheme.FontSize.Body,
                    color = item.isPrefab ? DashboardTheme.AccentOrange : DashboardTheme.TextPrimary,
                    overflow = Overflow.Hidden
                }
            };
            row.Add(MakeTableCell(nameLabel, "Name", DEFAULT_NAME));

            for (var ci = 0; ci < item.componentNames.Count; ci++)
            {
                var compName = item.componentNames[ci];
                var colId = $"comp_{compName}";
                var chipColor = DashboardTheme.TextSecondary;
                var cellText = ci < item.componentCellTexts.Count ? item.componentCellTexts[ci] : "";
                var isTag = cellText == "#tag";

                var cellLabel = new Label(cellText)
                {
                    style =
                    {
                        fontSize = DashboardTheme.FontSize.Small,
                        color = isTag ? DashboardTheme.AccentGreen : chipColor,
                        unityFontStyleAndWeight = isTag ? FontStyle.Bold : FontStyle.Normal,
                        overflow = Overflow.Hidden,
                        unityTextAlign = TextAnchor.MiddleCenter
                    }
                };
                row.Add(MakeTableCell(cellLabel, colId, DEFAULT_COMP));
            }

            AddRowInteractions(row, item.id, accentColor, selected, window);
            return row;
        }

        private static void AddRowInteractions(
            VisualElement row, int entityId, Color accentColor,
            bool selected, NukecsDashboardWindow window)
        {
            row.RegisterCallback<MouseUpEvent>(_ =>
            {
                window.SelectEntity(entityId);
                RefreshList(
                    row.parent as ScrollView ?? row.parent.GetFirstAncestorOfType<ScrollView>(),
                    window, window.World, window.SelectedArchetypeIndex >= 0);
            });

            row.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (!selected)
                {
                    row.style.backgroundColor = DashboardTheme.BgCard.WithAlpha(0.5f);
                    var hoverBar = new VisualElement
                    {
                        name = "hover-glow",
                        style =
                        {
                            position = Position.Absolute,
                            left = 0, top = 0, bottom = 0,
                            width = 1,
                            backgroundColor = accentColor.WithAlpha(0.6f)
                        }
                    };
                    row.Add(hoverBar);
                }
            });
            row.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (window.SelectedEntityId != entityId)
                {
                    row.style.backgroundColor = Color.clear;
                    var hoverBar = row.Q<VisualElement>("hover-glow");
                    if (hoverBar != null)
                        row.Remove(hoverBar);
                }
            });
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
#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public static class EntitiesTab
    {
        private static HashSet<string> _lastArchetypeSet = new HashSet<string>();
        private static HashSet<string> _currentArchetypeSet = new HashSet<string>();
        private static List<string> _archetypeSortBuffer = new List<string>();
        private static List<EntityInfo> _filteredBuffer = new List<EntityInfo>();
        private static bool _suppressSelection;

        public static VisualElement Create(EcsDebugV2Window window)
        {
            _lastArchetypeSet.Clear();
            _suppressSelection = false;

            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    flexGrow = 1
                }
            };

            var toolbar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 6,
                    paddingBottom = 6,
                    backgroundColor = EcsDebugV2Theme.PanelElevated,
                    borderBottomWidth = 1,
                    borderBottomColor = EcsDebugV2Theme.PanelBorder,
                    flexShrink = 0
                }
            };

            var searchContainer = new VisualElement
            {
                style =
                {
                    position = Position.Relative,
                    flexGrow = 1,
                    maxWidth = 240
                }
            };

            var searchField = EcsDebugV2Theme.CreateSearchField("", (q) =>
            {
                window.searchQuery = q;
                Refresh(container, window);
            });
            searchField.name = "entity-search";
            searchContainer.Add(searchField);

            var placeholder = new Label("filter entities\u2026")
            {
                name = "search-placeholder",
                pickingMode = PickingMode.Ignore
            };
            placeholder.style.fontSize = EcsDebugV2Theme.Font.Small;
            placeholder.style.color = EcsDebugV2Theme.MutedTextA05;
            placeholder.style.position = Position.Absolute;
            placeholder.style.left = 7;
            placeholder.style.top = 0;
            placeholder.style.bottom = 0;
            placeholder.style.unityTextAlign = TextAnchor.MiddleLeft;
            searchContainer.Add(placeholder);
            toolbar.Add(searchContainer);

            var filterRow = new VisualElement
            {
                name = "arch-filter-row",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginLeft = 8,
                    overflow = Overflow.Hidden,
                    flexShrink = 1
                }
            };
            toolbar.Add(filterRow);

            var countLabel = new Label("0/0")
            {
                name = "entity-count",
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.MutedText,
                    marginLeft = 8,
                    flexShrink = 0
                }
            };
            toolbar.Add(countLabel);
            container.Add(toolbar);

            var tableHeader = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    backgroundColor = EcsDebugV2Theme.PanelElevated,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 4,
                    paddingBottom = 4,
                    borderBottomWidth = 1,
                    borderBottomColor = EcsDebugV2Theme.PanelBorder,
                    flexShrink = 0
                }
            };
            tableHeader.Add(MakeHeaderCell("ID", 70));
            tableHeader.Add(MakeHeaderCell("Name", 0, true));
            tableHeader.Add(MakeHeaderCell("Archetype", 100));
            container.Add(tableHeader);

            var filtered = FilterEntities(window);

            ListView listView = null;
            listView = new ListView(filtered, 24,
                () =>
                {
                    var row = new VisualElement
                    {
                        style =
                        {
                            flexDirection = FlexDirection.Row,
                            paddingLeft = 8,
                            paddingRight = 8,
                            paddingTop = 4,
                            paddingBottom = 4,
                            borderBottomWidth = 1,
                            borderBottomColor = EcsDebugV2Theme.PanelBorderA04,
                            overflow = Overflow.Hidden
                        }
                    };
                    row.Add(MakeDataCell("", EcsDebugV2Theme.TypeEntity, 70));
                    row.Add(MakeDataCell("", EcsDebugV2Theme.Foreground, 0, true));
                    row.Add(MakeDataCell("", EcsDebugV2Theme.Orange, 100));
                    row.RegisterCallback<MouseEnterEvent>(evt =>
                    {
                        var r = evt.currentTarget as VisualElement;
                        if (r == null) return;
                        var id = (int)r.userData;
                            if (window.selectedEntityId != id)
                            r.style.backgroundColor = EcsDebugV2Theme.PanelElevatedA04;
                    });
                    row.RegisterCallback<MouseLeaveEvent>(evt =>
                    {
                        var r = evt.currentTarget as VisualElement;
                        if (r == null) return;
                        var id = (int)r.userData;
                        if (window.selectedEntityId != id)
                            r.style.backgroundColor = Color.clear;
                    });
                    return row;
                },
                (ve, idx) =>
                {
                    if (idx < 0 || idx >= _filteredBuffer.Count) return;
                    var e = _filteredBuffer[idx];
                    ve.userData = e.Id;
                    ve.name = $"erow-{e.Id}";
                    bool selected = window.selectedEntityId == e.Id;
                    ve.style.backgroundColor = selected
                        ? EcsDebugV2Theme.LimeA01
                        : Color.clear;
                    int ci = 0;
                    foreach (var child in ve.Children())
                    {
                        if (!(child is Label label)) continue;
                        if (ci == 0) label.text = $"#{e.Id}";
                        else if (ci == 1) label.text = e.Name;
                        else if (ci == 2) label.text = e.Archetype;
                        ci++;
                    }
                })
            {
                selectionType = SelectionType.Single,
                name = "entity-list",
                style =
                {
                    flexGrow = 1,
                    overflow = Overflow.Hidden
                }
            };

            listView.onSelectionChange += objects =>
            {
                if (_suppressSelection) return;
                foreach (var o in objects)
                {
                    if (o is EntityInfo info)
                    {
                        window.SelectEntity(info.Id);
                        break;
                    }
                }
            };
            listView.makeNoneElement = () => new VisualElement();
            if (window.selectedEntityId.HasValue)
            {
                var idx = filtered.FindIndex(e => e.Id == window.selectedEntityId.Value);
                if (idx >= 0)
                {
                    _suppressSelection = true;
                    listView.selectedIndex = idx;
                    _suppressSelection = false;
                }
            }

            container.Add(listView);

            BuildArchetypeFilters(container, window);

            if (container.Q("entity-count") is Label cl)
                cl.text = $"{filtered.Count}/{window.entities.Count}";

            UpdatePlaceholder(container);
            return container;
        }

        public static void RefreshSelection(VisualElement leftPanel, EcsDebugV2Window window)
        {
            var container = leftPanel.Q("left-panel") ?? leftPanel;
            var listView = container.Q<ListView>("entity-list");
            if (listView == null) return;
            _suppressSelection = true;
            if (window.selectedEntityId.HasValue)
            {
                var idx = _filteredBuffer.FindIndex(e => e.Id == window.selectedEntityId.Value);
                listView.selectedIndex = idx >= 0 ? idx : -1;
            }
            else
            {
                listView.selectedIndex = -1;
            }
            _suppressSelection = false;
            listView.Rebuild();
        }

        public static void Refresh(VisualElement container, EcsDebugV2Window window)
        {
            BuildArchetypeFilters(container, window);

            var listView = container.Q<ListView>("entity-list");
            if (listView == null) return;

            var filtered = FilterEntities(window);
            listView.itemsSource = filtered;
            listView.Rebuild();

            if (window.selectedEntityId.HasValue)
            {
                var idx = filtered.FindIndex(e => e.Id == window.selectedEntityId.Value);
                _suppressSelection = true;
                listView.selectedIndex = idx >= 0 ? idx : -1;
                _suppressSelection = false;
            }
            else
            {
                _suppressSelection = true;
                listView.selectedIndex = -1;
                _suppressSelection = false;
            }

            if (container.Q("entity-count") is Label countLabel)
                countLabel.text = $"{filtered.Count}/{window.entities.Count}";

            UpdatePlaceholder(container);
        }

        public static void UpdateValues(VisualElement leftPanel, EcsDebugV2Window window)
        {
            if (window.currentTab != TabKey.Entities) return;
            var container = leftPanel.Q("left-panel") ?? leftPanel;
            var countLabel = container.Q("entity-count") as Label;
            if (countLabel != null)
                countLabel.text = $"{window.filteredEntityIds.Count}/{window.entities.Count}";
        }

        private static void BuildArchetypeFilters(VisualElement container, EcsDebugV2Window window)
        {
            var filterRow = container.Q("arch-filter-row");
            if (filterRow == null) return;

            _currentArchetypeSet.Clear();
            foreach (var e in window.entities) _currentArchetypeSet.Add(e.Archetype);

            bool archetypesChanged = _currentArchetypeSet.Count != _lastArchetypeSet.Count;
            if (!archetypesChanged)
            {
                foreach (var a in _currentArchetypeSet)
                {
                    if (!_lastArchetypeSet.Contains(a))
                    {
                        archetypesChanged = true;
                        break;
                    }
                }
            }

            if (archetypesChanged)
            {
                _lastArchetypeSet.Clear();
                foreach (var a in _currentArchetypeSet) _lastArchetypeSet.Add(a);

                filterRow.Clear();
                var allBtn = CreateFilterButton("ALL", window.archetypeFilter == null, () =>
                {
                    window.archetypeFilter = null;
                    Refresh(container, window);
                });
                filterRow.Add(allBtn);
                _archetypeSortBuffer.Clear();
                foreach (var a in _currentArchetypeSet) _archetypeSortBuffer.Add(a);
                _archetypeSortBuffer.Sort();
                foreach (var a in _archetypeSortBuffer)
                {
                    var name = a;
                    var btn = CreateFilterButton(name.ToUpper(), window.archetypeFilter == name, () =>
                    {
                        window.archetypeFilter = window.archetypeFilter == name ? null : name;
                        Refresh(container, window);
                    });
                    filterRow.Add(btn);
                }
            }
        }

        private static void UpdatePlaceholder(VisualElement container)
        {
            var searchField = container.Q("entity-search") as TextField;
            var placeholder = container.Q("search-placeholder") as Label;
            if (searchField == null || placeholder == null) return;
            var hasText = !string.IsNullOrEmpty(searchField.value);
            placeholder.style.display = hasText ? DisplayStyle.None : DisplayStyle.Flex;
            if (!hasText)
            {
                searchField.RegisterCallback<FocusEvent>(_ => placeholder.style.display = DisplayStyle.None);
                searchField.RegisterCallback<BlurEvent>(_ =>
                {
                    if (string.IsNullOrEmpty(searchField.value))
                        placeholder.style.display = DisplayStyle.Flex;
                });
            }
        }

        private static List<EntityInfo> FilterEntities(EcsDebugV2Window window)
        {
            _filteredBuffer.Clear();
            window.filteredEntityIds.Clear();
            string q = null;
            if (!string.IsNullOrEmpty(window.searchQuery))
                q = window.searchQuery.ToLower();
            foreach (var e in window.entities)
            {
                if (window.archetypeFilter != null && e.Archetype != window.archetypeFilter)
                    continue;
                if (q != null)
                {
                    if (!e.Name.ToLower().Contains(q) && !e.Id.ToString().Contains(q))
                        continue;
                }
                _filteredBuffer.Add(e);
                window.filteredEntityIds.Add(e.Id);
            }
            return _filteredBuffer;
        }

        private static Label MakeHeaderCell(string text, int width, bool flex = false)
        {
            var label = new Label(text)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Mini,
                    color = EcsDebugV2Theme.MutedText,
                    letterSpacing = 1,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };
            if (flex)
                label.style.flexGrow = 1;
            else
                label.style.width = width;
            label.style.flexShrink = flex ? 1 : 0;
            return label;
        }

        private static Label MakeDataCell(string text, Color color, int width, bool flex = false)
        {
            var label = new Label(text)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = color,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    overflow = Overflow.Hidden
                }
            };
            if (flex)
                label.style.flexGrow = 1;
            else
                label.style.width = width;
            label.style.flexShrink = flex ? 1 : 0;
            return label;
        }

        private static Button CreateFilterButton(string text, bool active, Action onClick)
        {
            var btn = new Button(onClick)
            {
                text = text,
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Mini,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 2,
                    paddingBottom = 2,
                    marginRight = 3,
                    letterSpacing = 1
                }
            };
            btn.SetupRadius(EcsDebugV2Theme.BorderRadius);
            if (active)
            {
                btn.style.color = EcsDebugV2Theme.Orange;
                btn.style.backgroundColor = EcsDebugV2Theme.OrangeA015;
                btn.SetupBorder(EcsDebugV2Theme.Orange);
            }
            else
            {
                btn.style.color = EcsDebugV2Theme.MutedText;
                btn.style.backgroundColor = EcsDebugV2Theme.Panel;
                btn.SetupBorder(EcsDebugV2Theme.PanelBorder);
            }
            return btn;
        }
    }
}
#endif

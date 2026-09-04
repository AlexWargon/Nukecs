#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
// ReSharper disable HeapView.CanAvoidClosure
// ReSharper disable HeapView.ObjectAllocation

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    using static Constant;
    public static class EntitiesTab
    {
        private static readonly List<EntityInfo> FilteredBuffer = new ();
        private static bool _suppressSelection;
        public static VisualElement Create(EcsDebugV2Window window)
        {
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
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 8,
                    paddingBottom = 8,
                    backgroundColor = EcsDebugV2Theme.PanelElevated.WithAlpha(0.45f),
                    borderBottomWidth = 1,
                    borderBottomColor = EcsDebugV2Theme.GlassBorder,
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
                pickingMode = PickingMode.Ignore,
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.MutedTextA05,
                    position = Position.Absolute,
                    left = 7,
                    top = 0,
                    bottom = 0,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };
            searchContainer.Add(placeholder);
            toolbar.Add(searchContainer);

            var countLabel = new Label("0")
            {
                name = "entity-count",
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.MutedText,
                    marginLeft = 8,
                    flexShrink = 0,
                    alignSelf = Align.FlexEnd
                }
            };
            toolbar.Add(countLabel);
            var newEntityBtn = EcsDebugV2Theme.CreateActionBtn("+ new entity", EcsDebugV2Theme.Lime, window.CreateEntity);
            toolbar.Add(newEntityBtn);
            container.Add(toolbar);

            var tableHeader = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    backgroundColor = EcsDebugV2Theme.PanelElevated.WithAlpha(0.3f),
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 5,
                    paddingBottom = 5,
                    borderBottomWidth = 1,
                    borderBottomColor = EcsDebugV2Theme.GlassBorder,
                    flexShrink = 0
                }
            };
            tableHeader.Add(MakeHeaderCell("ID", 70));
            tableHeader.Add(MakeHeaderCell("Name", 0, true));
            container.Add(tableHeader);

            var filtered = FilterEntities(window);

            var listView = new ListView(filtered, ENTITY_LIST_ITEM_HEIGHT,
                () =>
                {
                    var row = new VisualElement
                    {
                        style =
                        {
                            flexDirection = FlexDirection.Row,
                            paddingLeft = 10,
                            paddingRight = 10,
                            paddingTop = 5,
                            paddingBottom = 5,
                            borderBottomWidth = 1,
                            borderBottomColor = EcsDebugV2Theme.GlassBorder,
                            overflow = Overflow.Hidden
                        }
                    };
                    row.Add(MakeDataCell("", EcsDebugV2Theme.TypeEntity, 70));
                    row.Add(MakeDataCell("", EcsDebugV2Theme.Foreground, 0, true));
                    // Hover is suppressed (via guard) when this row is the current selection,
                    // so the amber selection fill never gets overwritten by the hover wash.
                    row.ApplyHover(() => window.selectedEntityId == (int)row.userData);
                    return row;
                },
                (ve, idx) =>
                {
                    if (idx < 0 || idx >= FilteredBuffer.Count) return;
                    var e = FilteredBuffer[idx];
                    ve.userData = e.id;
                    ve.name = $"erow-{e.id}";
                    var selected = window.selectedEntityId == e.id;
                    ve.style.backgroundColor = selected
                        ? EcsDebugV2Theme.AmberA012
                        : Color.clear;
                    var ci = 0;
                    foreach (var child in ve.Children())
                    {
                        if (child is not Label label) continue;
                        label.text = ci switch
                        {
                            0 => $"#{e.id}",
                            1 => e.name,
                            _ => label.text
                        };
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
                        window.SelectEntity(info.id);
                        break;
                    }
                }
            };
            // Double-click (or Enter) frames the entity in the SceneView, Unity-style.
            listView.onItemsChosen += objects =>
            {
                foreach (var o in objects)
                {
                    if (o is EntityInfo info)
                    {
                        EntityTransformGizmoDrawer.FrameEntity(window, info.id);
                        break;
                    }
                }
            };
            listView.makeNoneElement = () => new VisualElement();
            if (window.selectedEntityId.HasValue)
            {
                var idx = filtered.FindIndex(e => e.id == window.selectedEntityId.Value);
                if (idx >= 0)
                {
                    _suppressSelection = true;
                    listView.selectedIndex = idx;
                    _suppressSelection = false;
                }
            }

            container.Add(listView);

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
                var idx = FilteredBuffer.FindIndex(e => e.id == window.selectedEntityId.Value);
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
            var listView = container.Q<ListView>("entity-list");

            var filtered = FilterEntities(window);
            listView.itemsSource = filtered;
            listView.Rebuild();

            if (window.selectedEntityId.HasValue)
            {
                var idx = filtered.FindIndex(e => e.id == window.selectedEntityId.Value);
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
            if (container.Q("entity-count") is Label countLabel)
                countLabel.text = $"{window.filteredEntityIds.Count}/{window.entities.Count}";
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
            FilteredBuffer.Clear();
            window.filteredEntityIds.Clear();
            string q = null;
            if (!string.IsNullOrEmpty(window.searchQuery))
                q = window.searchQuery.ToLower();
            foreach (var e in window.entities)
            {
                if (q != null)
                {
                    if (!e.name.ToLower().Contains(q) && !e.id.ToString().Contains(q))
                        continue;
                }
                FilteredBuffer.Add(e);
                window.filteredEntityIds.Add(e.id);
            }
            return FilteredBuffer;
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

    }
}
#endif

#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public static class EntitiesTab
    {
        public static VisualElement Create(EcsDebugV2Window window)
        {
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
                window.SearchQuery = q;
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
            placeholder.style.color = EcsDebugV2Theme.MutedText.WithAlpha(0.5f);
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

            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "entity-scroll",
                style =
                {
                    flexGrow = 1,
                    overflow = Overflow.Hidden
                }
            };
            scroll.focusable = true;
            scroll.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (window.FilteredEntityIds.Count == 0) return;
                var currentIdx = window.FilteredEntityIds.IndexOf(window.SelectedEntityId ?? -1);

                if (evt.keyCode == KeyCode.UpArrow && currentIdx > 0)
                {
                    window.SelectEntity(window.FilteredEntityIds[currentIdx - 1]);
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.DownArrow && currentIdx < window.FilteredEntityIds.Count - 1)
                {
                    window.SelectEntity(window.FilteredEntityIds[currentIdx + 1]);
                    evt.StopPropagation();
                }
            });
            container.Add(scroll);

            Refresh(container, window);
            return container;
        }

        public static void Refresh(VisualElement container, EcsDebugV2Window window)
        {
            var filterRow = container.Q("arch-filter-row");
            if (filterRow != null)
            {
                filterRow.Clear();
                var archetypes = new HashSet<string>();
                foreach (var e in window.Entities) archetypes.Add(e.Archetype);
                var allBtn = CreateFilterButton("ALL", window.ArchetypeFilter == null, () =>
                {
                    window.ArchetypeFilter = null;
                    Refresh(container, window);
                });
                filterRow.Add(allBtn);
                foreach (var a in archetypes.OrderBy(x => x))
                {
                    var name = a;
                    var btn = CreateFilterButton(name.ToUpper(), window.ArchetypeFilter == name, () =>
                    {
                        window.ArchetypeFilter = window.ArchetypeFilter == name ? null : name;
                        Refresh(container, window);
                    });
                    filterRow.Add(btn);
                }
            }

            var scroll = container.Q("entity-scroll") as ScrollView;
            if (scroll == null) return;
            var savedOffset = scroll.scrollOffset;
            scroll.Clear();

            var filtered = FilterEntities(window);
            foreach (var e in filtered)
            {
                var row = CreateEntityRow(e, window);
                scroll.Add(row);
            }
            scroll.scrollOffset = savedOffset;

            if (container.Q("entity-count") is Label countLabel)
                countLabel.text = $"{filtered.Count}/{window.Entities.Count}";

            UpdatePlaceholder(container);
        }

        public static void UpdateValues(VisualElement leftPanel, EcsDebugV2Window window)
        {
            if (window.CurrentTab != TabKey.Entities) return;
            var container = leftPanel.Q("left-panel") ?? leftPanel;
            var countLabel = container.Q("entity-count") as Label;
            if (countLabel != null)
                countLabel.text = $"{window.FilteredEntityIds.Count}/{window.Entities.Count}";
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
            var result = new List<EntityInfo>();
            foreach (var e in window.Entities)
            {
                if (window.ArchetypeFilter != null && e.Archetype != window.ArchetypeFilter)
                    continue;
                if (!string.IsNullOrEmpty(window.SearchQuery))
                {
                    var q = window.SearchQuery.ToLower();
                    if (!e.Name.ToLower().Contains(q) && !e.Id.ToString().Contains(q))
                        continue;
                }
                result.Add(e);
            }
            window.FilteredEntityIds = result.Select(e => e.Id).ToList();
            return result;
        }

        private static VisualElement CreateEntityRow(EntityInfo e, EcsDebugV2Window window)
        {
            bool selected = window.SelectedEntityId == e.Id;
            var row = new VisualElement
            {
                name = $"erow-{e.Id}",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 4,
                    paddingBottom = 4,
                    borderBottomWidth = 1,
                    borderBottomColor = EcsDebugV2Theme.PanelBorder.WithAlpha(0.4f),
                    backgroundColor = selected
                        ? EcsDebugV2Theme.Lime.WithAlpha(0.1f)
                        : Color.clear,
                    overflow = Overflow.Hidden
                }
            };

            row.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (window.SelectedEntityId != e.Id)
                    row.style.backgroundColor = EcsDebugV2Theme.PanelElevated.WithAlpha(0.4f);
            });
            row.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (window.SelectedEntityId != e.Id)
                    row.style.backgroundColor = Color.clear;
            });
            row.RegisterCallback<ClickEvent>(_ =>
            {
                window.SelectEntity(e.Id);
            });

            row.Add(MakeDataCell($"#{e.Id}", EcsDebugV2Theme.TypeEntity, 70));
            row.Add(MakeDataCell(e.Name, EcsDebugV2Theme.Foreground, 0, true));
            row.Add(MakeDataCell(e.Archetype, EcsDebugV2Theme.Orange, 100));
            return row;
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
                btn.style.backgroundColor = EcsDebugV2Theme.Orange.WithAlpha(0.15f);
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

#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor
{
    public static class DashboardLeftSidebar
    {
        private struct EntityGroup
        {
            public string name;
            public string icon;
            public Color color;
            public Func<World, List<int>> getEntities;
        }

        private static readonly unsafe  EntityGroup[] Groups = {
            new()
            {
                name = "All",
                icon = "\u25C6",
                color = DashboardTheme.AccentPurple,
                getEntities = w =>
                {
                    var result = new List<int>();
                    var entities = w.UnsafeWorld->entitiesDens.GetAliveEntities();
                    for (var i = 0; i < entities.Length; i++)
                        result.Add(entities[i]);
                    return result;
                }
            }
        };

        public static VisualElement Create(NukecsDashboardWindow window)
        {
            var sidebar = new VisualElement
            {
                name = "left-sidebar",
                style =
                {
                    width = 240,
                    minWidth = 200,
                    flexDirection = FlexDirection.Column,
                    backgroundColor = DashboardTheme.BgPanel,
                    paddingTop = 4,
                    paddingBottom = 4
                }
            };

            var header = DashboardStyles.SectionTitle("Groups", DashboardTheme.TextPrimary);
            sidebar.Add(header);

            var content = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "sidebar-content",
                style = { flexGrow = 1 }
            };
            sidebar.Add(content);

            return sidebar;
        }

        public static unsafe void Refresh(VisualElement sidebar, NukecsDashboardWindow window)
        {
            var world = window.World;
            if (!world.IsAlive) return;

            var content = sidebar.Q<ScrollView>("sidebar-content");
            if (content == null) return;
            content.Clear();

            var totalEntities = world.UnsafeWorld->entitiesAmount;

            var allGroup = CreateGroupCard("All", "\u25C6", DashboardTheme.AccentPurple,
                totalEntities, totalEntities, window.SelectedGroup == "All",
                () => window.SelectGroup("All"), true);
            content.Add(allGroup);

            var archetypeCount = world.UnsafeWorld->archetypesList.Length;
            for (var i = 0; i < archetypeCount; i++)
            {
                var archPtr = world.UnsafeWorld->archetypesList.Ptr[i];
                ref var arch = ref archPtr.Ref;
                var entityCount = arch.count;
                if (entityCount == 0) continue;

                var archIdx = arch.index;

                var displayHash = arch.hashId;
                var color = DashboardTheme.AccentForArchetype(displayHash);

                var card = CreateGroupCard(string.Empty, $"{displayHash}", color,
                    entityCount, totalEntities,
                    window.SelectedArchetypeIndex == archIdx && window.SelectedGroup == "All",
                    () => window.SelectArchetype(archIdx), false);
                content.Add(card);
            }
        }

        private static VisualElement CreateGroupCard(
            string name, string icon, Color accentColor,
            int count, int total, bool selected, Action onClick, bool isSpecial)
        {
            var card = new Button(onClick)
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginTop = 2,
                    marginBottom = 2,
                    marginLeft = 8,
                    marginRight = 8,
                    paddingLeft = 12,
                    paddingRight = 12,
                    paddingTop = 10,
                    paddingBottom = 10,
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8,
                    borderBottomRightRadius = 8,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    backgroundColor = selected
                        ? accentColor.WithAlpha(0.12f)
                        : DashboardTheme.BgCard,
                    overflow = Overflow.Hidden,
                    position = Position.Relative
                }
            };

            if (isSpecial)
            {
                var gradientOverlay = new VisualElement
                {
                    style =
                    {
                        position = Position.Absolute,
                        left = 0, right = 0, top = 0, bottom = 0,
                        backgroundColor = DashboardTheme.AccentPurple.WithAlpha(0.06f),
                        borderTopLeftRadius = 8,
                        borderTopRightRadius = 8,
                        borderBottomLeftRadius = 8,
                        borderBottomRightRadius = 8
                    }
                };
                card.Add(gradientOverlay);
            }

            var accentBar = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = 0, top = 0, bottom = 0,
                    width = selected ? 3 : 0,
                    backgroundColor = accentColor,
                    borderTopLeftRadius = 8,
                    borderBottomLeftRadius = 8
                }
            };
            card.Add(accentBar);

            if (selected)
            {
                card.style.borderLeftWidth = 2;
                card.style.borderLeftColor = accentColor;
            }

            var iconLabel = new Label(icon)
            {
                style =
                {
                    fontSize = 12,
                    color = accentColor,
                    marginRight = 8,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            card.Add(iconLabel);

            var textCol = new VisualElement
            {
                style = { flexGrow = 1, flexDirection = FlexDirection.Column }
            };

            var nameFontSize = isSpecial ? DashboardTheme.FontSize.Body : DashboardTheme.FontSize.Small;
            var nameLabel = new Label(name.Length > 22 ? name.Substring(0, 20) + ".." : name)
            {
                style =
                {
                    fontSize = nameFontSize,
                    color = selected ? DashboardTheme.TextPrimary : DashboardTheme.TextSecondary,
                    unityFontStyleAndWeight = selected ? FontStyle.Bold : FontStyle.Normal
                }
            };
            textCol.Add(nameLabel);

            var barBg = new VisualElement
            {
                style =
                {
                    height = 4,
                    backgroundColor = DashboardTheme.ProgressBarBg,
                    borderTopLeftRadius = 2,
                    borderTopRightRadius = 2,
                    borderBottomLeftRadius = 2,
                    borderBottomRightRadius = 2,
                    marginTop = 4,
                    overflow = Overflow.Hidden
                }
            };
            var pct = total > 0 ? (float)count / total : 0;
            var fillWidth = Mathf.Clamp01(pct) * 100;
            var fillBar = new VisualElement
            {
                style =
                {
                    width = fillWidth,
                    height = 4,
                    backgroundColor = accentColor.WithAlpha(0.8f),
                    borderTopLeftRadius = 2,
                    borderBottomLeftRadius = 2
                }
            };
            barBg.Add(fillBar);
            textCol.Add(barBg);
            card.Add(textCol);

            var countBadge = DashboardStyles.PillBadge(count.ToString(),
                accentColor.WithAlpha(0.2f), accentColor, DashboardTheme.FontSize.Small);
            countBadge.style.minWidth = 30;
            card.Add(countBadge);

            card.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (!selected)
                {
                    card.style.backgroundColor = DashboardTheme.BgCardHover;
                    accentBar.style.width = 5;
                }
            });
            card.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (!selected)
                {
                    card.style.backgroundColor = DashboardTheme.BgCard;
                    accentBar.style.width = 0;
                }
            });

            return card;
        }
    }
}
#endif

#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor
{
    public static class DashboardLeftSidebar
    {
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

            var header = DashboardStyles.SectionTitle("Queries", DashboardTheme.TextPrimary);
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

            var allCard = CreateGroupCard("All", "\u25C6", DashboardTheme.AccentPurple,
                totalEntities, totalEntities, window.SelectedQueryId < 0,
                () => window.SelectQuery(-1), true);
            content.Add(allCard);

            var queries = world.UnsafeWorld->queries;
            for (var i = 0; i < queries.Length; i++)
            {
                var qPtr = queries.Ptr[i].Ptr;
                var queryId = qPtr->Id;
                var entityCount = qPtr->count;
                if (entityCount == 0) continue;

                var label = BuildQueryLabel(qPtr);
                var color = DashboardTheme.AccentForArchetype(queryId);

                var card = CreateGroupCard(label, $"Q{queryId}", color,
                    entityCount, totalEntities,
                    window.SelectedQueryId == queryId,
                    () => window.SelectQuery(queryId), false);
                content.Add(card);
            }
        }

        private static unsafe string BuildQueryLabel(QueryUnsafe* q)
        {
            var sb = new StringBuilder();
            foreach (var typeIndex in ComponentTypeMap.TypesIndexes)
            {
                if (q->HasWith(typeIndex))
                {
                    var t = ComponentTypeMap.GetType(typeIndex);
                    sb.Append('+');
                    sb.Append(t != null ? t.Name : $"T{typeIndex}");
                }
                if (q->HasNone(typeIndex))
                {
                    var t = ComponentTypeMap.GetType(typeIndex);
                    sb.Append('-');
                    sb.Append(t != null ? t.Name : $"T{typeIndex}");
                }
            }
            if (sb.Length == 0) return "Query";
            var result = sb.ToString();
            return result.Length > 22 ? result.Substring(0, 20) + ".." : result;
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

            var countLabel = new Label(count.ToString())
            {
                style =
                {
                    fontSize = DashboardTheme.FontSize.Body,
                    color = accentColor,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    minWidth = 30,
                    unityTextAlign = TextAnchor.MiddleRight
                }
            };
            card.Add(countLabel);

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

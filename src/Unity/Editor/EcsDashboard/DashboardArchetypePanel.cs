#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor
{
    public static class DashboardArchetypePanel
    {
        private const int MAX_COMPONENTS_TO_SHOW = 10;
        public static unsafe void Refresh(ScrollView container, NukecsDashboardWindow window)
        {
            var world = window.World;
            if (!world.IsAlive) return;

            container.Clear();

            if (window.SelectedQueryId >= 0)
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

                if (selectedQuery != null)
                {
                    for (var ai = 0; ai < selectedQuery->matchingArchetypes.length; ai++)
                    {
                        var archIdx = selectedQuery->matchingArchetypes.Ptr[ai];
                        ref var arch = ref world.UnsafeWorld->archetypesList.Ptr[archIdx].Ref;
                        if (arch.count == 0) continue;
                        container.Add(CreateArchetypeCard(ref arch, world, window));
                    }
                }
            }
            else
            {
                var archetypeCount = world.UnsafeWorld->archetypesList.Length;
                for (var i = 0; i < archetypeCount; i++)
                {
                    var archPtr = world.UnsafeWorld->archetypesList.Ptr[i];
                    ref var arch = ref archPtr.Ref;
                    if (arch.count == 0) continue;

                    var card = CreateArchetypeCard(ref arch, world, window);
                    container.Add(card);
                }
            }
        }

        private static VisualElement CreateArchetypeCard(
            ref ArchetypeUnsafe arch, World world, NukecsDashboardWindow window)
        {
            var borderColor = DashboardTheme.AccentForArchetype(arch.hashId);
            var selected = window.SelectedArchetypeIndex == arch.index;

            var card = NukecsDashboardWindow.CreateGlowCard(DashboardTheme.Separator, 12);
            card.style.width = 200;
            card.style.height = 120;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 6;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.flexDirection = FlexDirection.Column;

            if (selected)
            {
                card.style.borderLeftWidth = 2;
                card.style.borderLeftColor = borderColor;
            }

            var archIndex = arch.index;
            card.RegisterCallback<MouseUpEvent>(_ =>
            {
                window.SelectArchetype(archIndex);
            });
            card.RegisterCallback<MouseEnterEvent>(_ =>
            {
                card.style.backgroundColor = DashboardTheme.BgCardHover;
            });
            card.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                card.style.backgroundColor = DashboardTheme.BgCard;
            });

            var headerRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center }
            };

            var idLabel = new Label($"#{arch.index}")
            {
                style =
                {
                    fontSize = DashboardTheme.FontSize.Small,
                    color = borderColor,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginRight = 4
                }
            };
            headerRow.Add(idLabel);

            var countLabel = new Label($"{arch.count}")
            {
                style =
                {
                    fontSize = DashboardTheme.FontSize.Small,
                    color = DashboardTheme.TextSecondary,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginLeft = 4
                }
            };
            headerRow.Add(countLabel);
            card.Add(headerRow);

            var chipsContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    marginTop = 4,
                    flexGrow = 1,
                    overflow = Overflow.Hidden
                }
            };

            var chipCount = 0;
            foreach (var typeIndex in arch.types)
            {
                if (chipCount >= MAX_COMPONENTS_TO_SHOW) break;
                var t = ComponentTypeMap.GetType(typeIndex);
                var typeName = t != null ? t.Name : $"T{typeIndex}";
                var chipColor = DashboardTheme.AccentForType(typeName);

                var chip = new Label(typeName.Length > 8 ? typeName.Substring(0, 7) + ".." : typeName)
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
                chipsContainer.Add(chip);
                chipCount++;
            }

            var remainingLabel = new Label();
            var totalTypes = 0;
            foreach (var _ in arch.types) totalTypes++;
            if (totalTypes > 5)
            {
                remainingLabel.text = $"+{totalTypes - 5}";
                remainingLabel.style.fontSize = DashboardTheme.FontSize.Micro;
                remainingLabel.style.color = DashboardTheme.TextSecondary;
                remainingLabel.style.marginTop = 1;
                chipsContainer.Add(remainingLabel);
            }

            card.Add(chipsContainer);

            var occupancyBg = new VisualElement
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
                    overflow = Overflow.Hidden,
                    position = Position.Relative
                }
            };
            var capacity = arch.capacity > 0 ? arch.capacity : 1;
            var fillPct = Mathf.Clamp01((float)arch.count / capacity) * 100;
            var fillColor = fillPct > 80 ? DashboardTheme.ProgressBarCritical
                : fillPct > 50 ? DashboardTheme.ProgressBarWarn
                : DashboardTheme.ProgressBarFill;
            var fillBar = new VisualElement
            {
                style =
                {
                    width = fillPct,
                    height = 4,
                    backgroundColor = fillColor,
                    borderTopLeftRadius = 2,
                    borderBottomLeftRadius = 2
                }
            };
            occupancyBg.Add(fillBar);

            var brightTip = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = 0, top = 0, bottom = 0,
                    width = 1,
                    backgroundColor = Color.white.WithAlpha(0.4f)
                }
            };
            occupancyBg.Add(brightTip);

            card.Add(occupancyBg);

            card.style.marginRight = 8;
            return card;
        }
    }
}
#endif

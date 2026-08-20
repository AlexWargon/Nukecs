#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System.Collections.Generic;
using Unity.Profiling;
using Unity.Profiling.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor
{
    public static class DashboardBottomPanel
    {
        public static VisualElement Create(NukecsDashboardWindow window)
        {
            var bar = new VisualElement
            {
                name = "bottom-bar",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    backgroundColor = DashboardTheme.BgPanel,
                    paddingTop = 6,
                    paddingBottom = 6,
                    paddingLeft = 12,
                    paddingRight = 12,
                    alignItems = Align.Center,
                    borderTopWidth = 1,
                    borderTopColor = DashboardTheme.Separator,
                    minHeight = 40
                }
            };

            return bar;
        }

        public static void Refresh(VisualElement bar, NukecsDashboardWindow window)
        {
            var world = window.World;
            if (!world.IsAlive) return;

            bar.Clear();

            DrawMemorySection(bar, world);
            DrawSeparator(bar);
            DrawStatsSection(bar, world);
            DrawSeparator(bar);
            DrawSystemsSection(bar, window);
        }

        private static unsafe void DrawMemorySection(VisualElement bar, World world)
        {
            long totalSize = 0;
            long memUsed = 0;
            try
            {
                totalSize = world.UnsafeWorld->AllocatorHandler.AllocatorWrapper.Allocator.TotalSize;
                memUsed = world.UnsafeWorld->AllocatorHandler.AllocatorWrapper.Allocator.MemoryUsed;
            }
            catch { return; }

            var memLabel = DashboardStyles.PillBadge("MEM",
                DashboardTheme.AccentCyan.WithAlpha(0.15f), DashboardTheme.TextSecondary,
                DashboardTheme.FontSize.Micro);
            memLabel.style.marginRight = 6;
            bar.Add(memLabel);

            var barBg = new VisualElement
            {
                style =
                {
                    width = 120,
                    height = 10,
                    backgroundColor = DashboardTheme.ProgressBarBg,
                    borderTopLeftRadius = 5,
                    borderTopRightRadius = 5,
                    borderBottomLeftRadius = 5,
                    borderBottomRightRadius = 5,
                    overflow = Overflow.Hidden,
                    marginRight = 6,
                    position = Position.Relative
                }
            };

            var pct = totalSize > 0 ? (float)memUsed / totalSize : 0;
            var fillWidth = Mathf.Clamp01(pct) * 120;
            var fillColor = pct > 0.9f ? DashboardTheme.ProgressBarCritical
                : pct > 0.7f ? DashboardTheme.ProgressBarWarn
                : DashboardTheme.ProgressBarFill;
            var fillBar = new VisualElement
            {
                style =
                {
                    width = fillWidth,
                    height = 10,
                    backgroundColor = fillColor,
                    borderTopLeftRadius = 5,
                    borderBottomLeftRadius = 5
                }
            };
            barBg.Add(fillBar);

            var brightOverlay = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = 0, top = 0, bottom = 0,
                    width = Mathf.Min(fillWidth, 30),
                    backgroundColor = Color.white.WithAlpha(0.1f),
                    borderTopLeftRadius = 5,
                    borderBottomLeftRadius = 5
                }
            };
            barBg.Add(brightOverlay);

            bar.Add(barBg);

            var valueLabel = new Label($"{DashboardTheme.FormatBytes(memUsed)} / {DashboardTheme.FormatBytes(totalSize)}")
            {
                style =
                {
                    fontSize = DashboardTheme.FontSize.Small,
                    color = DashboardTheme.TextSecondary,
                    marginRight = 4
                }
            };
            bar.Add(valueLabel);

            var pctLabel = new Label($"{pct * 100:F1}%")
            {
                style =
                {
                    fontSize = DashboardTheme.FontSize.Small,
                    color = fillColor,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginRight = 8
                }
            };
            bar.Add(pctLabel);
        }

        private static void DrawSeparator(VisualElement bar)
        {
            var sep = DashboardStyles.NeonSeparator(DashboardTheme.Separator.WithAlpha(0.3f));
            sep.style.marginLeft = 8;
            sep.style.marginRight = 8;
            bar.Add(sep);
        }

        private static unsafe void DrawStatsSection(VisualElement bar, World world)
        {
            var archetypesCount = world.UnsafeWorld->archetypesList.Length;
            var entitiesCount = world.UnsafeWorld->entitiesAmount;
            var queriesCount = world.UnsafeWorld->queries.Length;
            var poolsCount = world.UnsafeWorld->poolsCount;

            AddStatBadge(bar, $"{entitiesCount}", "ENT", DashboardTheme.AccentGreen);
            AddStatBadge(bar, $"{archetypesCount}", "ARCH", DashboardTheme.AccentPurple);
            AddStatBadge(bar, $"{queriesCount}", "QRY", DashboardTheme.AccentCyan);
            AddStatBadge(bar, $"{poolsCount}", "POOL", DashboardTheme.AccentOrange);
        }

        private static void AddStatBadge(VisualElement parent, string value, string label, Color color)
        {
            var badge = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    backgroundColor = color.WithAlpha(0.12f),
                    paddingTop = 3,
                    paddingBottom = 3,
                    paddingLeft = 8,
                    paddingRight = 8,
                    borderTopLeftRadius = 10,
                    borderTopRightRadius = 10,
                    borderBottomLeftRadius = 10,
                    borderBottomRightRadius = 10,
                    marginRight = 4
                }
            };

            var valueLabel = new Label(value)
            {
                style =
                {
                    fontSize = 11,
                    color = color,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginRight = 3
                }
            };
            badge.Add(valueLabel);

            var lbl = new Label(label)
            {
                style =
                {
                    fontSize = DashboardTheme.FontSize.Micro,
                    color = DashboardTheme.TextSecondary
                }
            };
            badge.Add(lbl);

            parent.Add(badge);
        }

        private static void DrawSystemsSection(VisualElement bar, NukecsDashboardWindow window)
        {
            var spacer = new VisualElement { style = { flexGrow = 1 } };
            bar.Add(spacer);

            var systems = WorldSystems.GetAll(window.SelectedWorldId);
            if (systems == null || systems.Count == 0) return;

            var sysLabel = DashboardStyles.PillBadge("SYSTEMS",
                DashboardTheme.AccentCyan.WithAlpha(0.12f), DashboardTheme.TextSecondary,
                DashboardTheme.FontSize.Micro);
            sysLabel.style.marginRight = 6;
            bar.Add(sysLabel);

            foreach (var sys in systems)
            {
                AddSystemBadges(bar, sys);
            }
        }

        private static void AddSystemBadges(VisualElement parent, Systems systems)
        {
            
            var allRunners = new List<ISystemRunner>();
            allRunners.AddRange(systems.onStart);
            allRunners.AddRange(systems.onUpdate);
            allRunners.AddRange(systems.onFixedUpdate);
            allRunners.AddRange(systems.onDestroy);

            var maxDisplay = 12;
            var displayCount = Mathf.Min(allRunners.Count, maxDisplay);

            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    alignItems = Align.Center
                }
            };

            for (var i = 0; i < displayCount; i++)
            {
                var runner = allRunners[i];
                var name = runner.Name;
                if (name.Length > 14)
                    name = name.Substring(0, 12) + "..";

                var dot = DashboardStyles.GlowDot(DashboardTheme.AccentCyan, 6);
                dot.style.marginRight = 3;
                container.Add(dot);

                var chip = new Label(name)
                {
                    style =
                    {
                        fontSize = DashboardTheme.FontSize.Small,
                        color = DashboardTheme.AccentCyan,
                        backgroundColor = DashboardTheme.AccentCyan.WithAlpha(0.08f),
                        paddingTop = 2,
                        paddingBottom = 2,
                        paddingLeft = 6,
                        paddingRight = 6,
                        borderTopLeftRadius = 4,
                        borderTopRightRadius = 4,
                        borderBottomLeftRadius = 4,
                        borderBottomRightRadius = 4,
                        marginRight = 3,
                        marginTop = 1
                    }
                };
                container.Add(chip);
            }

            if (allRunners.Count > maxDisplay)
            {
                var moreLabel = new Label($"+{allRunners.Count - maxDisplay}")
                {
                    style =
                    {
                        fontSize = DashboardTheme.FontSize.Small,
                        color = DashboardTheme.TextSecondary,
                        marginTop = 1
                    }
                };
                container.Add(moreLabel);
            }

            parent.Add(container);
        }
    }
}
#endif

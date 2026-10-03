#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor
{
    public static class DashboardTopBar
    {
        public static VisualElement Create(NukecsDashboardWindow window)
        {
            var bar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    backgroundColor = DashboardTheme.BgPanel,
                    paddingTop = 6,
                    paddingBottom = 6,
                    paddingLeft = 12,
                    paddingRight = 12,
                    alignItems = Align.Center,
                    flexShrink = 0,
                    minHeight = 38,
                    borderBottomWidth = 1,
                    borderBottomColor = DashboardTheme.Separator
                }
            };

            var worldBtn = new Button(() =>
            {
                var menu = new GenericMenu();
                for (var i = 0; i < 4; i++)
                {
                    var w = World.Get(i);
                    if (!w.IsAlive) continue;
                    var id = i;
                    var label = $"World {id}";
                    menu.AddItem(new GUIContent(label), id == window.SelectedWorldId,
                        () => window.SetWorld(id));
                }
                menu.ShowAsContext();
            })
            {
                text = $"World {window.SelectedWorldId}",
                name = "world-btn",
                style =
                {
                    fontSize = 10,
                    borderTopLeftRadius = 14,
                    borderTopRightRadius = 14,
                    borderBottomLeftRadius = 14,
                    borderBottomRightRadius = 14,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderTopColor = DashboardTheme.Separator,
                    borderBottomColor = DashboardTheme.Separator,
                    borderLeftColor = DashboardTheme.Separator,
                    borderRightColor = DashboardTheme.Separator,
                    backgroundColor = DashboardTheme.BgCard,
                    color = DashboardTheme.TextPrimary,
                    paddingTop = 3,
                    paddingBottom = 3,
                    paddingLeft = 10,
                    paddingRight = 10,
                    marginRight = 12
                }
            };
            bar.Add(worldBtn);

            AddSeparator(bar);

            AddStatBadge(bar, "0", "ENT", DashboardTheme.AccentGreen, "entity-badge");
            AddStatBadge(bar, "0", "ARCH", DashboardTheme.AccentPurple, "arch-badge");
            AddStatBadge(bar, "0", "QRY", DashboardTheme.AccentCyan, "query-badge");

            AddSeparator(bar);

            var memLabel = DashboardStyles.PillBadge("MEM",
                DashboardTheme.AccentCyan.WithAlpha(0.15f), DashboardTheme.TextSecondary,
                DashboardTheme.FontSize.Micro);
            memLabel.style.marginRight = 6;
            bar.Add(memLabel);

            var barBg = new VisualElement
            {
                name = "mem-bar-bg",
                style =
                {
                    width = 100,
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

            var fillBar = new VisualElement
            {
                name = "mem-bar-fill",
                style =
                {
                    width = 0,
                    height = 10,
                    backgroundColor = DashboardTheme.ProgressBarFill,
                    borderTopLeftRadius = 5,
                    borderBottomLeftRadius = 5
                }
            };
            barBg.Add(fillBar);

            var brightOverlay = new VisualElement
            {
                name = "mem-bar-overlay",
                style =
                {
                    position = Position.Absolute,
                    left = 0, top = 0, bottom = 0,
                    width = 0,
                    backgroundColor = Color.white.WithAlpha(0.1f),
                    borderTopLeftRadius = 5,
                    borderBottomLeftRadius = 5
                }
            };
            barBg.Add(brightOverlay);

            bar.Add(barBg);

            var memValue = new Label("-- / --")
            {
                name = "mem-value",
                style =
                {
                    fontSize = DashboardTheme.FontSize.Small,
                    color = DashboardTheme.TextSecondary,
                    marginRight = 4
                }
            };
            bar.Add(memValue);

            var memPct = new Label("--")
            {
                name = "mem-pct",
                style =
                {
                    fontSize = DashboardTheme.FontSize.Small,
                    color = DashboardTheme.ProgressBarFill,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginRight = 8
                }
            };
            bar.Add(memPct);

            var spacer = new VisualElement { style = { flexGrow = 1 } };
            bar.Add(spacer);

            var reloadBtn = new Button(() =>
            {
                DashboardTheme.Reload();
                window.CreateGUI();
            })
            {
                text = "Reload Theme",
                style =
                {
                    fontSize = 9,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    backgroundColor = DashboardTheme.BgCard,
                    color = DashboardTheme.TextSecondary,
                    paddingTop = 2,
                    paddingBottom = 2,
                    paddingLeft = 6,
                    paddingRight = 6,
                    marginRight = 8
                }
            };
            reloadBtn.RegisterCallback<MouseEnterEvent>(_ =>
            {
                reloadBtn.style.color = DashboardTheme.AccentCyan;
            });
            reloadBtn.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                reloadBtn.style.color = DashboardTheme.TextSecondary;
            });
            bar.Add(reloadBtn);

            var brandingContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    alignItems = Align.FlexEnd
                }
            };

            var versionLabel = new Label("NUKECS")
            {
                style =
                {
                    fontSize = 13,
                    color = DashboardTheme.TextPrimary,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    letterSpacing = 3
                }
            };
            brandingContainer.Add(versionLabel);

            var brandLine = new VisualElement
            {
                style =
                {
                    height = 1,
                    backgroundColor = DashboardTheme.Separator,
                    marginTop = 1
                }
            };
            brandingContainer.Add(brandLine);
            bar.Add(brandingContainer);

            var wrapper = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    flexShrink = 0
                }
            };
            wrapper.Add(bar);
            return wrapper;
        }

        public static unsafe void Update(VisualElement topBar, NukecsDashboardWindow window)
        {
            var world = window.World;
            if (!world.IsAlive) return;

            var entityBadge = topBar.Q<Label>("entity-badge-value");
            if (entityBadge != null)
            {
                var count = world.UnsafeWorld->entitiesAmount;
                entityBadge.text = $"{count}";
            }

            var archBadge = topBar.Q<Label>("arch-badge-value");
            if (archBadge != null)
            {
                var archetypes = world.UnsafeWorld->archetypesList.Length;
                archBadge.text = $"{archetypes}";
            }

            var queryBadge = topBar.Q<Label>("query-badge-value");
            if (queryBadge != null)
            {
                var queries = world.UnsafeWorld->queries.Length;
                queryBadge.text = $"{queries}";
            }

            long totalSize = 0;
            long memUsed = 0;
            try
            {
                totalSize = world.UnsafeWorld->AllocatorHandler.AllocatorWrapper.Allocator.TotalSize;
                memUsed = world.UnsafeWorld->AllocatorHandler.AllocatorWrapper.Allocator.MemoryUsed;
            }
            catch { return; }

            var pct = totalSize > 0 ? (float)memUsed / totalSize : 0;
            var fillWidth = Mathf.Clamp01(pct) * 100;
            var fillColor = pct > 0.9f ? DashboardTheme.ProgressBarCritical
                : pct > 0.7f ? DashboardTheme.ProgressBarWarn
                : DashboardTheme.ProgressBarFill;

            var fillBar = topBar.Q<VisualElement>("mem-bar-fill");
            if (fillBar != null)
            {
                fillBar.style.width = fillWidth;
                fillBar.style.backgroundColor = fillColor;
            }

            var overlay = topBar.Q<VisualElement>("mem-bar-overlay");
            if (overlay != null)
            {
                overlay.style.width = Mathf.Min(fillWidth, 25);
            }

            var memValue = topBar.Q<Label>("mem-value");
            if (memValue != null)
            {
                memValue.text = $"{DashboardTheme.FormatBytes(memUsed)} / {DashboardTheme.FormatBytes(totalSize)}";
            }

            var memPct = topBar.Q<Label>("mem-pct");
            if (memPct != null)
            {
                memPct.text = $"{pct * 100:F1}%";
                memPct.style.color = fillColor;
            }
        }

        private static void AddSeparator(VisualElement parent)
        {
            var sep = new VisualElement
            {
                style =
                {
                    width = 1,
                    height = 18,
                    backgroundColor = DashboardTheme.Separator,
                    marginRight = 12
                }
            };
            parent.Add(sep);
        }

        private static void AddStatBadge(VisualElement parent, string value, string label, Color color, string badgeName)
        {
            var badge = new VisualElement
            {
                name = badgeName,
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
                name = badgeName + "-value",
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
    }
}
#endif

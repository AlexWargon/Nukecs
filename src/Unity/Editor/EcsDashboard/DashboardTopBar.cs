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
                    minHeight = 38
                }
            };

            var liveDotWrapper = DashboardStyles.GlowDot(
                EditorApplication.isPlaying ? DashboardTheme.AccentGreen : DashboardTheme.TextSecondary, 10);
            liveDotWrapper.name = "live-dot-wrapper";
            liveDotWrapper.style.marginRight = 6;
            bar.Add(liveDotWrapper);

            var liveShadow = new Label(EditorApplication.isPlaying ? "LIVE" : "STOPPED")
            {
                style =
                {
                    fontSize = 10,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = EditorApplication.isPlaying
                        ? DashboardTheme.AccentGreen.WithAlpha(0.3f)
                        : DashboardTheme.TextSecondary.WithAlpha(0.3f),
                    position = Position.Absolute,
                    left = 1,
                    top = 1
                }
            };

            var liveLabel = new Label(EditorApplication.isPlaying ? "LIVE" : "STOPPED")
            {
                name = "live-label",
                style =
                {
                    fontSize = 10,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = EditorApplication.isPlaying
                        ? DashboardTheme.AccentGreen
                        : DashboardTheme.TextSecondary,
                    marginRight = 12,
                    position = Position.Relative
                }
            };
            var liveWrapper = new VisualElement
            {
                style =
                {
                    position = Position.Relative,
                    overflow = Overflow.Visible
                }
            };
            liveWrapper.Add(liveShadow);
            liveWrapper.Add(liveLabel);
            bar.Add(liveWrapper);

            var pauseBtn = new VisualElement
            {
                name = "pause-btn",
                style =
                {
                    fontSize = 12,
                    width = 30,
                    height = 24,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    backgroundColor = DashboardTheme.BgCard,
                    marginRight = 8,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };
            var pauseLabel = new Label(window.Paused ? "\u25B6" : "\u275A\u275A")
            {
                name = "pause-label",
                style =
                {
                    fontSize = 10,
                    color = DashboardTheme.TextPrimary,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            pauseBtn.Add(pauseLabel);
            pauseBtn.RegisterCallback<MouseUpEvent>(_ => window.TogglePause());
            pauseBtn.RegisterCallback<MouseEnterEvent>(_ =>
            {
                pauseBtn.style.backgroundColor = DashboardTheme.BgCardHover;
                pauseBtn.style.borderBottomWidth = 1;
                pauseBtn.style.borderBottomColor = DashboardTheme.AccentPurple.WithAlpha(0.5f);
            });
            pauseBtn.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                pauseBtn.style.backgroundColor = DashboardTheme.BgCard;
                pauseBtn.style.borderBottomWidth = 0;
            });
            bar.Add(pauseBtn);

            var timeLabel = new Label("0.000s")
            {
                name = "time-label",
                style =
                {
                    fontSize = 11,
                    color = DashboardTheme.AccentCyan,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginRight = 16,
                    minWidth = 65
                }
            };
            bar.Add(timeLabel);

            var separator = new VisualElement
            {
                style =
                {
                    width = 1,
                    height = 18,
                    backgroundColor = DashboardTheme.Separator,
                    marginRight = 12
                }
            };
            bar.Add(separator);

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
                    borderTopColor = DashboardTheme.AccentPurple.WithAlpha(0.3f),
                    borderBottomColor = DashboardTheme.AccentPurple.WithAlpha(0.3f),
                    borderLeftColor = DashboardTheme.AccentPurple.WithAlpha(0.3f),
                    borderRightColor = DashboardTheme.AccentPurple.WithAlpha(0.3f),
                    backgroundColor = DashboardTheme.BgCard,
                    color = DashboardTheme.AccentPurple,
                    paddingTop = 3,
                    paddingBottom = 3,
                    paddingLeft = 10,
                    paddingRight = 10,
                    marginRight = 12
                }
            };
            bar.Add(worldBtn);

            var entityBadge = DashboardStyles.PillBadge("0 Entities",
                DashboardTheme.AccentPurple.WithAlpha(0.25f), DashboardTheme.AccentPurple, 10);
            entityBadge.name = "entity-badge";
            bar.Add(entityBadge);

            var systemBadge = DashboardStyles.PillBadge("0 Systems",
                DashboardTheme.AccentCyan.WithAlpha(0.25f), DashboardTheme.AccentCyan, 10);
            systemBadge.name = "system-badge";
            systemBadge.style.marginLeft = 6;
            bar.Add(systemBadge);

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

            var brandGlow = new VisualElement
            {
                style =
                {
                    height = 2,
                    backgroundColor = DashboardTheme.AccentPurple.WithAlpha(0.4f),
                    marginTop = 1
                }
            };
            brandingContainer.Add(brandGlow);
            bar.Add(brandingContainer);

            var gradientLine = DashboardStyles.CreateGradientLine(2);

            var wrapper = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    flexShrink = 0
                }
            };
            wrapper.Add(bar);
            wrapper.Add(gradientLine);
            return wrapper;
        }

        public static unsafe void Update(VisualElement topBar, NukecsDashboardWindow window)
        {
            var world = window.World;
            if (!world.IsAlive) return;

            var liveDotWrapper = topBar.Q<VisualElement>("live-dot-wrapper");
            if (liveDotWrapper != null)
            {
                var coreDot = liveDotWrapper.Q<VisualElement>("glow-dot-core");
                if (coreDot != null)
                    coreDot.style.backgroundColor = EditorApplication.isPlaying
                        ? DashboardTheme.AccentGreen
                        : DashboardTheme.TextSecondary;
            }

            var liveLabel = topBar.Q<Label>("live-label");
            if (liveLabel != null)
            {
                liveLabel.text = EditorApplication.isPlaying ? "LIVE" : "STOPPED";
                liveLabel.style.color = EditorApplication.isPlaying
                    ? DashboardTheme.AccentGreen
                    : DashboardTheme.TextSecondary;
            }

            var timeLabel = topBar.Q<Label>("time-label");
            if (timeLabel != null && world.UnsafeWorld != null)
            {
                var t = world.UnsafeWorld->timeData.Time;
                timeLabel.text = $"{t:F2}s";
            }

            var entityBadge = topBar.Q<Label>("entity-badge");
            if (entityBadge != null)
            {
                var count = world.UnsafeWorld->entitiesAmount;
                entityBadge.text = $"{count} Entities";
            }

            var systemBadge = topBar.Q<Label>("system-badge");
            if (systemBadge != null)
            {
                var systems = WorldSystems.GetAll(window.SelectedWorldId);
                var sysCount = 0;
                foreach (var s in systems)
                    sysCount += s.runners.Count + s.fixedRunners.Count + s.mtRunners.Count + s.mtFixedRunners.Count;
                systemBadge.text = $"{sysCount} Systems";
            }

            var pauseLabel = topBar.Q<Label>("pause-label");
            if (pauseLabel != null)
                pauseLabel.text = window.Paused ? "\u25B6" : "\u275A\u275A";
        }
    }
}
#endif

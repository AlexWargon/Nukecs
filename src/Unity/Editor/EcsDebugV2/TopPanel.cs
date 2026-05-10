#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public static class TopPanel
    {
        private static int _lastTick = -1;
        private static int _lastEntCount = -1;
        private static int _lastArchCount = -1;
        private static int _lastQCount = -1;
        private static bool _lastPaused = true;

        public static VisualElement Create(EcsDebugV2Window window)
        {
            var header = EcsDebugV2Theme.CreateHeaderRow();
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.justifyContent = Justify.SpaceBetween;

            var leftGroup = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            var dot = EcsDebugV2Theme.CreateGlowDot(EcsDebugV2Theme.Lime, 10);
            dot.name = "pulse-dot";
            dot.style.marginRight = 8;
            float dotOpacity = 1f;
            dot.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                dot.schedule.Execute(() =>
                {
                    if (dot.panel == null) return;
                    dotOpacity = dotOpacity > 0.5f ? 0.35f : 1f;
                    dot.style.opacity = dotOpacity;
                }).Every(800);
            });
            leftGroup.Add(dot);

            var worldLabel = new Label("WORLD")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.MutedText,
                    letterSpacing = 2,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    marginRight = 6
                }
            };
            leftGroup.Add(worldLabel);

            var worldId = new Label(window.provider.WorldInfo.Name)
            {
                name = "world-id",
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.Lime,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            worldId.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (evt.button != 0) return;
                var info = window.provider.WorldInfo;
                if (info.WorldNames == null || info.WorldNames.Length <= 1) return;
                var menu = new GenericMenu();
                for (var i = 0; i < info.WorldNames.Length; i++)
                {
                    var slot = info.WorldSlots != null && i < info.WorldSlots.Length
                        ? info.WorldSlots[i]
                        : i;
                    var capturedSlot = slot;
                    var isCurrent = info.WorldNames[i] == info.Name;
                    menu.AddItem(new GUIContent(info.WorldNames[i]), isCurrent,
                        () => window.SwitchToWorld(capturedSlot));
                }
                menu.ShowAsContext();
                evt.StopPropagation();
            });
            leftGroup.Add(worldId);

            var tickLabel = new Label("t=0")
            {
                name = "tick-label",
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.MutedText,
                    marginLeft = 12
                }
            };
            leftGroup.Add(tickLabel);
            header.Add(leftGroup);

            var statsGroup = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };
            statsGroup.Add(CreateStatBadge("\u25C9", "ENT", window.entities.Count, EcsDebugV2Theme.Lime));
            statsGroup.Add(CreateStatBadge("\u25A0", "ARCH", window.archetypes.Count, EcsDebugV2Theme.Orange));
            statsGroup.Add(CreateStatBadge("\u2315", "Q", window.queries.Count, EcsDebugV2Theme.Yellow));
            statsGroup.Add(CreateStatBadge("\u2630", "SYS", window.systemCount, EcsDebugV2Theme.TypeBool));
            header.Add(statsGroup);

            var themeBtn = new Button()
            {
                name = "theme-btn",
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.MutedText,
                    backgroundColor = EcsDebugV2Theme.Panel,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 4,
                    paddingBottom = 4,
                    letterSpacing = 1
                }
            };
            themeBtn.SetupRadius(EcsDebugV2Theme.BorderRadius);
            themeBtn.SetupBorder(EcsDebugV2Theme.PanelBorder);
            UpdateThemeLabel(themeBtn);
            themeBtn.clicked += () =>
            {
                var menu = new GenericMenu();
                foreach (var name in EcsDebugV2Theme.AvailableThemes)
                {
                    var captured = name;
                    bool current = name == EcsDebugV2Theme.CurrentThemeName;
                    menu.AddItem(new GUIContent(captured), current, () =>
                    {
                        EcsDebugV2Theme.SwitchTheme(captured);
                        window.CreateGUI();
                    });
                }
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Reload Current"), false, () =>
                {
                    EcsDebugV2Theme.ReloadCurrentTheme();
                    window.CreateGUI();
                });
                menu.ShowAsContext();
            };
            header.Add(themeBtn);

            var pauseBtn = new Button(() => window.TogglePause())
            {
                name = "pause-btn",
                text = "\u23F8 Pause",
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.Foreground,
                    backgroundColor = EcsDebugV2Theme.Panel,
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 4,
                    paddingBottom = 4,
                    letterSpacing = 1
                }
            };
            pauseBtn.SetupRadius(EcsDebugV2Theme.BorderRadius);
            pauseBtn.SetupBorder(EcsDebugV2Theme.PanelBorder);
            header.Add(pauseBtn);
            return header;
        }

        public static void Update(VisualElement topPanel, EcsDebugV2Window window)
        {
            if (window.tick != _lastTick)
            {
                _lastTick = window.tick;
                var tickLabel = topPanel.Q("tick-label") as Label;
                if (tickLabel != null)
                    tickLabel.text = $"t={window.tick}";
            }

            var worldId = topPanel.Q("world-id") as Label;
            if (worldId != null)
                worldId.text = window.provider.WorldInfo.Name;

            if (window.paused != _lastPaused)
            {
                _lastPaused = window.paused;
                var pauseBtn = topPanel.Q("pause-btn") as Button;
                if (pauseBtn != null)
                {
                    pauseBtn.text = window.paused ? "\u25B6 Resume" : "\u23F8 Pause";
                    pauseBtn.style.color = window.paused ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.Orange;
                    pauseBtn.style.backgroundColor = window.paused
                        ? EcsDebugV2Theme.OrangeA015
                        : EcsDebugV2Theme.Panel;
                }

                var pulseDot = topPanel.Q("pulse-dot");
                if (pulseDot != null)
                    pulseDot.style.backgroundColor = window.paused
                        ? EcsDebugV2Theme.Orange
                        : EcsDebugV2Theme.Lime;
            }

            if (window.entities.Count != _lastEntCount)
            {
                _lastEntCount = window.entities.Count;
                var stats = topPanel.Q("stats-ent") as Label;
                if (stats != null) stats.text = _lastEntCount.ToString();
            }
            if (window.archetypes.Count != _lastArchCount)
            {
                _lastArchCount = window.archetypes.Count;
                var archStat = topPanel.Q("stats-arch") as Label;
                if (archStat != null) archStat.text = _lastArchCount.ToString();
            }
            if (window.queries.Count != _lastQCount)
            {
                _lastQCount = window.queries.Count;
                var qStat = topPanel.Q("stats-q") as Label;
                if (qStat != null) qStat.text = _lastQCount.ToString();
            }

            var themeBtn = topPanel.Q("theme-btn") as Button;
            if (themeBtn != null) UpdateThemeLabel(themeBtn);
        }

        private static void UpdateThemeLabel(Button btn)
        {
            btn.text = $"Theme: {EcsDebugV2Theme.CurrentThemeName} \u25BE";
        }

        private static VisualElement CreateStatBadge(string icon, string label, int value, Color color)
        {
            var badge = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 4,
                    paddingBottom = 4,
                    marginRight = 6,
                    backgroundColor = EcsDebugV2Theme.Panel
                }
            };
            badge.SetupRadius(EcsDebugV2Theme.BorderRadius);
            badge.SetupBorder(EcsDebugV2Theme.PanelBorder);

            var iconLabel = new Label(icon)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = color,
                    marginRight = 4
                }
            };
            badge.Add(iconLabel);

            var labelEl = new Label(label)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Mini,
                    color = EcsDebugV2Theme.MutedText,
                    letterSpacing = 1,
                    marginRight = 4
                }
            };
            badge.Add(labelEl);

            var valueLabel = new Label(value.ToString())
            {
                name = "stats-" + label.ToLower(),
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = color,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            badge.Add(valueLabel);
            return badge;
        }
    }
}
#endif

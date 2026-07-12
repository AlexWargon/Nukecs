#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
// ReSharper disable HeapView.CanAvoidClosure

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
            header.style.paddingLeft = 14;
            header.style.paddingRight = 14;
            header.style.justifyContent = Justify.SpaceBetween;
            // Soft glass underline instead of a solid accent.
            header.style.borderBottomColor = EcsDebugV2Theme.GlassBorder;

            var leftGroup = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            var dot = EcsDebugV2Theme.CreateGlowDot(EcsDebugV2Theme.Amber, 9);
            dot.name = "pulse-dot";
            dot.style.marginRight = 10;
            var dotOpacity = 1f;
            dot.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                dot.schedule.Execute(() =>
                {
                    if (dot.panel == null) return;
                    // Gentler pulse than the previous hard 1.0 ↔ 0.35 flip.
                    dotOpacity = dotOpacity > 0.6f ? 0.55f : 1f;
                    dot.style.opacity = dotOpacity;
                }).Every(900);
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
                    marginRight = 7
                }
            };
            leftGroup.Add(worldLabel);

            var worldId = new Label(window.provider.WorldInfo.name)
            {
                name = "world-id",
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.Amber,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            worldId.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (evt.button != 0) return;
                var info = window.provider.WorldInfo;
                if (info.worldNames == null || info.worldNames.Length <= 1) return;
                var menu = new GenericMenu();
                for (var i = 0; i < info.worldNames.Length; i++)
                {
                    var slot = info.worldSlots != null && i < info.worldSlots.Length
                        ? info.worldSlots[i]
                        : i;
                    var capturedSlot = slot;
                    var isCurrent = info.worldNames[i] == info.name;
                    menu.AddItem(new GUIContent(info.worldNames[i]), isCurrent,
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
                    marginLeft = 14
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
            // Minimal dot-led stat chips replace the previous glyph-heavy badges.
            statsGroup.Add(CreateStatChip("ENT", window.entities.Count, EcsDebugV2Theme.Amber));
            statsGroup.Add(CreateStatChip("ARCH", window.archetypes.Count, EcsDebugV2Theme.Orange));
            statsGroup.Add(CreateStatChip("Q", window.queries.Count, EcsDebugV2Theme.Yellow));
            statsGroup.Add(CreateStatChip("SYS", window.systemCount, EcsDebugV2Theme.TypeBool));
            header.Add(statsGroup);

            var themeBtn = new Button()
            {
                name = "theme-btn",
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.MutedText,
                    backgroundColor = EcsDebugV2Theme.PanelElevated.WithAlpha(0.6f),
                    paddingLeft = 9,
                    paddingRight = 9,
                    paddingTop = 4,
                    paddingBottom = 4,
                    letterSpacing = 0.5f,
                    marginLeft = 8
                }
            };
            themeBtn.SetupRadius(EcsDebugV2Theme.BorderRadius);
            themeBtn.SetupGlassBorder();
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
                    backgroundColor = EcsDebugV2Theme.PanelElevated.WithAlpha(0.6f),
                    paddingLeft = 11,
                    paddingRight = 11,
                    paddingTop = 4,
                    paddingBottom = 4,
                    letterSpacing = 0.5f,
                    marginLeft = 6
                }
            };
            pauseBtn.SetupRadius(EcsDebugV2Theme.BorderRadius);
            pauseBtn.SetupGlassBorder();
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
                worldId.text = window.provider.WorldInfo.name;

            if (window.paused != _lastPaused)
            {
                _lastPaused = window.paused;
                var pauseBtn = topPanel.Q("pause-btn") as Button;
                if (pauseBtn != null)
                {
                    pauseBtn.text = window.paused ? "\u25B6 Resume" : "\u23F8 Pause";
                    pauseBtn.style.color = window.paused ? EcsDebugV2Theme.Amber : EcsDebugV2Theme.Foreground;
                    pauseBtn.style.backgroundColor = window.paused
                        ? EcsDebugV2Theme.AmberA012
                        : EcsDebugV2Theme.PanelElevated.WithAlpha(0.6f);
                }

                var pulseDot = topPanel.Q("pulse-dot");
                if (pulseDot != null)
                    pulseDot.style.backgroundColor = window.paused
                        ? EcsDebugV2Theme.Orange
                        : EcsDebugV2Theme.Amber;
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

        // Minimal pill: small leading dot + uppercase label + value.
        private static VisualElement CreateStatChip(string label, int value, Color color)
        {
            var chip = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 9,
                    paddingRight = 9,
                    paddingTop = 4,
                    paddingBottom = 4,
                    marginRight = 6,
                    backgroundColor = EcsDebugV2Theme.PanelElevated.WithAlpha(0.55f)
                }
            };
            chip.SetupRadius(EcsDebugV2Theme.BorderRadius);
            chip.SetupGlassBorder();

            var dot = new Label("\u2022")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = color,
                    marginRight = 5
                }
            };
            chip.Add(dot);

            var labelEl = new Label(label)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Mini,
                    color = EcsDebugV2Theme.MutedText,
                    letterSpacing = 1,
                    marginRight = 5
                }
            };
            chip.Add(labelEl);

            var valueLabel = new Label(value.ToString())
            {
                name = "stats-" + label.ToLower(),
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.Foreground,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            chip.Add(valueLabel);
            return chip;
        }
    }
}
#endif

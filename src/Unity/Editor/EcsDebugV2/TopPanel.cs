#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public static class TopPanel
    {
        public static VisualElement Create(EcsDebugV2Window window)
        {
            var header = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.SpaceBetween,
                    paddingLeft = 12,
                    paddingRight = 12,
                    paddingTop = 8,
                    paddingBottom = 8,
                    backgroundColor = EcsDebugV2Theme.PanelElevated,
                    borderBottomWidth = 1,
                    borderBottomColor = EcsDebugV2Theme.PanelBorder,
                    flexShrink = 0
                }
            };

            var leftGroup = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            var dot = EcsDebugV2Theme.CreateGlowDot(EcsDebugV2Theme.Lime, 10);
            dot.style.marginRight = 8;
            dot.name = "pulse-dot";
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

            var worldId = new Label("world::main")
            {
                name = "world-id",
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.Lime,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
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
            statsGroup.Add(CreateStatBadge("\u25C9", "ENT", window.Entities.Count, EcsDebugV2Theme.Lime));
            statsGroup.Add(CreateStatBadge("\u25A0", "ARCH", window.Archetypes.Count, EcsDebugV2Theme.Orange));
            statsGroup.Add(CreateStatBadge("\u2315", "Q", window.Queries.Count, EcsDebugV2Theme.Yellow));
            statsGroup.Add(CreateStatBadge("\u2630", "SYS", 12, EcsDebugV2Theme.TypeBool));
            header.Add(statsGroup);

            var pauseBtn = new Button(() => window.TogglePause())
            {
                name = "pause-btn",
                text = "\u23F8 Pause",
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.Foreground,
                    backgroundColor = EcsDebugV2Theme.Panel,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = EcsDebugV2Theme.PanelBorder,
                    borderBottomColor = EcsDebugV2Theme.PanelBorder,
                    borderLeftColor = EcsDebugV2Theme.PanelBorder,
                    borderRightColor = EcsDebugV2Theme.PanelBorder,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 4,
                    paddingBottom = 4,
                    letterSpacing = 1
                }
            };
            header.Add(pauseBtn);
            return header;
        }

        public static void Update(VisualElement topPanel, EcsDebugV2Window window)
        {
            var tickLabel = topPanel.Q("tick-label") as Label;
            if (tickLabel != null)
                tickLabel.text = $"t={window.Tick}";

            var pauseBtn = topPanel.Q("pause-btn") as Button;
            if (pauseBtn != null)
            {
                pauseBtn.text = window.Paused ? "\u25B6 Resume" : "\u23F8 Pause";
                pauseBtn.style.color = window.Paused ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.Orange;
            }

            var stats = topPanel.Q("stats-ent") as Label;
            if (stats != null) stats.text = window.Entities.Count.ToString();
            var archStat = topPanel.Q("stats-arch") as Label;
            if (archStat != null) archStat.text = window.Archetypes.Count.ToString();
            var qStat = topPanel.Q("stats-q") as Label;
            if (qStat != null) qStat.text = window.Queries.Count.ToString();
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
                    backgroundColor = EcsDebugV2Theme.Panel,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = EcsDebugV2Theme.PanelBorder,
                    borderBottomColor = EcsDebugV2Theme.PanelBorder,
                    borderLeftColor = EcsDebugV2Theme.PanelBorder,
                    borderRightColor = EcsDebugV2Theme.PanelBorder,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4
                }
            };

            var iconLabel = new Label(icon)
            {
                style =
                {
                    fontSize = 10,
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

#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public static class Footer
    {
        private const string VERSION = "v0.1";
        private static int _lastTick = -1;

        public static VisualElement Create(EcsDebugV2Window window)
        {
            var footer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.SpaceBetween,
                    paddingLeft = 14,
                    paddingRight = 14,
                    paddingTop = 5,
                    paddingBottom = 5,
                    backgroundColor = EcsDebugV2Theme.PanelElevated.WithAlpha(0.4f),
                    borderTopWidth = 1,
                    borderTopColor = EcsDebugV2Theme.GlassBorder,
                    flexShrink = 0
                }
            };

            var tickLabel = new Label("tick 0")
            {
                name = "footer-tick",
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Mini,
                    color = EcsDebugV2Theme.MutedText
                }
            };
            footer.Add(tickLabel);

            var legend = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };
            legend.Add(CreateLegendDot(EcsDebugV2Theme.Amber, "running"));
            legend.Add(CreateLegendDot(EcsDebugV2Theme.Orange, "mutated"));
            legend.Add(CreateLegendDot(EcsDebugV2Theme.Yellow, "flash"));
            legend.Add(CreateLegendDot(EcsDebugV2Theme.Red, "error"));
            footer.Add(legend);

            var version = new Label($"ECS Debugger {VERSION}")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Mini,
                    color = EcsDebugV2Theme.MutedText
                }
            };
            footer.Add(version);

            return footer;
        }

        public static void Update(VisualElement footer, EcsDebugV2Window window)
        {
            if (window.tick == _lastTick) return;
            _lastTick = window.tick;
            var tickLabel = footer.Q("footer-tick") as Label;
            if (tickLabel != null)
                tickLabel.text = $"tick {window.tick}";
        }

        private static VisualElement CreateLegendDot(Color color, string label)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginRight = 14
                }
            };
            // Smaller, softer dot — a filled rounded square instead of a heavy glyph.
            var dot = new VisualElement
            {
                style =
                {
                    width = 7,
                    height = 7,
                    backgroundColor = color.WithAlpha(0.9f),
                    marginRight = 6
                }
            };
            dot.SetupRadius(2);
            row.Add(dot);
            row.Add(new Label(label)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Mini,
                    color = EcsDebugV2Theme.MutedText
                }
            });
            return row;
        }
    }
}
#endif

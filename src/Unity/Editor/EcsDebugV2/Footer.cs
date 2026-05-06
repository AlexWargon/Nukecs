#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public static class Footer
    {
        private const string Version = "v0.1";
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
                    paddingLeft = 12,
                    paddingRight = 12,
                    paddingTop = 4,
                    paddingBottom = 4,
                    backgroundColor = EcsDebugV2Theme.PanelElevated,
                    borderTopWidth = 1,
                    borderTopColor = EcsDebugV2Theme.PanelBorder,
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
            legend.Add(CreateLegendDot(EcsDebugV2Theme.Lime, "running"));
            legend.Add(CreateLegendDot(EcsDebugV2Theme.Orange, "mutated"));
            legend.Add(CreateLegendDot(EcsDebugV2Theme.Yellow, "flash"));
            legend.Add(CreateLegendDot(EcsDebugV2Theme.Red, "error"));
            footer.Add(legend);

            var version = new Label($"ECS Debugger {Version}")
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
                    marginRight = 12
                }
            };
            row.Add(new Label("\u25CF")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Mini,
                    color = color,
                    marginRight = 3
                }
            });
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

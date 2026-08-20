#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public enum TabKey
    {
        Entities,
        Archetypes,
        Queries,
        Resources
    }

    public static class TabBar
    {
        private static readonly (TabKey key, string label)[] Tabs =
        {
            (TabKey.Entities, "Entities"),
            (TabKey.Archetypes, "Archetypes"),
            (TabKey.Queries, "Queries"),
            (TabKey.Resources, "Resources")
        };

        public static VisualElement Create(EcsDebugV2Window window)
        {
            // Segment strip: a recessed track holds pill-shaped tab buttons. The
            // active segment gets an amber-tint fill; inactive ones stay transparent.
            var nav = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 12,
                    paddingRight = 12,
                    paddingTop = 8,
                    paddingBottom = 8,
                    backgroundColor = EcsDebugV2Theme.PanelElevated.WithAlpha(0.45f),
                    borderBottomWidth = 1,
                    borderBottomColor = EcsDebugV2Theme.GlassBorder,
                    flexShrink = 0
                }
            };

            var track = new VisualElement
            {
                name = "segment-track",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    backgroundColor = EcsDebugV2Theme.Background.WithAlpha(0.5f),
                    paddingTop = 3,
                    paddingBottom = 3,
                    paddingLeft = 3,
                    paddingRight = 3
                }
            };
            track.SetupRadius(EcsDebugV2Theme.BorderRadius + 2);
            track.SetupGlassBorder();

            foreach (var tab in Tabs)
            {
                var btn = new Button(() =>
                {
                    window.SetTab(tab.key);
                    Refresh(nav, window);
                })
                {
                    text = tab.label,
                    name = "tab-" + tab.key,
                    style =
                    {
                        fontSize = EcsDebugV2Theme.Font.Small,
                        letterSpacing = 0.4f,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        paddingLeft = 14,
                        paddingRight = 14,
                        paddingTop = 5,
                        paddingBottom = 5,
                        marginRight = 2,
                        borderTopWidth = 0,
                        borderLeftWidth = 0,
                        borderRightWidth = 0,
                        borderBottomWidth = 0,
                        backgroundColor = Color.clear,
                        color = EcsDebugV2Theme.MutedText
                    }
                };
                btn.SetupRadius(EcsDebugV2Theme.BorderRadius);
                track.Add(btn);
            }

            nav.Add(track);
            Refresh(nav, window);
            return nav;
        }

        public static void Refresh(VisualElement nav, EcsDebugV2Window window)
        {
            foreach (var tab in Tabs)
            {
                if (nav.Q("tab-" + tab.key) is not Button btn) continue;
                var active = window.currentTab == tab.key;
                btn.style.color = active ? EcsDebugV2Theme.Amber : EcsDebugV2Theme.MutedText;
                btn.style.backgroundColor = active
                    ? EcsDebugV2Theme.AmberA012
                    : Color.clear;
            }
        }
    }
}
#endif

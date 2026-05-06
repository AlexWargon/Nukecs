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
            var nav = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.FlexEnd,
                    paddingLeft = 10,
                    paddingTop = 6,
                    backgroundColor = EcsDebugV2Theme.PanelElevated,
                    borderBottomWidth = 1,
                    borderBottomColor = EcsDebugV2Theme.PanelBorder,
                    flexShrink = 0
                }
            };

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
                        letterSpacing = 2,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        paddingLeft = 10,
                        paddingRight = 10,
                        paddingTop = 6,
                        paddingBottom = 6,
                        borderTopLeftRadius = EcsDebugV2Theme.BorderRadius,
                        borderTopRightRadius = EcsDebugV2Theme.BorderRadius,
                        borderBottomLeftRadius = 0,
                        borderBottomRightRadius = 0,
                        borderTopWidth = 0,
                        borderLeftWidth = 0,
                        borderRightWidth = 0,
                        borderBottomWidth = 2,
                        backgroundColor = Color.clear
                    }
                };
                nav.Add(btn);
            }

            Refresh(nav, window);
            return nav;
        }

        public static void Refresh(VisualElement nav, EcsDebugV2Window window)
        {
            foreach (var tab in Tabs)
            {
                var btn = nav.Q("tab-" + tab.key) as Button;
                if (btn == null) continue;
                bool active = window.currentTab == tab.key;
                btn.style.color = active ? EcsDebugV2Theme.Lime : EcsDebugV2Theme.MutedText;
                btn.style.borderBottomColor = active ? EcsDebugV2Theme.Lime : Color.clear;
                btn.style.backgroundColor = active ? EcsDebugV2Theme.Panel : Color.clear;
            }
        }
    }
}
#endif

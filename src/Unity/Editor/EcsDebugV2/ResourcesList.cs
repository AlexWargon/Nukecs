#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public static class ResourcesList
    {
        private static int _lastCount = -1;

        public static VisualElement Create(EcsDebugV2Window window)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "resources-scroll",
                style =
                {
                    flexGrow = 1,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 6,
                    paddingBottom = 6,
                    overflow = Overflow.Hidden
                }
            };

            Refresh(scroll, window);
            return scroll;
        }

        public static void Refresh(VisualElement container, EcsDebugV2Window window)
        {
            _lastCount = window.resources.Count;
            var scroll = container as ScrollView;
            var savedOffset = scroll != null ? scroll.scrollOffset : Vector2.zero;

            var content = scroll != null ? scroll.contentContainer : container;
            if (content.childCount == window.resources.Count)
            {
                UpdateExistingCards(content, window);
                if (scroll != null) scroll.scrollOffset = savedOffset;
                return;
            }

            container.Clear();
            foreach (var r in window.resources)
            {
                var card = CreateResourceCard(r, window);
                container.Add(card);
            }
            if (scroll != null) scroll.scrollOffset = savedOffset;
        }

        private static void UpdateExistingCards(VisualElement content, EcsDebugV2Window window)
        {
            int idx = 0;
            foreach (var r in window.resources)
            {
                var card = content[idx];
                card.name = $"resource-card-{r.name}";

                bool selected = window.selectedResourceName == r.name;
                if (selected)
                {
                    card.SetupBorder(EcsDebugV2Theme.AmberA03);
                    card.style.backgroundColor = EcsDebugV2Theme.AmberA012;
                }
                else
                {
                    card.SetupGlassBorder();
                    card.style.backgroundColor = EcsDebugV2Theme.PanelElevated.WithAlpha(0.55f);
                }

                idx++;
            }
        }

        public static void UpdateValues(VisualElement leftPanel, EcsDebugV2Window window)
        {
            if (window.resources.Count != _lastCount)
            {
                _lastCount = window.resources.Count;
                var scroll = leftPanel as ScrollView ?? leftPanel.Q<ScrollView>();
                if (scroll != null)
                {
                    Refresh(scroll, window);
                }
            }
        }

        private static VisualElement CreateResourceCard(ResourceInfo resource, EcsDebugV2Window window)
        {
            bool selected = window.selectedResourceName == resource.name;
            var card = EcsDebugV2Theme.CreateGlassCard();
            card.name = $"resource-card-{resource.name}";
            card.style.flexDirection = FlexDirection.Row;
            card.style.alignItems = Align.Center;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 8;
            card.style.marginBottom = 5;
            card.style.flexShrink = 0;

            if (selected)
            {
                card.SetupBorder(EcsDebugV2Theme.AmberA03);
                card.style.backgroundColor = EcsDebugV2Theme.AmberA012;
            }

            var dot = EcsDebugV2Theme.CreateGlowDot(EcsDebugV2Theme.Amber, 7);
            dot.style.marginRight = 10;
            card.Add(dot);

            var nameLabel = new Label(resource.name)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.Foreground,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    flexShrink = 1
                }
            };
            card.Add(nameLabel);

            var typeLabel = new Label(resource.type)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.MutedText,
                    marginLeft = Length.Auto()
                }
            };
            card.Add(typeLabel);

            card.RegisterCallback<ClickEvent>(_ => window.SelectResource(resource.name));
            card.ApplyHover(() => window.selectedResourceName == resource.name);
            return card;
        }
    }
}
#endif

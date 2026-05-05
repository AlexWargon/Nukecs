#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public static class ResourcesList
    {
        public static VisualElement Create(EcsDebugV2Window window)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
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
            var scroll = container as ScrollView;
            var savedOffset = scroll != null ? scroll.scrollOffset : Vector2.zero;
            container.Clear();
            foreach (var r in window.Resources)
            {
                var card = CreateResourceCard(r, window);
                container.Add(card);
            }
            if (scroll != null) scroll.scrollOffset = savedOffset;
        }

        public static void UpdateValues(VisualElement leftPanel, EcsDebugV2Window window)
        {
        }

        private static VisualElement CreateResourceCard(ResourceInfo resource, EcsDebugV2Window window)
        {
            bool selected = window.SelectedResourceName == resource.Name;
            var card = EcsDebugV2Theme.CreateCard();
            card.style.flexDirection = FlexDirection.Row;
            card.style.alignItems = Align.Center;
            card.style.paddingLeft = 8;
            card.style.paddingRight = 8;
            card.style.paddingTop = 6;
            card.style.paddingBottom = 6;
            card.style.marginBottom = 4;
            card.style.flexShrink = 0;

            if (selected)
            {
                card.SetupBorder(EcsDebugV2Theme.Yellow);
                card.style.backgroundColor = EcsDebugV2Theme.Yellow.WithAlpha(0.1f);
            }

            var dot = EcsDebugV2Theme.CreateGlowDot(EcsDebugV2Theme.Yellow, 6);
            dot.style.marginRight = 8;
            card.Add(dot);

            var nameLabel = new Label(resource.Name)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.Foreground,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            card.Add(nameLabel);

            var typeLabel = new Label(resource.Type)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.MutedText,
                    marginLeft = UnityEngine.UIElements.Length.Auto()
                }
            };
            card.Add(typeLabel);

            card.RegisterCallback<ClickEvent>(_ => window.SelectResource(resource.Name));
            card.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (window.SelectedResourceName != resource.Name)
                    card.style.backgroundColor = EcsDebugV2Theme.PanelElevated.WithAlpha(0.4f);
            });
            card.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (window.SelectedResourceName != resource.Name)
                    card.style.backgroundColor = EcsDebugV2Theme.Panel;
                else
                    card.style.backgroundColor = EcsDebugV2Theme.Yellow.WithAlpha(0.1f);
            });
            return card;
        }
    }
}
#endif

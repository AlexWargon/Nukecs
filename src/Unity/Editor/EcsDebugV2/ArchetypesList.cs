#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public static class ArchetypesList
    {
        private static int _lastCount = -1;

        public static VisualElement Create(EcsDebugV2Window window)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "archetypes-scroll",
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
            _lastCount = window.archetypes.Count;
            var scroll = container as ScrollView;
            var savedOffset = scroll != null ? scroll.scrollOffset : Vector2.zero;

            if (window.archetypes.Count == 0)
            {
                container.Clear();
                return;
            }

            var content = scroll != null ? scroll.contentContainer : container;
            if (content.childCount == window.archetypes.Count)
            {
                UpdateExistingCards(content, window);
                if (scroll != null) scroll.scrollOffset = savedOffset;
                return;
            }

            container.Clear();
            foreach (var arch in window.archetypes)
            {
                var card = CreateArchetypeCard(arch, window);
                container.Add(card);
            }

            if (scroll != null) scroll.scrollOffset = savedOffset;
        }

        private static void UpdateExistingCards(VisualElement content, EcsDebugV2Window window)
        {
            int idx = 0;
            foreach (var arch in window.archetypes)
            {
                var card = content[idx];
                card.name = $"arch-card-{arch.id}";

                bool selected = window.selectedArchetypeId == arch.id;
                if (selected)
                {
                    card.SetupBorder(EcsDebugV2Theme.Orange);
                    card.style.backgroundColor = EcsDebugV2Theme.OrangeA01;
                }
                else
                {
                    card.SetupBorder(EcsDebugV2Theme.PanelBorder);
                    card.style.backgroundColor = EcsDebugV2Theme.Panel;
                }

                var countLabel = card.Q<Label>("arch-count");
                if (countLabel != null)
                    countLabel.text = $"{arch.entityCount} ent \u00B7 {arch.chunkCount} ch";

                idx++;
            }
        }

        public static void UpdateValues(VisualElement leftPanel, EcsDebugV2Window window)
        {
            if (window.archetypes.Count != _lastCount)
            {
                _lastCount = window.archetypes.Count;
                var scroll = leftPanel as ScrollView ?? leftPanel.Q<ScrollView>();
                if (scroll != null)
                {
                    Refresh(scroll, window);
                    return;
                }
            }

            foreach (var arch in window.archetypes)
            {
                var card = leftPanel.Q($"arch-card-{arch.id}");
                if (card == null) continue;
                var countLabel = card.Q<Label>("arch-count");
                if (countLabel != null)
                    countLabel.text = $"{arch.entityCount} ent \u00B7 {arch.chunkCount} ch";
            }
        }

        private static VisualElement CreateArchetypeCard(ArchetypeInfo arch, EcsDebugV2Window window)
        {
            bool selected = window.selectedArchetypeId == arch.id;
            var card = EcsDebugV2Theme.CreateCard();
            card.name = $"arch-card-{arch.id}";
            card.style.paddingLeft = 8;
            card.style.paddingRight = 8;
            card.style.paddingTop = 4;
            card.style.paddingBottom = 4;
            card.style.marginBottom = 3;
            card.style.flexShrink = 0;

            if (selected)
            {
                card.SetupBorder(EcsDebugV2Theme.Orange);
                card.style.backgroundColor = EcsDebugV2Theme.OrangeA01;
            }

            var topRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 3
                }
            };
            var archLabel = new Label("ARCH")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Mini,
                    color = EcsDebugV2Theme.MutedText,
                    letterSpacing = 1,
                    marginRight = 6
                }
            };
            topRow.Add(archLabel);
            var idLabel = new Label($"#{arch.id}")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.Orange,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            topRow.Add(idLabel);
            var countLabel = new Label($"{arch.entityCount} ent \u00B7 {arch.chunkCount} ch")
            {
                name = "arch-count",
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.MutedText,
                    marginLeft = UnityEngine.UIElements.Length.Auto()
                }
            };
            topRow.Add(countLabel);
            card.Add(topRow);

            var tagRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap
                }
            };
            foreach (var comp in arch.components)
                tagRow.Add(EcsDebugV2Theme.CreateFilterTag(comp, true));
            card.Add(tagRow);

            card.RegisterCallback<ClickEvent>(_ => window.SelectArchetype(arch.id));
            card.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (window.selectedArchetypeId != arch.id)
                    card.style.backgroundColor = EcsDebugV2Theme.PanelElevatedA04;
            });
            card.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (window.selectedArchetypeId != arch.id)
                    card.style.backgroundColor = EcsDebugV2Theme.Panel;
                else
                    card.style.backgroundColor = EcsDebugV2Theme.OrangeA01;
            });
            return card;
        }
    }
}
#endif

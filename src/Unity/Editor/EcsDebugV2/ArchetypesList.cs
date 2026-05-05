#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public static class ArchetypesList
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
            container.Clear();
            if (window.Archetypes.Count == 0) return;
            int maxCount = 1;
            foreach (var a in window.Archetypes)
                if (a.EntityCount > maxCount) maxCount = a.EntityCount;

            foreach (var arch in window.Archetypes)
            {
                var card = CreateArchetypeCard(arch, maxCount, window);
                container.Add(card);
            }
        }

        private static VisualElement CreateArchetypeCard(MockArchetype arch, int maxCount, EcsDebugV2Window window)
        {
            bool selected = window.SelectedArchetypeId == arch.Id;
            var card = EcsDebugV2Theme.CreateCard();
            card.style.paddingLeft = 8;
            card.style.paddingRight = 8;
            card.style.paddingTop = 6;
            card.style.paddingBottom = 6;
            card.style.marginBottom = 4;
            card.style.flexShrink = 0;

            if (selected)
            {
                card.style.borderTopColor = EcsDebugV2Theme.Orange;
                card.style.borderBottomColor = EcsDebugV2Theme.Orange;
                card.style.borderLeftColor = EcsDebugV2Theme.Orange;
                card.style.borderRightColor = EcsDebugV2Theme.Orange;
                card.style.backgroundColor = EcsDebugV2Theme.Orange.WithAlpha(0.1f);
            }

            var topRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 4
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
            var idLabel = new Label($"#{arch.Id}")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.Orange,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            topRow.Add(idLabel);
            var countLabel = new Label($"{arch.EntityCount} ent \u00B7 {arch.ChunkCount} ch")
            {
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
                    flexWrap = Wrap.Wrap,
                    marginBottom = 4
                }
            };
            foreach (var comp in arch.Components)
                tagRow.Add(EcsDebugV2Theme.CreateFilterTag(comp, true));
            card.Add(tagRow);

            var barBg = new VisualElement
            {
                style =
                {
                    height = 4,
                    backgroundColor = EcsDebugV2Theme.PanelElevated,
                    borderTopLeftRadius = 2,
                    borderTopRightRadius = 2,
                    borderBottomLeftRadius = 2,
                    borderBottomRightRadius = 2,
                    overflow = Overflow.Hidden
                }
            };
            var pct = (float)arch.EntityCount / maxCount;
            var barFill = new VisualElement
            {
                style =
                {
                    height = 4,
                    width = UnityEngine.UIElements.Length.Percent(pct * 100),
                    backgroundColor = EcsDebugV2Theme.Yellow,
                    borderTopLeftRadius = 2,
                    borderTopRightRadius = 2,
                    borderBottomLeftRadius = 2,
                    borderBottomRightRadius = 2
                }
            };
            barBg.Add(barFill);
            card.Add(barBg);

            card.RegisterCallback<ClickEvent>(_ => window.SelectArchetype(arch.Id));
            card.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (window.SelectedArchetypeId != arch.Id)
                    card.style.backgroundColor = EcsDebugV2Theme.PanelElevated.WithAlpha(0.4f);
            });
            card.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (window.SelectedArchetypeId != arch.Id)
                    card.style.backgroundColor = EcsDebugV2Theme.Panel;
                else
                    card.style.backgroundColor = EcsDebugV2Theme.Orange.WithAlpha(0.1f);
            });
            return card;
        }
    }
}
#endif

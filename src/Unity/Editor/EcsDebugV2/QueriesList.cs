#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public static class QueriesList
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
            foreach (var q in window.Queries)
            {
                var card = CreateQueryCard(q, window);
                container.Add(card);
            }
            if (scroll != null) scroll.scrollOffset = savedOffset;
        }

        public static void UpdateValues(VisualElement leftPanel, EcsDebugV2Window window)
        {
        }

        private static VisualElement CreateQueryCard(QueryInfo query, EcsDebugV2Window window)
        {
            bool selected = window.SelectedQueryId == query.Id;
            var card = EcsDebugV2Theme.CreateCard();
            card.style.paddingLeft = 8;
            card.style.paddingRight = 8;
            card.style.paddingTop = 6;
            card.style.paddingBottom = 6;
            card.style.marginBottom = 4;
            card.style.flexShrink = 0;

            if (selected)
            {
                card.SetupBorder(EcsDebugV2Theme.Lime);
                card.style.backgroundColor = EcsDebugV2Theme.Lime.WithAlpha(0.1f);
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
            var nameLabel = new Label(query.Name)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.Lime,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            topRow.Add(nameLabel);
            var matchedLabel = new Label($"{query.Matched} matched")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.Orange,
                    marginLeft = UnityEngine.UIElements.Length.Auto()
                }
            };
            topRow.Add(matchedLabel);
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
            foreach (var w in query.With)
                tagRow.Add(EcsDebugV2Theme.CreateFilterTag("+" + w, true));
            foreach (var w in query.Without)
                tagRow.Add(EcsDebugV2Theme.CreateFilterTag("\u2212" + w, false));
            card.Add(tagRow);

            var timeLabel = new Label($"last {query.LastRunMs:F2} ms")
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.Yellow
                }
            };
            card.Add(timeLabel);

            card.RegisterCallback<ClickEvent>(_ => window.SelectQuery(query.Id));
            card.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (window.SelectedQueryId != query.Id)
                    card.style.backgroundColor = EcsDebugV2Theme.PanelElevated.WithAlpha(0.4f);
            });
            card.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (window.SelectedQueryId != query.Id)
                    card.style.backgroundColor = EcsDebugV2Theme.Panel;
                else
                    card.style.backgroundColor = EcsDebugV2Theme.Lime.WithAlpha(0.1f);
            });
            return card;
        }
    }
}
#endif

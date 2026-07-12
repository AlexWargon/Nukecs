#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public static class QueriesList
    {
        private static int _lastCount = -1;

        public static VisualElement Create(EcsDebugV2Window window)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "queries-scroll",
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
            _lastCount = window.queries.Count;
            var scroll = container as ScrollView;
            var savedOffset = scroll != null ? scroll.scrollOffset : Vector2.zero;

            var content = scroll != null ? scroll.contentContainer : container;
            if (content.childCount == window.queries.Count)
            {
                UpdateExistingCards(content, window);
                if (scroll != null) scroll.scrollOffset = savedOffset;
                return;
            }

            container.Clear();
            foreach (var q in window.queries)
            {
                var card = CreateQueryCard(q, window);
                container.Add(card);
            }
            if (scroll != null) scroll.scrollOffset = savedOffset;
        }

        private static void UpdateExistingCards(VisualElement content, EcsDebugV2Window window)
        {
            int idx = 0;
            foreach (var q in window.queries)
            {
                var card = content[idx];
                card.name = $"query-card-{q.id}";

                bool selected = window.selectedQueryId == q.id;
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

                var matchedLabel = card.Q<Label>("query-matched");
                if (matchedLabel != null)
                    matchedLabel.text = $"{q.matched} matched";
                var timeLabel = card.Q<Label>("query-time");
                if (timeLabel != null)
                    timeLabel.text = $"last {q.lastRunMs:F2} ms";

                idx++;
            }
        }

        public static void UpdateValues(VisualElement leftPanel, EcsDebugV2Window window)
        {
            if (window.queries.Count != _lastCount)
            {
                _lastCount = window.queries.Count;
                var scroll = leftPanel as ScrollView ?? leftPanel.Q<ScrollView>();
                if (scroll != null)
                {
                    Refresh(scroll, window);
                    return;
                }
            }

            foreach (var q in window.queries)
            {
                var card = leftPanel.Q($"query-card-{q.id}");
                if (card == null) continue;
                var matchedLabel = card.Q<Label>("query-matched");
                if (matchedLabel != null)
                    matchedLabel.text = $"{q.matched} matched";
                var timeLabel = card.Q<Label>("query-time");
                if (timeLabel != null)
                    timeLabel.text = $"last {q.lastRunMs:F2} ms";
            }
        }

        private static VisualElement CreateQueryCard(QueryInfo query, EcsDebugV2Window window)
        {
            bool selected = window.selectedQueryId == query.id;
            var card = EcsDebugV2Theme.CreateGlassCard();
            card.name = $"query-card-{query.id}";
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

            var topRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 5
                }
            };
            var nameLabel = new Label(query.name)
            {
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Small,
                    color = EcsDebugV2Theme.Foreground,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    flexShrink = 1
                }
            };
            topRow.Add(nameLabel);
            var matchedLabel = new Label($"{query.matched} matched")
            {
                name = "query-matched",
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.Amber,
                    marginLeft = Length.Auto()
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
                    marginBottom = 5
                }
            };
            foreach (var w in query.with)
                tagRow.Add(EcsDebugV2Theme.CreateFilterTag("+" + w, true));
            foreach (var w in query.without)
                tagRow.Add(EcsDebugV2Theme.CreateFilterTag("\u2212" + w, false));
            card.Add(tagRow);

            var timeLabel = new Label($"last {query.lastRunMs:F2} ms")
            {
                name = "query-time",
                style =
                {
                    fontSize = EcsDebugV2Theme.Font.Micro,
                    color = EcsDebugV2Theme.MutedText
                }
            };
            card.Add(timeLabel);

            card.RegisterCallback<ClickEvent>(_ => window.SelectQuery(query.id));
            card.ApplyHover(() => window.selectedQueryId == query.id);
            return card;
        }
    }
}
#endif

#if UNITY_EDITOR
namespace Wargon.Nukecs
{
    using System;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;

    public unsafe class AllocatorDebugWindow : EditorWindow
    {
        private static readonly Color BG = new(0.18f, 0.18f, 0.18f);
        private static readonly Color CardBg = new(0.157f, 0.157f, 0.157f);
        private static readonly Color Border = new(0.235f, 0.235f, 0.235f);
        private static readonly Color BarTrackBg = new(0.118f, 0.118f, 0.118f);
        private static readonly Color HeaderBg = new(0.137f, 0.137f, 0.137f);
        private static readonly Color HeaderBorder = new(0.216f, 0.216f, 0.216f);

        private static void SetBorderRadius(IStyle s, float r)
        {
            s.borderTopLeftRadius = r;
            s.borderTopRightRadius = r;
            s.borderBottomLeftRadius = r;
            s.borderBottomRightRadius = r;
        }

        private static void SetBorderWidth(IStyle s, float w)
        {
            s.borderTopWidth = w;
            s.borderBottomWidth = w;
            s.borderLeftWidth = w;
            s.borderRightWidth = w;
        }

        private static void SetBorderColor(IStyle s, Color c)
        {
            s.borderTopColor = c;
            s.borderBottomColor = c;
            s.borderLeftColor = c;
            s.borderRightColor = c;
        }

        private static AllocatorDebugWindow _instance;
        private VisualElement _root;
        private ScrollView _contentScroll;
        private VisualElement _headerStatus;
        private VisualElement _overviewContainer;
        private VisualElement _overallBarFill;
        private Label _overallBarLabel;
        private VisualElement _regionsContainer;
        private Label _noDataLabel;

        [MenuItem("Nuke.cs/Allocator Debug")]
        public static void ShowWindow()
        {
            _instance = GetWindow<AllocatorDebugWindow>();
            _instance.titleContent = new GUIContent("Allocator Debug");
            _instance.minSize = new Vector2(380, 500);
            _instance.Show();
        }

        public void CreateGUI()
        {
            _root = rootVisualElement;
            _root.style.flexDirection = FlexDirection.Column;
            _root.style.backgroundColor = BG;

            DrawHeader();

            _contentScroll = new ScrollView(ScrollViewMode.Vertical);
            _contentScroll.style.flexGrow = 1;
            _root.Add(_contentScroll);

            DrawOverview();
            DrawOverallUsage();
            DrawRegionsSection();

            _noDataLabel = new Label("No active worlds found.\nStart play mode to inspect the allocator.");
            _noDataLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            _noDataLabel.style.fontSize = 13;
            _noDataLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _noDataLabel.style.flexGrow = 1;
            _noDataLabel.style.justifyContent = Justify.Center;
            _noDataLabel.style.display = DisplayStyle.Flex;
            _root.Add(_noDataLabel);

            _contentScroll.style.display = DisplayStyle.None;

            _root.schedule.Execute(Refresh).Every(200);
        }

        private void DrawHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.paddingTop = 8;
            header.style.paddingBottom = 8;
            header.style.backgroundColor = HeaderBg;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = HeaderBorder;

            var title = new Label("Memory Allocator");
            title.style.color = new Color(0.9f, 0.9f, 0.9f);
            title.style.fontSize = 15;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);

            _headerStatus = new VisualElement();
            _headerStatus.style.paddingLeft = 8;
            _headerStatus.style.paddingRight = 8;
            _headerStatus.style.paddingTop = 2;
            _headerStatus.style.paddingBottom = 2;
            SetBorderRadius(_headerStatus.style, 10);
            _headerStatus.style.fontSize = 11;
            _headerStatus.style.backgroundColor = new Color(0.47f, 0.16f, 0.16f);
            _headerStatus.Add(new Label("Offline"));
            header.Add(_headerStatus);

            _root.Add(header);
        }

        private void DrawOverview()
        {
            _overviewContainer = CreateCard();

            var title = CreateSectionTitle("Overview");
            _overviewContainer.Add(title);

            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.Add(CreateOverviewStat("total-val", "Total Capacity", "--"));
            grid.Add(CreateOverviewStat("used-val", "Memory Used", "--"));
            grid.Add(CreateOverviewStat("free-val", "Memory Free", "--"));
            grid.Add(CreateOverviewStat("regions-val", "Regions", "--"));
            grid.Add(CreateOverviewStat("freeblocks-val", "Free Blocks", "--"));
            grid.Add(CreateOverviewStat("freemem-val", "Free List Mem", "--"));
            _overviewContainer.Add(grid);

            _contentScroll.Add(_overviewContainer);
        }

        private VisualElement CreateOverviewStat(string statName, string label, string initialValue)
        {
            var container = new VisualElement();
            container.style.width = new StyleLength(new Length(33.3f, LengthUnit.Percent));
            container.style.paddingTop = 8;
            container.style.paddingBottom = 8;

            var value = new Label(initialValue) { name = statName };
            value.style.color = new Color(0.9f, 0.9f, 0.9f);
            value.style.fontSize = 16;
            value.style.unityFontStyleAndWeight = FontStyle.Bold;
            container.Add(value);

            var lbl = new Label(label);
            lbl.style.color = new Color(0.51f, 0.51f, 0.51f);
            lbl.style.fontSize = 11;
            lbl.style.marginTop = 2;
            container.Add(lbl);

            return container;
        }

        private void DrawOverallUsage()
        {
            var section = CreateCard();

            var title = CreateSectionTitle("Overall Usage");
            section.Add(title);

            var track = new VisualElement();
            track.style.height = 14;
            track.style.backgroundColor = BarTrackBg;
            SetBorderRadius(track.style, 7);
            track.style.overflow = Overflow.Hidden;
            track.style.position = Position.Relative;

            _overallBarFill = new VisualElement();
            _overallBarFill.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
            SetBorderRadius(_overallBarFill.style, 7);
            track.Add(_overallBarFill);

            _overallBarLabel = new Label("");
            _overallBarLabel.style.color = new Color(0.78f, 0.78f, 0.78f);
            _overallBarLabel.style.fontSize = 10;
            _overallBarLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _overallBarLabel.style.position = Position.Absolute;
            _overallBarLabel.style.left = 0;
            _overallBarLabel.style.right = 0;
            _overallBarLabel.style.top = 0;
            _overallBarLabel.style.bottom = 0;
            track.Add(_overallBarLabel);

            section.Add(track);
            _contentScroll.Add(section);
        }

        private void DrawRegionsSection()
        {
            var section = CreateCard();

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.justifyContent = Justify.SpaceBetween;
            titleRow.style.alignItems = Align.Center;

            var title = new Label("Regions");
            title.style.color = new Color(0.86f, 0.86f, 0.86f);
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginTop = 0;
            title.style.marginBottom = 8;
            titleRow.Add(title);

            var refreshBtn = new Button(() => Refresh()) { text = "Refresh" };
            refreshBtn.style.paddingLeft = 8;
            refreshBtn.style.paddingRight = 8;
            refreshBtn.style.paddingTop = 2;
            refreshBtn.style.paddingBottom = 2;
            titleRow.Add(refreshBtn);

            section.Add(titleRow);

            _regionsContainer = new VisualElement();
            section.Add(_regionsContainer);

            _contentScroll.Add(section);
        }

        private VisualElement CreateCard()
        {
            var card = new VisualElement();
            card.style.backgroundColor = CardBg;
            SetBorderWidth(card.style, 1);
            SetBorderColor(card.style, Border);
            SetBorderRadius(card.style, 6);
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.paddingTop = 12;
            card.style.paddingBottom = 12;
            card.style.marginBottom = 8;
            return card;
        }

        private static Label CreateSectionTitle(string text)
        {
            var label = new Label(text);
            label.style.color = new Color(0.86f, 0.86f, 0.86f);
            label.style.fontSize = 14;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 12;
            label.style.marginBottom = 8;
            return label;
        }

        private void Refresh()
        {
            if (!World.HasActiveWorlds())
            {
                _noDataLabel.style.display = DisplayStyle.Flex;
                _contentScroll.style.display = DisplayStyle.None;
                UpdateStatus(false);
                return;
            }

            ref var world = ref World.Default;
            if(!world.IsAlive) return;
            var allocatorPtr = world.AllocatorHandler.AllocatorWrapper.GetAllocatorPtr();
            if (allocatorPtr == null)
            {
                _noDataLabel.style.display = DisplayStyle.Flex;
                _contentScroll.style.display = DisplayStyle.None;
                UpdateStatus(false);
                return;
            }

            _noDataLabel.style.display = DisplayStyle.None;
            _contentScroll.style.display = DisplayStyle.Flex;
            UpdateStatus(true);

            ref var allocator = ref *allocatorPtr;
            UpdateOverview(ref allocator);
            UpdateOverallUsage(ref allocator);
            UpdateRegions(ref allocator);
        }

        private void UpdateStatus(bool active)
        {
            _headerStatus.Clear();
            if (active)
            {
                _headerStatus.style.backgroundColor = new Color(0.16f, 0.47f, 0.16f);
                var lbl = new Label("Active");
                lbl.style.color = new Color(0.7f, 1f, 0.7f);
                lbl.style.fontSize = 11;
                lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                _headerStatus.Add(lbl);
            }
            else
            {
                _headerStatus.style.backgroundColor = new Color(0.47f, 0.16f, 0.16f);
                var lbl = new Label("Offline");
                lbl.style.color = new Color(1f, 0.7f, 0.7f);
                lbl.style.fontSize = 11;
                lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
                _headerStatus.Add(lbl);
            }
        }

        private void UpdateOverview(ref MemAllocator allocator)
        {
            SetOverviewStat("total-val", FormatBytes(allocator.TotalSize));
            SetOverviewStat("used-val", FormatBytes(allocator.MemoryUsed));
            SetOverviewStat("free-val", FormatBytes(allocator.MemoryLeft));
            SetOverviewStat("regions-val", allocator.RegionCount.ToString());
            SetOverviewStat("freeblocks-val", allocator.TotalFreeBlocks.ToString());
            SetOverviewStat("freemem-val", FormatBytes(allocator.FreeListMemory));
        }

        private void UpdateOverallUsage(ref MemAllocator allocator)
        {
            var used = allocator.MemoryUsed;
            var total = allocator.TotalSize;
            var pct = total > 0 ? (float)used / total : 0f;

            _overallBarFill.style.width = new StyleLength(new Length(Mathf.Clamp01(pct) * 100, LengthUnit.Percent));
            _overallBarFill.style.backgroundColor = GetUsageColor(pct);
            _overallBarLabel.text = $"{FormatBytes(used)} / {FormatBytes(total)} ({pct * 100:F1}%)";
            _overallBarLabel.style.color = pct > 0.7f ? Color.white : new Color(0.8f, 0.8f, 0.8f);
        }

        private void UpdateRegions(ref MemAllocator allocator)
        {
            var regionCount = allocator.RegionCount;
            var childCount = _regionsContainer.childCount;

            for (int i = 0; i < regionCount; i++)
            {
                ref var region = ref allocator.GetRegion(i);
                var regionUsed = region.cursor;
                var regionSize = region.size;
                var pct = regionSize > 0 ? (float)regionUsed / regionSize : 0f;

                VisualElement card;
                if (i < childCount)
                    card = _regionsContainer[i];
                else
                {
                    card = CreateRegionCard();
                    _regionsContainer.Add(card);
                }

                UpdateRegionCard(card, i, regionUsed, regionSize, pct, region.freeCount);
            }

            for (int i = childCount - 1; i >= regionCount; i--)
                _regionsContainer.RemoveAt(i);
        }

        private VisualElement CreateRegionCard()
        {
            var card = new VisualElement();
            card.style.backgroundColor = CardBg;
            SetBorderWidth(card.style, 1);
            SetBorderColor(card.style, Border);
            SetBorderRadius(card.style, 6);
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.paddingTop = 10;
            card.style.paddingBottom = 10;
            card.style.marginBottom = 6;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;

            var name = new Label { name = "region-name" };
            name.style.color = new Color(0.78f, 0.78f, 0.78f);
            name.style.fontSize = 13;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;

            var percent = new Label { name = "region-percent" };
            percent.style.fontSize = 12;
            percent.style.unityFontStyleAndWeight = FontStyle.Bold;

            header.Add(name);
            header.Add(percent);
            card.Add(header);

            var barTrack = new VisualElement();
            barTrack.style.height = 8;
            barTrack.style.backgroundColor = BarTrackBg;
            SetBorderRadius(barTrack.style, 4);
            barTrack.style.marginTop = 6;
            barTrack.style.overflow = Overflow.Hidden;

            var barFill = new VisualElement { name = "bar-fill" };
            barFill.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
            SetBorderRadius(barFill.style, 4);
            barTrack.Add(barFill);
            card.Add(barTrack);

            var details = new VisualElement();
            details.style.flexDirection = FlexDirection.Row;
            details.style.justifyContent = Justify.SpaceBetween;
            details.style.marginTop = 6;

            var detailColor = new Color(0.55f, 0.55f, 0.55f);
            var usedLabel = new Label { name = "detail-used" };
            usedLabel.style.color = detailColor;
            usedLabel.style.fontSize = 11;
            var freeLabel = new Label { name = "detail-free" };
            freeLabel.style.color = detailColor;
            freeLabel.style.fontSize = 11;
            var totalLabel = new Label { name = "detail-total" };
            totalLabel.style.color = detailColor;
            totalLabel.style.fontSize = 11;
            var freeBlocksLabel = new Label { name = "detail-freeblocks" };
            freeBlocksLabel.style.color = new Color(0.18f, 0.75f, 0.35f);
            freeBlocksLabel.style.fontSize = 11;

            details.Add(usedLabel);
            details.Add(freeLabel);
            details.Add(totalLabel);
            details.Add(freeBlocksLabel);
            card.Add(details);

            return card;
        }

        private void UpdateRegionCard(VisualElement card, int index, long used, long size, float pct, int freeBlocks)
        {
            var name = card.Q<Label>("region-name");
            name.text = $"Region {index}";

            var percent = card.Q<Label>("region-percent");
            percent.text = $"{pct * 100:F1}%";
            percent.style.color = GetUsageColor(pct);

            var barFill = card.Q<VisualElement>("bar-fill");
            barFill.style.width = new StyleLength(new Length(Mathf.Clamp01(pct) * 100, LengthUnit.Percent));
            barFill.style.backgroundColor = GetUsageColor(pct);

            card.Q<Label>("detail-used").text = $"Used: {FormatBytes(used)}";
            card.Q<Label>("detail-free").text = $"Free: {FormatBytes(size - used)}";
            card.Q<Label>("detail-total").text = $"Total: {FormatBytes(size)}";
            card.Q<Label>("detail-freeblocks").text = $"Free blocks: {freeBlocks}";
        }

        private static Color GetUsageColor(float pct)
        {
            if (pct < 0.5f)
                return Color.Lerp(new Color(0.18f, 0.75f, 0.35f), new Color(0.95f, 0.75f, 0.1f), pct / 0.5f);
            if (pct < 0.8f)
                return Color.Lerp(new Color(0.95f, 0.75f, 0.1f), new Color(0.95f, 0.4f, 0.1f), (pct - 0.5f) / 0.3f);
            return Color.Lerp(new Color(0.95f, 0.4f, 0.1f), new Color(0.9f, 0.15f, 0.15f), (pct - 0.8f) / 0.2f);
        }

        private void SetOverviewStat(string statName, string value)
        {
            var label = _overviewContainer.Q<Label>(statName);
            if (label != null) label.text = value;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 0) return "0 B";
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < suffixes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {suffixes[order]}";
        }
    }
}
#endif

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs
{

    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using UnityEditor;
    using UnityEngine;
    using UnityEngine.UIElements;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public unsafe class MemoryAllocatorDebugWindow2 : EditorWindow
    {
        private SerializableMemoryAllocator allocator;
        private Label totalSizeLabel;
        private Label usedSizeLabel;
        private Label freeSizeLabel;
        private Label blockCountLabel;
        private Label defragCyclesLabel;
        private VisualElement mosaicContainer;

        private const float MIN_BLOCK_SIZE = 20f; // Минимальный размер блока в пикселях (ширина/высота)
        private const long MIN_MEMORY_SIZE = 1024; // Минимальный размер блока в памяти для отображения (1 KB)
        private const int MAX_DISPLAYED_BLOCKS = 100; // Максимальное число отображаемых блоков для производительности

        [MenuItem("Nuke.cs/Memory Allocator Debug (UIElements)")]
        public static void ShowWindow()
        {
            GetWindow<MemoryAllocatorDebugWindow2>("Memory Allocator Debug (UIElements)");
        }

        private void CreateGUI()
        {
            // Основной контейнер
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;

            // Заголовок с информацией
            var infoLabel = new Label("Memory Allocator Info");
            infoLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(infoLabel);

            // Поля с данными
            var infoContainer = new VisualElement();
            infoContainer.style.flexDirection = FlexDirection.Column;

            totalSizeLabel = new Label("Total Size: ");
            usedSizeLabel = new Label("Used Size: ");
            freeSizeLabel = new Label("Free Size: ");
            blockCountLabel = new Label("Block Count: ");
            defragCyclesLabel = new Label("Defragmentation Cycles: ");

            infoContainer.Add(totalSizeLabel);
            infoContainer.Add(usedSizeLabel);
            infoContainer.Add(freeSizeLabel);
            infoContainer.Add(blockCountLabel);
            infoContainer.Add(defragCyclesLabel);
            root.Add(infoContainer);

            // Контейнер для мозаики
            mosaicContainer = new VisualElement();
            mosaicContainer.style.flexDirection = FlexDirection.Column;
            mosaicContainer.style.flexWrap = new StyleEnum<Wrap>(Wrap.NoWrap);
            mosaicContainer.style.height = 400; // Увеличенная высота для плотной упаковки
            mosaicContainer.style.backgroundColor = Color.black; // Фон для контраста
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.Add(mosaicContainer);
            root.Add(scrollView);

            // Кнопка обновления
            var refreshButton = new Button(() => UpdateUI()) { text = "Refresh" };
            refreshButton.style.marginTop = 10;
            root.Add(refreshButton);

            // Инициализируем аллокатор (если доступен) и обновляем UI
            allocator = World.Default.AllocatorHandler.AllocatorWrapper.Allocator;
            UpdateUI();
        }

        // Метод для передачи аллокатора в окно
        public void SetAllocator(ref SerializableMemoryAllocator allocator)
        {
            this.allocator = allocator;
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (!allocator.IsActive)
            {
                rootVisualElement.Clear();
                rootVisualElement.Add(new Label("Allocator is not initialized or disposed."));
                return;
            }

            // Обновляем информацию
            var memoryInfo = allocator.GetMemoryInfo();
            totalSizeLabel.text = $"Total Size: {memoryInfo.totalSize} bytes";
            usedSizeLabel.text = $"Used Size: {memoryInfo.usedSize} bytes";
            freeSizeLabel.text = $"Free Size: {memoryInfo.freeSize} bytes";
            blockCountLabel.text = $"Block Count: {memoryInfo.blockCount}";
            defragCyclesLabel.text = $"Defragmentation Cycles: {memoryInfo.defragmentationCycles}";

            // Очищаем и перестраиваем мозаику
            mosaicContainer.Clear();
            float containerWidth = position.width - 40; // Учитываем отступы и скроллбар
            float containerHeight = 400; // Фиксированная высота мозаики

            // Собираем данные о блоках и агрегируем мелкие
            List<(int index, long size, bool isUsed)> blocks = new();
            unsafe
            {
                MemoryView view = allocator.GetMemoryView();
                long usedSum = 0, freeSum = 0;
                bool inUsedRun = false, inFreeRun = false;
                int startIndex = 0;

                for (int i = 0; i < view.BlockCount; i++)
                {
                    var block = view.Blocks[i];
                    if (block.Size < MIN_MEMORY_SIZE)
                    {
                        if (block.IsUsed)
                        {
                            if (!inUsedRun)
                            {
                                if (inFreeRun)
                                {
                                    blocks.Add((startIndex, freeSum, false));
                                    inFreeRun = false;
                                    freeSum = 0;
                                }

                                inUsedRun = true;
                                usedSum = 0;
                                startIndex = i;
                            }

                            usedSum += block.Size;
                        }
                        else
                        {
                            if (!inFreeRun)
                            {
                                if (inUsedRun)
                                {
                                    blocks.Add((startIndex, usedSum, true));
                                    inUsedRun = false;
                                    usedSum = 0;
                                }

                                inFreeRun = true;
                                freeSum = 0;
                                startIndex = i;
                            }

                            freeSum += block.Size;
                        }
                    }
                    else
                    {
                        if (inUsedRun)
                        {
                            blocks.Add((startIndex, usedSum, true));
                            inUsedRun = false;
                            usedSum = 0;
                        }

                        if (inFreeRun)
                        {
                            blocks.Add((startIndex, freeSum, false));
                            inFreeRun = false;
                            freeSum = 0;
                        }

                        blocks.Add((i, block.Size, block.IsUsed));
                    }
                }

                if (inUsedRun) blocks.Add((startIndex, usedSum, true));
                if (inFreeRun) blocks.Add((startIndex, freeSum, false));
            }

            // Ограничиваем количество отображаемых блоков для производительности
            if (blocks.Count > MAX_DISPLAYED_BLOCKS)
            {
                blocks = blocks.OrderByDescending(b => b.size).Take(MAX_DISPLAYED_BLOCKS).ToList();
            }

            // Сортируем блоки по убыванию размера для лучшей упаковки
            blocks.Sort((a, b) => b.size.CompareTo(a.size));

            // Алгоритм Guillotine Bin Packing: размещаем блоки, минимизируя пустоты
            List<(float x, float y, float width, float height)> placedRects = new();
            float binWidth = containerWidth;
            float binHeight = containerHeight;

            // Рекурсивно делим пространство для плотной упаковки
            PlaceBlocks(blocks, placedRects, binWidth, binHeight, 0, 0);

            // Рисуем блоки
            foreach (var (x, y, width, height) in placedRects)
            {
                int blockIndex = blocks[placedRects.IndexOf((x, y, width, height))].index;
                bool isUsed = blocks[placedRects.IndexOf((x, y, width, height))].isUsed;
                long size = blocks[placedRects.IndexOf((x, y, width, height))].size;

                VisualElement blockElement = new VisualElement();
                blockElement.style.position = Position.Absolute;
                blockElement.style.left = x;
                blockElement.style.top = y;
                blockElement.style.width = width;
                blockElement.style.height = height;

                // Цвет: красный для занятых, зеленый для свободных
                Color blockColor = isUsed
                    ? new Color(1f, 0f, 0f,
                        Mathf.Clamp01(size / (float)SerializableMemoryAllocator.BIG_MEMORY_BLOCK_SIZE))
                    : Color.green;
                blockElement.style.backgroundColor = blockColor;

                // Подпись с номером и размером (для больших блоков)
                if (width >= MIN_BLOCK_SIZE * 2 &&
                    height >= MIN_BLOCK_SIZE * 2) // Показываем подпись только для читаемых блоков
                {
                    Label blockLabel = new Label($"#{blockIndex}\n{size}b");
                    blockLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                    blockLabel.style.color = Color.white;
                    blockLabel.style.fontSize = 12;
                    blockElement.Add(blockLabel);
                }

                mosaicContainer.Add(blockElement);
            }

            // Устанавливаем размер контейнера для прокрутки
            float maxHeight = placedRects.Count > 0 ? placedRects.Max(r => r.y + r.height) : containerHeight;
            mosaicContainer.style.width = containerWidth;
            mosaicContainer.style.height = Mathf.Max(containerHeight, maxHeight);
        }

        private void PlaceBlocks(List<(int index, long size, bool isUsed)> blocks,
            List<(float x, float y, float width, float height)> placedRects, float binWidth, float binHeight, float x,
            float y)
        {
            if (blocks.Count == 0) return;

            // Берем самый большой блок
            var (index, size, isUsed) = blocks[0];
            blocks.RemoveAt(0);

            float blockWidth =
                Mathf.Max(MIN_BLOCK_SIZE, (float)(size * binWidth / allocator.GetMemoryInfo().totalSize));
            float blockHeight = Mathf.Max(MIN_BLOCK_SIZE, blockWidth * 0.5f); // Пропорциональная высота

            // Проверяем, помещается ли блок в текущую область
            if (x + blockWidth <= binWidth && y + blockHeight <= binHeight)
            {
                placedRects.Add((x, y, blockWidth, blockHeight));

                // Делим оставшееся пространство (Guillotine: вертикально или горизонтально)
                bool splitHorizontally = (binWidth - (x + blockWidth)) > (binHeight - (y + blockHeight));

                if (splitHorizontally)
                {
                    // Разделяем по вертикали
                    PlaceBlocks(blocks, placedRects, binWidth, y + blockHeight, x + blockWidth, y);
                    PlaceBlocks(blocks, placedRects, binWidth, binHeight, x, y + blockHeight);
                }
                else
                {
                    // Разделяем по горизонтали
                    PlaceBlocks(blocks, placedRects, x + blockWidth, binHeight, x + blockWidth, y);
                    PlaceBlocks(blocks, placedRects, binWidth, binHeight, x, y + blockHeight);
                }
            }
            else
            {
                // Если не помещается, пробуем разместить в другой области
                PlaceBlocks(blocks, placedRects, binWidth, binHeight, 0, 0);
            }
        }
    }
}
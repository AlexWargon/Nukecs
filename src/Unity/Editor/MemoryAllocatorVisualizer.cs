#if UNITY_EDITOR
using System.Linq;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Wargon.Nukecs
{
    public unsafe class MemoryAllocatorVisualizer : EditorWindow
    {
        private SerializableMemoryAllocator* allocator;
        private Vector2 scrollPosition;
        private int selectedBlockIndex = -1;

        private void OnGUI()
        {
            if (!World.HasActiveWorlds()) return;
            ref var world = ref World.Default;

            if (allocator == null)
            {
                EditorGUILayout.HelpBox("No allocator found. Please assign one to debug.", MessageType.Warning);
                if (GUILayout.Button("Initialize Allocator (Test)"))
                    allocator = world.AllocatorHandler.AllocatorWrapper.GetAllocatorPtr();
                return;
            }

            // Main layout split into two areas
            var visualizationArea = new Rect(0, 0, position.width, position.height * 0.8f);
            var detailsArea = new Rect(0, position.height * 0.8f, position.width, position.height * 0.2f);

            DrawVisualizationArea(visualizationArea);
            DrawDetailsArea(detailsArea);
        }

        private void DrawVisualizationArea(Rect area)
        {
            GUILayout.BeginArea(area);
            GUILayout.Label("Memory Blocks Visualization", EditorStyles.boldLabel);

            // Scrollable область для блоков
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Width(area.width),
                GUILayout.Height(area.height));

            var memoryView = allocator->GetMemoryView();
            float totalMemorySize = 10_000_000;

            // Константы
            const float blockPadding = 2f;
            const float minBlockSize = 10f;
            const float maxBlockSize = 240f;
            // Доступные размеры
            var maxWindowWidth = Screen.width - 40f;
            var sizeMulti =  maxWindowWidth/ area.width * 0.5f;
            var availableWidth = area.width - 20f; // Учитываем скролл
            float currentX = 0;
            float currentY = 0;
            float maxRowHeight = 0;

            if (memoryView.BlockCount > 0 && memoryView.Blocks != null)
            {
                // Сортировка блоков по размеру: от больших к маленьким
                var blocks = new SerializableMemoryAllocator.MemoryBlock[memoryView.BlockCount];

                fixed (void* ptr = blocks)
                {
                    UnsafeUtility.MemCpy(ptr, memoryView.Blocks,
                        memoryView.BlockCount * sizeof(SerializableMemoryAllocator.MemoryBlock));
                }

                blocks = blocks.OrderByDescending(block => block.Size).ToArray();

                for (var i = 0; i < blocks.Length; i++)
                {
                    ref var block = ref blocks[i];

                    // Рассчитываем размеры блока
                    var blockRatio = block.Size / totalMemorySize ;
                    var blockSize = Mathf.Max(minBlockSize, blockRatio * availableWidth);

                    blockSize = Mathf.Min(blockSize, maxBlockSize);
                    
                    blockSize *= sizeMulti;
                    blockSize = Mathf.Max(blockSize, 10f);
                    // Проверяем, поместится ли блок в текущей строке
                    if (currentX + blockSize > availableWidth)
                    {
                        // Переход на новую строку
                        currentX = 0;
                        currentY += maxRowHeight + blockPadding;
                        maxRowHeight = 0;
                    }
                    
                    // Рисуем блок
                    var blockRect = new Rect(currentX, currentY, blockSize, blockSize);
                    DrawBlock(block, blockRect, i, 30);

                    // Обновляем позицию и высоту строки
                    currentX += blockSize + blockPadding;
                    maxRowHeight = Mathf.Max(maxRowHeight, blockSize);
                }
            }
            else
            {
                GUILayout.Label("No memory blocks allocated.");
            }

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        protected override void OnBackingScaleFactorChanged()
        {
            base.OnBackingScaleFactorChanged();
            Repaint();
        }
        
        private void DrawBlock(SerializableMemoryAllocator.MemoryBlock block, Rect blockRect, int index, int minBlockSize)
        {
            // Определяем цвет блока
            var blockColor = block.IsUsed
                ? Color.Lerp(Color.yellow, Color.red, block.Size / 1_000_000f)
                : Color.green;

            // Рисуем прямоугольник
            EditorGUI.DrawRect(blockRect, blockColor);
            if (blockRect.width > minBlockSize)
            {
                var labelStyle = EditorStyles.whiteLabel;
                if(block.IsUsed) labelStyle.normal.textColor = Color.black;
                // Добавляем текст поверх блока
                GUI.Label(blockRect, $"{block.Size / 1024.0f:F1} KB", labelStyle);
            }

            
            // Обработка кликов
            if (Event.current.type == EventType.MouseDown && blockRect.Contains(Event.current.mousePosition))
            {
                selectedBlockIndex = index;
                Repaint();
            }
        }

        private void DrawDetailsArea(Rect area)
        {
            GUILayout.BeginArea(area);
            GUILayout.Label("Block Details", EditorStyles.boldLabel);

            if (selectedBlockIndex != -1)
            {
                var memoryView = allocator->GetMemoryView();
                ref var block = ref memoryView.Blocks[selectedBlockIndex];

                GUILayout.Label($"Block Index: {selectedBlockIndex}");
                GUILayout.Label($"Size: {block.Size} bytes");
                GUILayout.Label($"Status: {(block.IsUsed ? "Used" : "Free")}");
                GUILayout.Label($"Offset: {block.Pointer}");
            }
            else
            {
                GUILayout.Label("Click on a block to view details.");
            }

            GUILayout.EndArea();
        }

        [MenuItem("Nuke.cs/Memory Allocator Visualizer")]
        public static void ShowWindow()
        {
            var window = GetWindow<MemoryAllocatorVisualizer>("Memory Allocator Visualizer");
            window.Show();
        }
    }
}
#endif
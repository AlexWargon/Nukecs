#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor
{
    // Serializable theme data — loaded from / saved to DashboardTheme.json
    // Edit the JSON file to change colors without recompiling.
    [Serializable]
    public class DashboardThemeData
    {
        [Header("Backgrounds")]
        [Tooltip("Main background — root, center panel, entity table area")]
        public Color BgDark = new Color(17f / 255f, 20f / 255f, 22f / 255f);

        [Tooltip("Panel background — sidebars, top bar, bottom bar, inspector")]
        public Color BgPanel = new Color(19f / 255f, 21f / 255f, 22f / 255f);

        [Tooltip("Card / component card background")]
        public Color BgCard = new Color(28f / 255f, 30f / 255f, 33f / 255f, 1f);

        [Tooltip("Hovered card background")]
        public Color BgCardHover = new Color(38f / 255f, 40f / 255f, 46f / 255f, 1f);

        [Tooltip("Selected entity row / card")]
        public Color BgCardSelected = new Color(50f / 255f, 36f / 255f, 70f / 255f, 0.95f);

        [Header("Accent Colors")]
        public Color AccentPurple = new Color(0xB8 / 255f, 0x4D / 255f, 0xFF / 255f);
        public Color AccentCyan = new Color(0x00 / 255f, 0xF0 / 255f, 0xFF / 255f);
        public Color AccentGreen = new Color(0x39 / 255f, 0xFF / 255f, 0x14 / 255f);
        public Color AccentOrange = new Color(0xFF / 255f, 0x6D / 255f, 0x00 / 255f);
        public Color AccentRed = new Color(0xFF / 255f, 0x2D / 255f, 0x2D / 255f);
        public Color AccentYellow = new Color(0xFF / 255f, 0xE5 / 255f, 0x00 / 255f);
        public Color AccentPink = new Color(0xFF / 255f, 0x2D / 255f, 0x6B / 255f);

        [Header("Text")]
        [Tooltip("Primary text — component names, section titles")]
        public Color TextPrimary = new Color(252f / 255f, 254f / 255f, 255f / 255f);

        [Tooltip("Secondary / dimmed text")]
        public Color TextSecondary = new Color(0x6B / 255f, 0x73 / 255f, 0x94 / 255f);

        public Color TextWhite = Color.white;

        [Header("Borders & Separators")]
        public Color Border = new Color(0x3A / 255f, 0x3F / 255f, 0x5C / 255f, 0.5f);
        public Color BorderGlow = new Color(0xB8 / 255f, 0x4D / 255f, 0xFF / 255f, 0.3f);
        public Color Separator = new Color(38f / 255f, 38f / 255f, 44f / 255f);

        [Header("Buttons")]
        public Color RemoveBtn = new Color(0.8f, 0.267f, 0.267f);
        public Color RemoveBtnHoverBg = new Color(0.8f, 0.267f, 0.267f, 0.2f);

        [Header("Progress Bars")]
        public Color ProgressBarBg = new Color(20f / 255f, 20f / 255f, 24f / 255f);
        public Color ProgressBarFill = new Color(0x39 / 255f, 0xFF / 255f, 0x14 / 255f, 0.8f);
        public Color ProgressBarWarn = new Color(0xFF / 255f, 0x6D / 255f, 0x00 / 255f, 0.8f);
        public Color ProgressBarCritical = new Color(0xFF / 255f, 0x2D / 255f, 0x2D / 255f, 0.8f);

        private static string FilePath => Path.Combine(Application.dataPath, "Nukecs", "DashboardTheme.json");

        public static DashboardThemeData Load()
        {
            var path = FilePath;
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var data = JsonUtility.FromJson<DashboardThemeData>(json);
                    if (data != null) return data;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DashboardTheme] Failed to load theme: {e.Message}");
                }
            }
            var defaults = new DashboardThemeData();
            defaults.Save();
            return defaults;
        }

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var json = JsonUtility.ToJson(this, true);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DashboardTheme] Failed to save theme: {e.Message}");
            }
        }
    }

    // Static facade — all existing code uses DashboardTheme.BgDark etc. unchanged.
    // Call Reload() to re-read the JSON, then recreate the window.
    public static class DashboardTheme
    {
        private static DashboardThemeData _data = DashboardThemeData.Load();

        public static DashboardThemeData Data => _data;

        public static void Reload()
        {
            _data = DashboardThemeData.Load();
        }

        public static void Save()
        {
            _data.Save();
        }

        // Backgrounds
        public static Color BgDark => _data.BgDark;
        public static Color BgPanel => _data.BgPanel;
        public static Color BgCard => _data.BgCard;
        public static Color BgCardHover => _data.BgCardHover;
        public static Color BgCardSelected => _data.BgCardSelected;

        // Accents
        public static Color AccentPurple => _data.AccentPurple;
        public static Color AccentCyan => _data.AccentCyan;
        public static Color AccentGreen => _data.AccentGreen;
        public static Color AccentOrange => _data.AccentOrange;
        public static Color AccentRed => _data.AccentRed;
        public static Color AccentYellow => _data.AccentYellow;
        public static Color AccentPink => _data.AccentPink;

        // Text
        public static Color TextPrimary => _data.TextPrimary;
        public static Color TextSecondary => _data.TextSecondary;
        public static Color TextWhite => _data.TextWhite;

        // Borders & separators
        public static Color Border => _data.Border;
        public static Color BorderGlow => _data.BorderGlow;
        public static Color Separator => _data.Separator;

        // Buttons
        public static Color RemoveBtn => _data.RemoveBtn;
        public static Color RemoveBtnHoverBg => _data.RemoveBtnHoverBg;

        // Progress bars
        public static Color ProgressBarBg => _data.ProgressBarBg;
        public static Color ProgressBarFill => _data.ProgressBarFill;
        public static Color ProgressBarWarn => _data.ProgressBarWarn;
        public static Color ProgressBarCritical => _data.ProgressBarCritical;

        public static class FontSize
        {
            public const int TitleLarge = 16;
            public const int TitleMedium = 14;
            public const int Body = 12;
            public const int Small = 10;
            public const int Micro = 9;
        }

        public static Color GlowColor(Color accent, float intensity)
        {
            return new Color(accent.r, accent.g, accent.b, intensity);
        }

        public static Color AccentForType(string typeName)
        {
            var hash = 0;
            for (var i = 0; i < typeName.Length; i++)
                hash = typeName[i] + ((hash << 5) - hash);
            var hue = ((hash & 0xFF) / 255f) % 1f;
            if (hue < 0) hue += 1f;
            return Color.HSVToRGB(hue, 0.7f, 0.9f);
        }

        public static Color AccentForArchetype(int hashId)
        {
            var hue = ((hashId * 0.618033988749895f) % 1f);
            if (hue < 0) hue += 1f;
            return Color.HSVToRGB(hue, 0.6f, 0.85f);
        }

        public static string ColorToHex(Color c)
        {
            var r = (int)(c.r * 255);
            var g = (int)(c.g * 255);
            var b = (int)(c.b * 255);
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        public static string FormatBytes(long bytes)
        {
            const float KB = 1024f;
            const float MB = 1024f * 1024f;
            if (bytes < 0) return "0 B";
            if (bytes >= MB) return $"{bytes / MB:F2} MB";
            if (bytes >= KB) return $"{bytes / KB:F1} KB";
            return $"{bytes} B";
        }

        public static Color WithAlpha(this Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }
    }

    public static class DashboardStyles
    {
        public static VisualElement SectionTitle(string text, Color color)
        {
            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    paddingLeft = 12,
                    paddingTop = 8,
                    paddingBottom = 4
                }
            };

            var label = new Label(text)
            {
                style =
                {
                    fontSize = DashboardTheme.FontSize.TitleMedium,
                    color = color,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    letterSpacing = 1
                }
            };
            container.Add(label);

            var underline = new VisualElement
            {
                style =
                {
                    height = 2,
                    backgroundColor = color.WithAlpha(0.6f),
                    marginTop = 3,
                    width = 60
                }
            };
            container.Add(underline);

            var glowLine = new VisualElement
            {
                style =
                {
                    height = 2,
                    backgroundColor = color.WithAlpha(0.15f),
                    marginTop = 0,
                    width = 80
                }
            };
            container.Add(glowLine);

            return container;
        }

        public static Label PillBadge(string text, Color bgColor, Color textColor, float fontSize = 10)
        {
            return new Label(text)
            {
                style =
                {
                    fontSize = fontSize,
                    color = textColor,
                    backgroundColor = bgColor,
                    paddingTop = 3,
                    paddingBottom = 3,
                    paddingLeft = 10,
                    paddingRight = 10,
                    borderTopLeftRadius = 14,
                    borderTopRightRadius = 14,
                    borderBottomLeftRadius = 14,
                    borderBottomRightRadius = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
        }

        public static VisualElement NeonSeparator(Color color)
        {
            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    marginLeft = 8,
                    marginRight = 8
                }
            };

            var line = new VisualElement
            {
                style =
                {
                    height = 1,
                    backgroundColor = color
                }
            };
            container.Add(line);

            var glow = new VisualElement
            {
                style =
                {
                    height = 3,
                    backgroundColor = color.WithAlpha(Mathf.Max(color.a * 0.3f, 0.05f))
                }
            };
            container.Add(glow);

            return container;
        }

        public static VisualElement CreateSearchField(string placeholder, Action<string> onChanged)
        {
            var wrapper = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    flexGrow = 1,
                    backgroundColor = new Color(0.06f, 0.07f, 0.12f),
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8,
                    borderBottomRightRadius = 8,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = new Color(0.2f, 0.22f, 0.35f),
                    borderBottomColor = new Color(0.2f, 0.22f, 0.35f),
                    borderLeftColor = new Color(0.2f, 0.22f, 0.35f),
                    borderRightColor = new Color(0.2f, 0.22f, 0.35f),
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 4,
                    paddingBottom = 4,
                    marginRight = 8,
                    overflow = Overflow.Hidden,
                    position = Position.Relative
                }
            };

            var icon = new Label("\u25CE")
            {
                style =
                {
                    fontSize = 12,
                    color = DashboardTheme.TextSecondary,
                    marginRight = 6,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            wrapper.Add(icon);

            var placeholderLabel = new Label(placeholder)
            {
                name = "search-placeholder",
                style =
                {
                    fontSize = 11,
                    color = DashboardTheme.TextSecondary.WithAlpha(0.5f),
                    position = Position.Absolute,
                    left = 28,
                    top = 4
                }
            };
            wrapper.Add(placeholderLabel);

            var textField = new TextField
            {
                name = "search-input",
                style =
                {
                    fontSize = 11,
                    color = DashboardTheme.TextPrimary,
                    flexGrow = 1,
                    backgroundColor = Color.clear,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    marginLeft = 0,
                    marginRight = 0,
                    marginTop = 0,
                    marginBottom = 0,
                    paddingLeft = 0,
                    paddingRight = 0,
                    paddingTop = 0,
                    paddingBottom = 0
                }
            };
            textField.Q("unity-text-input").style.backgroundColor = Color.clear;
            textField.Q("unity-text-input").style.borderLeftWidth = 0;
            textField.Q("unity-text-input").style.borderRightWidth = 0;
            textField.Q("unity-text-input").style.borderTopWidth = 0;
            textField.Q("unity-text-input").style.borderBottomWidth = 0;
            textField.Q("unity-text-input").style.marginLeft = 0;
            textField.Q("unity-text-input").style.marginRight = 0;
            textField.Q("unity-text-input").style.paddingLeft = 0;
            textField.Q("unity-text-input").style.paddingRight = 0;

            textField.RegisterValueChangedCallback(evt =>
            {
                onChanged?.Invoke(evt.newValue);
                if (placeholderLabel != null)
                    placeholderLabel.style.display = string.IsNullOrEmpty(evt.newValue)
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            });

            textField.RegisterCallback<FocusEvent>(_ =>
            {
                wrapper.style.borderTopColor = DashboardTheme.AccentCyan.WithAlpha(0.6f);
                wrapper.style.borderBottomColor = DashboardTheme.AccentCyan.WithAlpha(0.6f);
                wrapper.style.borderLeftColor = DashboardTheme.AccentCyan.WithAlpha(0.6f);
                wrapper.style.borderRightColor = DashboardTheme.AccentCyan.WithAlpha(0.6f);
            });
            textField.RegisterCallback<BlurEvent>(_ =>
            {
                wrapper.style.borderTopColor = new Color(0.2f, 0.22f, 0.35f);
                wrapper.style.borderBottomColor = new Color(0.2f, 0.22f, 0.35f);
                wrapper.style.borderLeftColor = new Color(0.2f, 0.22f, 0.35f);
                wrapper.style.borderRightColor = new Color(0.2f, 0.22f, 0.35f);
            });

            wrapper.Add(textField);
            return wrapper;
        }

        public static VisualElement ShineLine()
        {
            return new VisualElement
            {
                style =
                {
                    height = 1,
                    backgroundColor = Color.white.WithAlpha(0.08f),
                    position = Position.Absolute,
                    left = 0, right = 0, top = 0
                }
            };
        }

        public static VisualElement GlowDot(Color color, float size)
        {
            var container = new VisualElement
            {
                style =
                {
                    width = size + 6,
                    height = size + 6,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };

            var outerGlow = new VisualElement
            {
                style =
                {
                    width = size + 6,
                    height = size + 6,
                    borderTopLeftRadius = (size + 6) / 2f,
                    borderTopRightRadius = (size + 6) / 2f,
                    borderBottomLeftRadius = (size + 6) / 2f,
                    borderBottomRightRadius = (size + 6) / 2f,
                    backgroundColor = color.WithAlpha(0.2f),
                    position = Position.Absolute,
                    left = 0, top = 0
                }
            };
            container.Add(outerGlow);

            var dot = new VisualElement
            {
                name = "glow-dot-core",
                style =
                {
                    width = size,
                    height = size,
                    borderTopLeftRadius = size / 2f,
                    borderTopRightRadius = size / 2f,
                    borderBottomLeftRadius = size / 2f,
                    borderBottomRightRadius = size / 2f,
                    backgroundColor = color
                }
            };
            container.Add(dot);

            return container;
        }

        public static VisualElement CreateGradientLine(float height = 2f)
        {
            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    height = height,
                    overflow = Overflow.Hidden
                }
            };

            var purple = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    backgroundColor = DashboardTheme.AccentPurple.WithAlpha(0.7f)
                }
            };
            container.Add(purple);

            var cyan = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    backgroundColor = DashboardTheme.AccentCyan.WithAlpha(0.5f)
                }
            };
            container.Add(cyan);

            var fade = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    backgroundColor = DashboardTheme.AccentCyan.WithAlpha(0.15f)
                }
            };
            container.Add(fade);

            return container;
        }
    }
}
#endif

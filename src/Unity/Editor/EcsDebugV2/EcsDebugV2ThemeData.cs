#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    [Serializable]
    public class EcsDebugV2ThemeData
    {
        public Color Background = new Color(0x1A / 255f, 0x1C / 255f, 0x24 / 255f);
        public Color Panel = new Color(0x1F / 255f, 0x21 / 255f, 0x29 / 255f);
        public Color PanelElevated = new Color(0x26 / 255f, 0x27 / 255f, 0x2E / 255f);
        public Color PanelBorder = new Color(0x32 / 255f, 0x34 / 255f, 0x3D / 255f);
        public Color Lime = new Color(0x8F / 255f, 0xD8 / 255f, 0x30 / 255f);
        public Color Orange = new Color(0xF5 / 255f, 0x80 / 255f, 0x0A / 255f);
        public Color Red = new Color(0xD9 / 255f, 0x26 / 255f, 0x26 / 255f);
        public Color Yellow = new Color(0xF5 / 255f, 0xD8 / 255f, 0x04 / 255f);
        public Color TypeNumber = new Color(0x5C / 255f, 0xC8 / 255f, 0xE6 / 255f);
        public Color TypeString = new Color(0xF5 / 255f, 0x9E / 255f, 0x38 / 255f);
        public Color TypeBool = new Color(0xC0 / 255f, 0x5E / 255f, 0xDB / 255f);
        public Color TypeEntity = new Color(0x8F / 255f, 0xD8 / 255f, 0x30 / 255f);
        public Color MutedText = new Color(0x8A / 255f, 0x8D / 255f, 0x9A / 255f);
        public Color Foreground = new Color(0xDD / 255f, 0xDE / 255f, 0xE3 / 255f);

        public int BorderRadius = 4;
        public int CardRadius = 6;
        public int PaddingH = 8;
        public int PaddingV = 4;
        public int HeaderPaddingH = 10;
        public int HeaderPaddingV = 8;
        public int FontBody = 13;
        public int FontSmall = 11;
        public int FieldName = 12;
        public int FontMicro = 10;
        public int FontMini = 9;
        public bool AdaptiveSkin;
        public Color ForegroundDark = new Color(0.88f, 0.88f, 0.88f, 1f);
        public Color ForegroundLight = new Color(0f, 0f, 0f, 0.85f);
        public Color MutedTextDark = new Color(0.55f, 0.55f, 0.6f, 1f);
        public Color MutedTextLight = new Color(0.4f, 0.4f, 0.4f, 0.7f);

        private static string ThemesDir => Path.Combine(Application.dataPath, "Nukecs", "EcsDebugV2Themes");

        public static EcsDebugV2ThemeData Load(string themeName)
        {
            var path = Path.Combine(ThemesDir, themeName + ".json");
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var data = JsonUtility.FromJson<EcsDebugV2ThemeData>(json);
                    if (data != null) return data;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[EcsDebugV2Theme] Failed to load theme '{themeName}': {e.Message}");
                }
            }
            return new EcsDebugV2ThemeData();
        }

        public void Save(string themeName)
        {
            try
            {
                var dir = ThemesDir;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, themeName + ".json");
                var json = JsonUtility.ToJson(this, true);
                File.WriteAllText(path, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EcsDebugV2Theme] Failed to save theme '{themeName}': {e.Message}");
            }
        }

        public static string[] ListThemeNames()
        {
            var dir = ThemesDir;
            if (!Directory.Exists(dir))
                return new[] { "Default" };
            var files = Directory.GetFiles(dir, "*.json");
            var names = files.Select(f => Path.GetFileNameWithoutExtension(f)).ToArray();
            return names.Length > 0 ? names : new[] { "Default" };
        }

        public static void EnsureBuiltinThemes()
        {
            var dir = ThemesDir;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var defaultPath = Path.Combine(dir, "Default.json");
            if (!File.Exists(defaultPath))
                CreateDefaultTheme().Save("Default");

            var unityPath = Path.Combine(dir, "Unity.json");
            if (!File.Exists(unityPath))
                CreateUnityTheme().Save("Unity");

            var monokaiPath = Path.Combine(dir, "Monokai.json");
            if (!File.Exists(monokaiPath))
                CreateMonokaiTheme().Save("Monokai");
        }

        private static EcsDebugV2ThemeData CreateDefaultTheme()
        {
            return new EcsDebugV2ThemeData();
        }

        private static EcsDebugV2ThemeData CreateUnityTheme()
        {
            return new EcsDebugV2ThemeData
            {
                AdaptiveSkin = true,
                BorderRadius = 0,
                CardRadius = 2,
                PaddingH = 8,
                PaddingV = 3,
                HeaderPaddingH = 10,
                HeaderPaddingV = 6,
                FontBody = 12,
                FontSmall = 11,
                FontMicro = 10,
                FontMini = 9,
                Background = new Color(1f, 1f, 1f, 0.03f),
                Panel = new Color(1f, 1f, 1f, 0.08f),
                PanelElevated = new Color(1f, 1f, 1f, 0.12f),
                PanelBorder = new Color(0f, 0f, 0f, 0.15f),
                Lime = new Color(0.25f, 0.55f, 0.88f),
                Orange = new Color(0.96f, 0.50f, 0.04f),
                Red = new Color(0.80f, 0.20f, 0.20f),
                Yellow = new Color(0.70f, 0.58f, 0.04f),
                TypeNumber = new Color(0.15f, 0.45f, 0.72f),
                TypeString = new Color(0.60f, 0.32f, 0.18f),
                TypeBool = new Color(0.50f, 0.18f, 0.58f),
                TypeEntity = new Color(0.25f, 0.55f, 0.88f),
                MutedText = new Color(0.5f, 0.5f, 0.5f, 0.7f),
                Foreground = new Color(0f, 0f, 0f, 0.85f),
                ForegroundDark = new Color(0.88f, 0.88f, 0.88f, 1f),
                ForegroundLight = new Color(0f, 0f, 0f, 0.85f),
                MutedTextDark = new Color(0.55f, 0.55f, 0.6f, 1f),
                MutedTextLight = new Color(0.4f, 0.4f, 0.4f, 0.7f)
            };
        }

        private static EcsDebugV2ThemeData CreateMonokaiTheme()
        {
            return new EcsDebugV2ThemeData
            {
                BorderRadius = 4,
                CardRadius = 6,
                PaddingH = 8,
                PaddingV = 4,
                HeaderPaddingH = 10,
                HeaderPaddingV = 8,
                FontBody = 13,
                FontSmall = 11,
                FontMicro = 10,
                FontMini = 9,
                Background = new Color(0.153f, 0.157f, 0.133f),
                Panel = new Color(0.243f, 0.239f, 0.196f),
                PanelElevated = new Color(0.286f, 0.282f, 0.243f),
                PanelBorder = new Color(0.341f, 0.337f, 0.259f),
                Lime = new Color(0.651f, 0.886f, 0.180f),
                Orange = new Color(0.992f, 0.588f, 0.122f),
                Red = new Color(0.976f, 0.149f, 0.447f),
                Yellow = new Color(0.902f, 0.859f, 0.455f),
                TypeNumber = new Color(0.682f, 0.506f, 1.0f),
                TypeString = new Color(0.902f, 0.859f, 0.455f),
                TypeBool = new Color(0.976f, 0.149f, 0.447f),
                TypeEntity = new Color(0.651f, 0.886f, 0.180f),
                MutedText = new Color(0.459f, 0.443f, 0.369f),
                Foreground = new Color(0.973f, 0.973f, 0.949f),
                ForegroundDark = new Color(0.88f, 0.88f, 0.88f, 1f),
                ForegroundLight = new Color(0f, 0f, 0f, 0.85f),
                MutedTextDark = new Color(0.55f, 0.55f, 0.6f, 1f),
                MutedTextLight = new Color(0.4f, 0.4f, 0.4f, 0.7f)
            };
        }
    }
}
#endif

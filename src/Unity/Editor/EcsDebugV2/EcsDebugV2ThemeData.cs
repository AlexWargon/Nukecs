#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.IO;
using System.Linq;
using UnityEngine;
// ReSharper disable InconsistentNaming

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    [Serializable]
    public class EcsDebugV2ThemeData
    {
        // Schema marker for migration: bump when the built-in palette/metrics change
        // so that stale theme JSON on disk gets regenerated.
        public int SchemaVersion = CurrentSchemaVersion;
        public const int CurrentSchemaVersion = 2;

        // ── Graphite + Amber palette ──────────────────────────────────────────────
        public Color Background = new (0x12 / 255f, 0x14 / 255f, 0x19 / 255f);
        public Color Panel = new (0x1A / 255f, 0x1D / 255f, 0x24 / 255f);
        public Color PanelElevated = new (0x22 / 255f, 0x26 / 255f, 0x2F / 255f);
        // Glass border = translucent white, not a solid color.
        public Color PanelBorder = new Color(1f, 1f, 1f, 0.06f);
        // Amber = primary accent (active tab, selection, primary actions).
        public Color Lime = new (0xE8 / 255f, 0xB2 / 255f, 0x66 / 255f);
        public Color Orange = new (0xE0 / 255f, 0x7A / 255f, 0x4F / 255f);
        public Color Red = new (0xE0 / 255f, 0x55 / 255f, 0x55 / 255f);
        public Color Yellow = new (0xF5 / 255f, 0xD5 / 255f, 0x47 / 255f);
        // Secondary muted lime used for the TypeEntity color only.
        public Color TypeEntitySecondary = new (0x9C / 255f, 0xC7 / 255f, 0x6E / 255f);
        public Color TypeNumber = new (0x6F / 255f, 0xB8 / 255f, 0xD6 / 255f);
        public Color TypeString = new (0xE8 / 255f, 0xB2 / 255f, 0x66 / 255f);
        public Color TypeBool = new (0xB9 / 255f, 0x8F / 255f, 0xD9 / 255f);
        public Color TypeEntity = new (0x9C / 255f, 0xC7 / 255f, 0x6E / 255f);
        public Color MutedText = new (0x7A / 255f, 0x7E / 255f, 0x88 / 255f);
        public Color Foreground = new (0xE4 / 255f, 0xE6 / 255f, 0xEB / 255f);

        // ── Glass metrics ─────────────────────────────────────────────────────────
        public int BorderRadius = 6;
        public int CardRadius = 10;
        public int PaddingH = 10;
        public int PaddingV = 6;
        public int HeaderPaddingH = 12;
        public int HeaderPaddingV = 9;
        public int FontBody = 13;
        public int FontSmall = 11;
        public int FieldName = 12;
        public int FontMicro = 10;
        public int FontMini = 9;
        public int ComponentHeaderHeight = 26;
        public bool AdaptiveSkin;
        public Color ForegroundDark = new (0.88f, 0.88f, 0.88f, 1f);
        public Color ForegroundLight = new (0f, 0f, 0f, 0.85f);
        public Color MutedTextDark = new (0.55f, 0x55 / 255f, 0.6f, 1f);
        public Color MutedTextLight = new (0.4f, 0.4f, 0.4f, 0.7f);

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
                    if (data != null)
                    {
                        // Migrate stale themes whose schema predates the current palette.
                        if (data.SchemaVersion != CurrentSchemaVersion)
                        {
                            var fresh = themeName switch
                            {
                                "Unity" => CreateUnityTheme(),
                                "Monokai" => CreateMonokaiTheme(),
                                _ => CreateDefaultTheme()
                            };
                            fresh.Save(themeName);
                            return fresh;
                        }
                        return data;
                    }
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

            EnsureTheme("Default", CreateDefaultTheme());
            EnsureTheme("Unity", CreateUnityTheme());
            EnsureTheme("Monokai", CreateMonokaiTheme());
        }

        private static void EnsureTheme(string name, EcsDebugV2ThemeData data)
        {
            var path = Path.Combine(ThemesDir, name + ".json");
            if (!File.Exists(path))
            {
                data.Save(name);
                return;
            }
            // Re-save if the on-disk theme is from an older schema.
            try
            {
                var existing = JsonUtility.FromJson<EcsDebugV2ThemeData>(File.ReadAllText(path));
                if (existing == null || existing.SchemaVersion != CurrentSchemaVersion)
                    data.Save(name);
            }
            catch
            {
                data.Save(name);
            }
        }

        private static EcsDebugV2ThemeData CreateDefaultTheme()
        {
            return new EcsDebugV2ThemeData();
        }

        private static EcsDebugV2ThemeData CreateUnityTheme()
        {
            // Light, almost borderless Unity-like skin, retuned for the graphite base.
            return new EcsDebugV2ThemeData
            {
                AdaptiveSkin = true,
                BorderRadius = 4,
                CardRadius = 6,
                PaddingH = 9,
                PaddingV = 5,
                HeaderPaddingH = 11,
                HeaderPaddingV = 7,
                FontBody = 12,
                FontSmall = 11,
                FontMicro = 10,
                FontMini = 9,
                Background = new Color(0f, 0f, 0f, 0.12f),
                Panel = new Color(1f, 1f, 1f, 0.04f),
                PanelElevated = new Color(1f, 1f, 1f, 0.06f),
                PanelBorder = new Color(1f, 1f, 1f, 0.08f),
                Lime = new Color(0xF2 / 255f, 0xC2 / 255f, 0x82 / 255f),
                Orange = new Color(0xF0 / 255f, 0x90 / 255f, 0x60 / 255f),
                Red = new Color(0xF0 / 255f, 0x6A / 255f, 0x6A / 255f),
                Yellow = new Color(0xFA / 255f, 0xDE / 255f, 0x5E / 255f),
                TypeNumber = new Color(0x7F / 255f, 0xC8 / 255f, 0xE2 / 255f),
                TypeString = new Color(0xF2 / 255f, 0xC2 / 255f, 0x82 / 255f),
                TypeBool = new Color(0xC8 / 255f, 0x9F / 255f, 0xE2 / 255f),
                TypeEntity = new Color(0xB0 / 255f, 0xD6 / 255f, 0x84 / 255f),
                TypeEntitySecondary = new Color(0xB0 / 255f, 0xD6 / 255f, 0x84 / 255f),
                MutedText = new Color(0.5f, 0.5f, 0.55f, 0.85f),
                Foreground = new Color(0f, 0f, 0f, 0.85f),
                ForegroundDark = new Color(0.88f, 0.88f, 0.88f, 1f),
                ForegroundLight = new Color(0f, 0f, 0f, 0.85f),
                MutedTextDark = new Color(0.55f, 0x55 / 255f, 0.6f, 1f),
                MutedTextLight = new Color(0.4f, 0.4f, 0.4f, 0.7f)
            };
        }

        private static EcsDebugV2ThemeData CreateMonokaiTheme()
        {
            // Classic Monokai warmth kept as an alternative, retuned for glass surfaces.
            return new EcsDebugV2ThemeData
            {
                BorderRadius = 6,
                CardRadius = 10,
                PaddingH = 10,
                PaddingV = 6,
                HeaderPaddingH = 12,
                HeaderPaddingV = 9,
                FontBody = 13,
                FontSmall = 11,
                FontMicro = 10,
                FontMini = 9,
                Background = new Color(0.153f, 0.157f, 0.133f),
                Panel = new Color(0.243f, 0.239f, 0.196f),
                PanelElevated = new Color(0.286f, 0.282f, 0.243f),
                PanelBorder = new Color(1f, 1f, 1f, 0.06f),
                Lime = new Color(0.992f, 0.78f, 0.22f),
                Orange = new Color(0.992f, 0.588f, 0.122f),
                Red = new Color(0.976f, 0.149f, 0.447f),
                Yellow = new Color(0.902f, 0.859f, 0.455f),
                TypeNumber = new Color(0.682f, 0.506f, 1.0f),
                TypeString = new Color(0.992f, 0.78f, 0.22f),
                TypeBool = new Color(0.976f, 0.149f, 0.447f),
                TypeEntity = new Color(0.651f, 0.886f, 0.180f),
                TypeEntitySecondary = new Color(0.651f, 0.886f, 0.180f),
                MutedText = new Color(0.459f, 0.443f, 0.369f),
                Foreground = new Color(0.973f, 0.973f, 0.949f),
                ForegroundDark = new Color(0.88f, 0.88f, 0.88f, 1f),
                ForegroundLight = new Color(0f, 0f, 0f, 0.85f),
                MutedTextDark = new Color(0.55f, 0x55 / 255f, 0.6f, 1f),
                MutedTextLight = new Color(0.4f, 0.4f, 0.4f, 0.7f)
            };
        }
    }
}
#endif

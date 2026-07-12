#pragma warning disable CS0618
#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wargon.Nukecs.Editor.EcsDebugV2
{
    public static class EcsDebugV2Theme
    {
        private static EcsDebugV2ThemeData _data;
        private static string _currentThemeName;

        private static bool _alphaCacheInit;
        // Primary accent (amber) alpha variants.
        private static Color _amberA01;
        private static Color _amberA03;
        private static Color _amberA05;
        private static Color _amberA08;
        private static Color _amberA012;
        private static Color _orangeA015;
        private static Color _orangeA01;
        private static Color _yellowA015;
        private static Color _yellowA01;
        private static Color _redA01;
        private static Color _redA03;
        private static Color _redA015;
        // Glass helpers: translucent whites used as borders / highlights / hover wash.
        private static Color _glassBorder;
        private static Color _glassBorderStrong;
        private static Color _glassHighlight;
        private static Color _surfaceHover;
        private static Color _surfacePressed;
        private static Color _mutedTextA05;
        private static Color _bgA04;
        private static Color _panelElevatedA04;

        // Legacy lime-alpha fields retained for callers that still reference them;
        // they now alias the amber accent since amber is the primary accent.
        private static Color _limeA01;
        private static Color _limeA03;
        private static Color _limeA05;

        static EcsDebugV2Theme()
        {
            EcsDebugV2ThemeData.EnsureBuiltinThemes();
            _data = EcsDebugV2ThemeData.Load("Default");
            _currentThemeName = "Default";
        }

        public static string CurrentThemeName => _currentThemeName;
        public static string[] AvailableThemes => EcsDebugV2ThemeData.ListThemeNames();

        public static void SwitchTheme(string name)
        {
            _data = EcsDebugV2ThemeData.Load(name);
            _currentThemeName = name;
            _alphaCacheInit = false;
        }

        public static void ReloadCurrentTheme()
        {
            _data = EcsDebugV2ThemeData.Load(_currentThemeName);
            _alphaCacheInit = false;
        }

        public static Color Background => _data.Background;
        public static Color Panel => _data.Panel;
        public static Color PanelElevated => _data.PanelElevated;
        public static Color PanelBorder => _data.PanelBorder;
        // "Lime" exposes the amber primary accent to legacy callers.
        public static Color Lime => _data.Lime;
        // Canonical alias for the primary amber accent.
        public static Color Amber => _data.Lime;
        public static Color Orange => _data.Orange;
        public static Color Red => _data.Red;
        public static Color Yellow => _data.Yellow;
        public static Color TypeNumber => _data.TypeNumber;
        public static Color TypeString => _data.TypeString;
        public static Color TypeBool => _data.TypeBool;
        public static Color TypeEntity => _data.TypeEntity;

        public static Color MutedText => _data.AdaptiveSkin
            ? (EditorGUIUtility.isProSkin ? _data.MutedTextDark : _data.MutedTextLight)
            : _data.MutedText;

        public static Color Foreground => _data.AdaptiveSkin
            ? (EditorGUIUtility.isProSkin ? _data.ForegroundDark : _data.ForegroundLight)
            : _data.Foreground;

        // ── Alpha-variant accessors ───────────────────────────────────────────────
        // Legacy "lime" variants now alias amber so existing callers keep working.
        public static Color LimeA01 { get { EnsureAlphaCache(); return _limeA01; } }
        public static Color LimeA03 { get { EnsureAlphaCache(); return _limeA03; } }
        public static Color LimeA05 { get { EnsureAlphaCache(); return _limeA05; } }

        public static Color AmberA01 { get { EnsureAlphaCache(); return _amberA01; } }
        public static Color AmberA03 { get { EnsureAlphaCache(); return _amberA03; } }
        public static Color AmberA05 { get { EnsureAlphaCache(); return _amberA05; } }
        public static Color AmberA08 { get { EnsureAlphaCache(); return _amberA08; } }
        public static Color AmberA012 { get { EnsureAlphaCache(); return _amberA012; } }
        public static Color OrangeA015 { get { EnsureAlphaCache(); return _orangeA015; } }
        public static Color OrangeA01 { get { EnsureAlphaCache(); return _orangeA01; } }
        public static Color YellowA015 { get { EnsureAlphaCache(); return _yellowA015; } }
        public static Color YellowA01 { get { EnsureAlphaCache(); return _yellowA01; } }
        public static Color RedA01 { get { EnsureAlphaCache(); return _redA01; } }
        public static Color RedA03 { get { EnsureAlphaCache(); return _redA03; } }
        public static Color RedA015 { get { EnsureAlphaCache(); return _redA015; } }

        public static Color GlassBorder { get { EnsureAlphaCache(); return _glassBorder; } }
        public static Color GlassBorderStrong { get { EnsureAlphaCache(); return _glassBorderStrong; } }
        public static Color GlassHighlight { get { EnsureAlphaCache(); return _glassHighlight; } }
        public static Color SurfaceHover { get { EnsureAlphaCache(); return _surfaceHover; } }
        public static Color SurfacePressed { get { EnsureAlphaCache(); return _surfacePressed; } }
        public static Color MutedTextA05 { get { EnsureAlphaCache(); return _mutedTextA05; } }
        public static Color BgA04 { get { EnsureAlphaCache(); return _bgA04; } }
        public static Color PanelElevatedA04 { get { EnsureAlphaCache(); return _panelElevatedA04; } }
        public static Color PanelBorderA04 => GlassBorder;

        private static void EnsureAlphaCache()
        {
            if (_alphaCacheInit) return;
            _alphaCacheInit = true;
            _amberA01 = Amber.WithAlpha(0.10f);
            _amberA03 = Amber.WithAlpha(0.30f);
            _amberA05 = Amber.WithAlpha(0.50f);
            _amberA08 = Amber.WithAlpha(0.08f);
            _amberA012 = Amber.WithAlpha(0.12f);
            _orangeA015 = Orange.WithAlpha(0.15f);
            _orangeA01 = Orange.WithAlpha(0.10f);
            _yellowA015 = Yellow.WithAlpha(0.15f);
            _yellowA01 = Yellow.WithAlpha(0.10f);
            _redA01 = Red.WithAlpha(0.10f);
            _redA03 = Red.WithAlpha(0.30f);
            _redA015 = Red.WithAlpha(0.15f);
            _glassBorder = new Color(1f, 1f, 1f, 0.06f);
            _glassBorderStrong = new Color(1f, 1f, 1f, 0.10f);
            _glassHighlight = new Color(1f, 1f, 1f, 0.04f);
            _surfaceHover = new Color(1f, 1f, 1f, 0.045f);
            _surfacePressed = new Color(1f, 1f, 1f, 0.08f);
            _mutedTextA05 = MutedText.WithAlpha(0.5f);
            _bgA04 = Background.WithAlpha(0.4f);
            _panelElevatedA04 = PanelElevated.WithAlpha(0.4f);
            // Legacy lime aliases → amber (primary accent is now amber).
            _limeA01 = _amberA01;
            _limeA03 = _amberA03;
            _limeA05 = _amberA05;
        }

        public static int FontBody => _data.FontBody;
        public static int FontSmall => _data.FontSmall;
        public static int FontMicro => _data.FontMicro;
        public static int FontMini => _data.FontMini;
        public static int BorderRadius => _data.BorderRadius;
        public static int CardRadius => _data.CardRadius;
        public static int PaddingH => _data.PaddingH;
        public static int PaddingV => _data.PaddingV;
        public static int HeaderPaddingH => _data.HeaderPaddingH;
        public static int HeaderPaddingV => _data.HeaderPaddingV;
        public static int ComponentHeaderHeight => _data.ComponentHeaderHeight;
        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);

        public static class Font
        {
            public static int Body => _data.FontBody;
            public static int Small => _data.FontSmall;
            public static int Micro => _data.FontMicro;
            public static int Mini => _data.FontMini;
            public static int FieldName => _data.FieldName;
        }

        // ── Existing factories, retuned for glass surfaces ─────────────────────────

        public static Button CreateActionBtn(string text, Color color, Action onClick)
        {
            var btn = new Button(onClick)
            {
                text = text,
                style =
                {
                    fontSize = Font.Small,
                    color = color,
                    backgroundColor = color.WithAlpha(0.10f),
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 4,
                    paddingBottom = 4,
                    letterSpacing = 0.5f
                }
            };
            btn.SetupRadius(BorderRadius);
            btn.SetupGlassBorder();
            return btn;
        }

        public static void SetupBorder(this VisualElement el, Color color, int width = 1)
        {
            el.style.borderTopWidth = width;
            el.style.borderBottomWidth = width;
            el.style.borderLeftWidth = width;
            el.style.borderRightWidth = width;
            el.style.borderTopColor = color;
            el.style.borderBottomColor = color;
            el.style.borderLeftColor = color;
            el.style.borderRightColor = color;
        }

        /// <summary>Applies a translucent white "glass" border on all sides, with an
        /// optional brighter top edge to simulate top-down light.</summary>
        public static void SetupGlassBorder(this VisualElement el, bool topStrong = false)
        {
            var top = topStrong ? GlassBorderStrong : GlassBorder;
            el.style.borderTopWidth = 1;
            el.style.borderBottomWidth = 1;
            el.style.borderLeftWidth = 1;
            el.style.borderRightWidth = 1;
            el.style.borderTopColor = top;
            el.style.borderBottomColor = GlassBorder;
            el.style.borderLeftColor = GlassBorder;
            el.style.borderRightColor = GlassBorder;
        }

        public static void SetupRadius(this VisualElement el, float radius)
        {
            el.style.borderTopLeftRadius = radius;
            el.style.borderTopRightRadius = radius;
            el.style.borderBottomLeftRadius = radius;
            el.style.borderBottomRightRadius = radius;
        }

        public static VisualElement CreateRow(FlexDirection dir = FlexDirection.Row)
        {
            return new VisualElement
            {
                style =
                {
                    flexDirection = dir,
                    paddingLeft = PaddingH,
                    paddingRight = PaddingH,
                    paddingTop = PaddingV,
                    paddingBottom = PaddingV,
                    borderBottomWidth = 1,
                    borderBottomColor = GlassBorder
                }
            };
        }

        public static VisualElement CreateHeaderRow()
        {
            return new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = HeaderPaddingH,
                    paddingRight = HeaderPaddingH,
                    paddingTop = HeaderPaddingV,
                    paddingBottom = HeaderPaddingV,
                    backgroundColor = PanelElevated,
                    borderBottomWidth = 1,
                    borderBottomColor = GlassBorder,
                    flexShrink = 0
                }
            };
        }

        public static VisualElement CreateSeparator()
        {
            return new VisualElement
            {
                style =
                {
                    height = 1,
                    backgroundColor = GlassBorder,
                    flexShrink = 0
                }
            };
        }

        public static Label CreateBadge(string text, Color bg, Color fg, int fontSize = 10)
        {
            var label = new Label(text)
            {
                style =
                {
                    fontSize = fontSize,
                    color = fg,
                    backgroundColor = bg,
                    paddingTop = 2,
                    paddingBottom = 2,
                    paddingLeft = 8,
                    paddingRight = 8,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            label.SetupRadius(BorderRadius);
            return label;
        }

        public static VisualElement CreateCard()
        {
            var el = new VisualElement
            {
                style =
                {
                    backgroundColor = Panel,
                    overflow = Overflow.Hidden
                }
            };
            el.SetupGlassBorder();
            el.SetupRadius(CardRadius);
            return el;
        }

        /// <summary>A translucent "glass" surface card: layered panel background with
        /// a subtle highlight and a thin glass border. Use for list cards.</summary>
        public static VisualElement CreateGlassCard()
        {
            var el = new VisualElement
            {
                style =
                {
                    backgroundColor = PanelElevated.WithAlpha(0.55f),
                    overflow = Overflow.Hidden
                }
            };
            el.SetupGlassBorder(true);
            el.SetupRadius(CardRadius);
            return el;
        }

        public static Label CreateFilterTag(string text, bool positive)
        {
            var bgColor = positive ? AmberA01 : RedA01;
            var fgColor = positive ? Amber : Red;
            var borderColor = positive ? AmberA03 : RedA03;
            var label = new Label(text)
            {
                style =
                {
                    fontSize = Font.Micro,
                    color = fgColor,
                    backgroundColor = bgColor,
                    paddingTop = 2,
                    paddingBottom = 2,
                    paddingLeft = 7,
                    paddingRight = 7
                }
            };
            label.SetupRadius(BorderRadius);
            label.SetupBorder(borderColor);
            return label;
        }

        /// <summary>A small rounded chip used for component tags / metadata.</summary>
        public static Label CreatePill(string text, Color color)
        {
            var label = new Label(text)
            {
                style =
                {
                    fontSize = Font.Micro,
                    color = color,
                    backgroundColor = color.WithAlpha(0.10f),
                    paddingTop = 2,
                    paddingBottom = 2,
                    paddingLeft = 8,
                    paddingRight = 8,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            label.SetupRadius(BorderRadius);
            label.SetupGlassBorder();
            return label;
        }

        public static VisualElement CreateGlowDot(Color color, float size)
        {
            var dot = new VisualElement
            {
                style =
                {
                    width = size,
                    height = size,
                    backgroundColor = color
                }
            };
            dot.SetupRadius(size / 2f);
            return dot;
        }

        public static TextField CreateSearchField(string placeholder, Action<string> onChanged)
        {
            var tf = new TextField
            {
                style =
                {
                    fontSize = Font.Small,
                    color = Foreground,
                    flexGrow = 1,
                    backgroundColor = PanelElevated.WithAlpha(0.6f),
                    paddingLeft = 8,
                    paddingRight = 6,
                    paddingTop = 5,
                    paddingBottom = 5,
                    maxWidth = 260
                }
            };
            tf.SetupGlassBorder();
            tf.SetupRadius(BorderRadius);
            tf.Q("unity-text-input").style.backgroundColor = Color.clear;
            tf.Q("unity-text-input").style.borderLeftWidth = 0;
            tf.Q("unity-text-input").style.borderRightWidth = 0;
            tf.Q("unity-text-input").style.borderTopWidth = 0;
            tf.Q("unity-text-input").style.borderBottomWidth = 0;
            tf.Q("unity-text-input").style.paddingLeft = 0;
            tf.Q("unity-text-input").style.paddingRight = 0;
            tf.Q("unity-text-input").style.marginLeft = 0;
            tf.Q("unity-text-input").style.marginRight = 0;
            tf.SetValueWithoutNotify("");
            tf.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            return tf;
        }

        // ── New glass interaction helpers ─────────────────────────────────────────

        /// <summary>Registers a subtle hover background swap. <paramref name="guard"/>
        /// (optional) returns false when the hover effect should be suppressed — e.g.
        /// when the row is the current selection.</summary>
        /// <summary>Registers a subtle hover background swap. <paramref name="guard"/>
        /// (optional) returns true when the hover effect should be suppressed — e.g.
        /// when the row is the current selection, so the amber selection fill is kept.</summary>
        public static void ApplyHover(this VisualElement el, Func<bool> guard = null)
        {
            el.RegisterCallback<MouseEnterEvent>(evt =>
            {
                var r = evt.currentTarget as VisualElement;
                if (r == null) return;
                if (guard != null && guard()) return;
                r.style.backgroundColor = SurfaceHover;
            });
            el.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                var r = evt.currentTarget as VisualElement;
                if (r == null) return;
                if (guard != null && guard()) return;
                r.style.backgroundColor = Color.clear;
            });
        }
    }
}
#endif

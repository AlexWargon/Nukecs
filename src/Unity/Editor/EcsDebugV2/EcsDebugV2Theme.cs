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
        }

        public static void ReloadCurrentTheme()
        {
            _data = EcsDebugV2ThemeData.Load(_currentThemeName);
        }

        public static Color Background => _data.Background;
        public static Color Panel => _data.Panel;
        public static Color PanelElevated => _data.PanelElevated;
        public static Color PanelBorder => _data.PanelBorder;
        public static Color Lime => _data.Lime;
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

        public static Color WithAlpha(this Color c, float a) => new Color(c.r, c.g, c.b, a);

        public static class Font
        {
            public static int Body => _data.FontBody;
            public static int Small => _data.FontSmall;
            public static int Micro => _data.FontMicro;
            public static int Mini => _data.FontMini;
        }

        public static Button CreateActionBtn(string text, Color color, Action onClick)
        {
            var btn = new Button(onClick)
            {
                text = text,
                style =
                {
                    fontSize = Font.Small,
                    color = color,
                    backgroundColor = color.WithAlpha(0.15f),
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 3,
                    paddingBottom = 3,
                    letterSpacing = 1
                }
            };
            btn.SetupRadius(BorderRadius);
            btn.SetupBorder(color.WithAlpha(0.3f));
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
                    borderBottomColor = PanelBorder.WithAlpha(0.4f)
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
                    borderBottomColor = PanelBorder,
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
                    backgroundColor = PanelBorder,
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
            el.SetupBorder(PanelBorder);
            el.SetupRadius(CardRadius);
            return el;
        }

        public static Label CreateFilterTag(string text, bool positive)
        {
            var bgColor = positive ? Lime.WithAlpha(0.1f) : Red.WithAlpha(0.1f);
            var fgColor = positive ? Lime : Red;
            var borderColor = positive ? Lime.WithAlpha(0.3f) : Red.WithAlpha(0.3f);
            var label = new Label(text)
            {
                style =
                {
                    fontSize = Font.Micro,
                    color = fgColor,
                    backgroundColor = bgColor,
                    paddingTop = 1,
                    paddingBottom = 1,
                    paddingLeft = 6,
                    paddingRight = 6
                }
            };
            label.SetupRadius(BorderRadius);
            label.SetupBorder(borderColor);
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
                    backgroundColor = Panel,
                    paddingLeft = 6,
                    paddingRight = 4,
                    paddingTop = 4,
                    paddingBottom = 4,
                    maxWidth = 240
                }
            };
            tf.SetupBorder(PanelBorder);
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
    }
}
#endif

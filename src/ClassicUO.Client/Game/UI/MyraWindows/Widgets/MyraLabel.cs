using System;
using ClassicUO.Assets;
using FontStashSharp.RichText;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.Styles;

namespace ClassicUO.Game.UI.MyraWindows.Widgets;

public class MyraLabel : Label
{
    private readonly TextStyle _style;
    private readonly AlignMode _align;
    private readonly int _fontSizeOffset;
    private readonly bool _usesCustomFontSize;
    private Func<Color> _textColorOverride;

    public MyraLabel() : this(string.Empty, TextStyle.P)
    {
    }

    public MyraLabel(string text, int fontSizeOffset)
    {
        _fontSizeOffset = fontSizeOffset;
        _usesCustomFontSize = true;

        Wrap = true;
        Text = text;
        ApplyCurrentTheme();
    }

    public MyraLabel(string text, TextStyle style, AlignMode align = AlignMode.Left)
    {
        _style = style;
        _align = align;

        Wrap = true;
        Text = text;
        VerticalAlignment = VerticalAlignment.Center;
        ApplyCurrentTheme();
    }

    public virtual void ApplyCurrentTheme()
    {
        if (Stylesheet.Current.LabelStyle.Clone() is not LabelStyle styleSheet)
        {
            return;
        }

        if (_usesCustomFontSize)
        {
            styleSheet.Font = MyraStyle.GetUiFont(_fontSizeOffset);
        }
        else
        {
            ApplyTextStyle(styleSheet);
        }

        ApplyLabelStyle(styleSheet);

        if (_textColorOverride != null)
        {
            TextColor = _textColorOverride();
        }

        HorizontalAlignment = _align switch
        {
            AlignMode.Center => HorizontalAlignment.Center,
            AlignMode.Right => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Left
        };
    }

    public MyraLabel WithThemeTextColor(Func<Color> colorProvider)
    {
        _textColorOverride = colorProvider;
        ApplyCurrentTheme();
        return this;
    }

    private void ApplyTextStyle(LabelStyle styleSheet)
    {
        switch (_style)
        {
            case TextStyle.H1:
                styleSheet.Font = MyraStyle.GetUiFont(6);
                styleSheet.TextColor = MyraStyle.TextHighlightColor;
                break;
            case TextStyle.H2:
                styleSheet.Font = MyraStyle.GetUiFont(4);
                styleSheet.TextColor = MyraStyle.TextHighlightColor;
                break;
            case TextStyle.H3:
                styleSheet.Font = MyraStyle.GetUiFont(2);
                styleSheet.TextColor = MyraStyle.TextHighlightColor;
                styleSheet.Padding = new Thickness(4, 2);
                break;
            case TextStyle.H4:
                styleSheet.Font = MyraStyle.GetUiFont(0);
                styleSheet.Padding = new Thickness(3, 1);
                break;
            case TextStyle.H5:
                styleSheet.Font = MyraStyle.GetUiFont(-2);
                styleSheet.Padding = new Thickness(3, 1);
                break;
            case TextStyle.H6:
                styleSheet.Font = MyraStyle.GetUiFont(-4);
                styleSheet.Padding = new Thickness(2, 0);
                break;
            case TextStyle.TableHeader:
                styleSheet.Font = MyraStyle.GetUiFont(-2);
                styleSheet.TextColor = MyraStyle.TextHighlightColor;
                styleSheet.Padding = new Thickness(4, 0);
                styleSheet.Margin = new Thickness(2, 0);
                break;
            case TextStyle.P:
            default:
                styleSheet.Font = MyraStyle.UiFont;
                styleSheet.Padding = new Thickness(4, 2);
                break;
        }
    }

    /// <summary>
    /// Default size for a symbol glyph. Matches the property grid's expander and reset marks, which
    /// is what most glyphs sit beside.
    /// </summary>
    public const int SymbolFontSize = 24;

    /// <summary>
    /// A glyph from Noto Sans Symbols 2 - arrows, ticks, padlocks and the like, none of which the
    /// body font carries. Asking for one of those code points in the UI font renders nothing at all,
    /// so this is the only correct way to draw them.
    /// </summary>
    /// <param name="glyph">The character to draw.</param>
    /// <param name="size">Point size; also what the vertical nudge is derived from.</param>
    /// <param name="color">Optional colour; the label's own when omitted.</param>
    /// <returns>The label, centred in its cell.</returns>
    public static MyraLabel Symbol(string glyph, int size = SymbolFontSize, Color? color = null)
    {
        // The symbol font's ascent leaves more room above a glyph than below it, so every one of
        // them sits high in its line box unless nudged down. Proportional to the size, since the gap
        // scales with it.
        var label = new MyraLabel(glyph, size)
        {
            Font = TrueTypeLoader.Instance.GetFont(EmbeddedFontNames.NOTO_SANS_2_SYMBOLS, size),
            Wrap = false,
            SingleLine = true,
            TextAlign = TextHorizontalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Top = Math.Max(1, size / 12)
        };

        if (color != null)
            label.TextColor = color.Value;

        return label;
    }

    public enum TextStyle
    {
        H1,
        H2,
        H3,
        H4,
        H5,
        H6,
        P,
        TableHeader
    }

    public enum AlignMode
    {
        Left,
        Center,
        Right
    }
}

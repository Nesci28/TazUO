using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.UI.MyraWindows.Theme;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.TextureAtlases;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.Styles;

namespace ClassicUO.Game.UI.MyraWindows;

public static class MyraStyle
{
    public const int STANDARD_SPACING = 4;
    public const int STANDARD_BORDER_ALPHA = 150;

    public static Color WindowTitleBackgroundColor => _palette.WindowTitleBackground;
    public static Color SurfaceColor => _palette.Surface;
    public static Color SurfaceMutedColor => _palette.SurfaceMuted;
    public static Color SurfaceRaisedColor => _palette.SurfaceRaised;
    public static Color SurfaceInputColor => _palette.SurfaceInput;
    public static Color SurfaceFocusedColor => _palette.SurfaceFocused;
    public static Color BorderColor => _palette.Border;
    public static Color BorderSoftColor => _palette.BorderSoft;
    public static Color AccentColor => _palette.Accent;
    public static Color AccentHoverColor => _palette.AccentHover;
    public static Color AccentPressedColor => _palette.AccentPressed;
    public static Color TextColor => _palette.Text;
    public static Color TextMutedColor => _palette.TextMuted;
    public static Color TextHighlightColor => _palette.TextHighlight;
    public static Color DangerColor => _palette.Danger;
    public static Color DangerHoverColor => _palette.DangerHover;
    public static Color DangerPressedColor => _palette.DangerPressed;
    public static Color DangerBorderColor => _palette.DangerBorder;
    public static Color ScriptRunningBackgroundColor => _palette.ScriptRunningBackground;
    public static Color ScriptGlobalAutoStartColor => _palette.ScriptGlobalAutoStart;
    public static Color ScriptCharacterAutoStartColor => _palette.ScriptCharacterAutoStart;
    public static Color TableHeaderBackgroundColor => _palette.TableHeaderBackground;
    public static Color TableOddRowBackgroundColor => _palette.TableOddRowBackground;
    public static Color TableEvenRowBackgroundColor => _palette.TableEvenRowBackground;
    public static Color TableSelectedRowBackgroundColor => _palette.TableSelectedRowBackground;
    public static Color GridBorderColor => BorderColor;

    public static SpriteFontBase UiFont => _uiFont;
    public static int UiFontSize => ProfileManager.CurrentProfile == null ? 16 : ProfileManager.CurrentProfile.OptionsFontSize;
    public static SpriteFontBase GetUiFont(int sizeOffset) =>
        TrueTypeLoader.Instance.GetFont(
            ProfileManager.CurrentProfile == null ? EmbeddedFontNames.IBM_PLEX : ProfileManager.CurrentProfile.OptionsFont,
            UiFontSize + sizeOffset
        );

    private static SpriteFontBase _uiFont;
    private static NinePatchRegion _ninePatchPanel;
    public static NinePatchRegion NinePatchButtonUp;
    public static NinePatchRegion NinePatchButtonDown;
    private static NinePatchRegion _ninePatchButtonDangerUp;
    private static NinePatchRegion _ninePatchButtonDangerDown;
    private static TextureRegion _skillUpButton;
    private static TextureRegion _skillDownButton;
    private static TextureRegion _skillLockBtn;
    private static ButtonStyle _lastUsedNavigationButtonStylesheet;
    private static ButtonStyle _navigationButtonStyle;
    private static ThemePalette _palette = ThemePalette.Original;
    private static readonly ConditionalWeakTable<Button, ThemedButtonMarker> _buttonThemeMarkers = new();

    public static event EventHandler ThemePresetChanged;
    public static ClientGumpThemePreset CurrentThemePreset { get; private set; } = ClientGumpThemePreset.Original;

    public static SolidBrush Brush(Color color) => new(color);
    public static IBrush WindowBackgroundBrush => UsesLegacyChrome ? _ninePatchPanel : CreateSurfaceBrush(ThemedSurface.Window);
    public static IBrush WindowTitleBackgroundBrush => CreateSurfaceBrush(ThemedSurface.Title);
    public static IBrush SurfaceBackgroundBrush => CreateSurfaceBrush(ThemedSurface.Surface);
    public static IBrush SurfaceMutedBackgroundBrush => CreateSurfaceBrush(ThemedSurface.Muted);
    public static IBrush SurfaceRaisedBackgroundBrush => CreateSurfaceBrush(ThemedSurface.Raised);
    public static IBrush SurfaceInputBackgroundBrush => CreateSurfaceBrush(ThemedSurface.Input);
    public static IBrush SurfaceFocusedBackgroundBrush => CreateSurfaceBrush(ThemedSurface.Focused);

    public static void ApplyThemePreset(ClientGumpThemePreset preset)
    {
        preset = NormalizeThemePreset(preset);

        if (CurrentThemePreset == preset)
        {
            SynchronizeUpstreamPalette();
            EnsureDefaultTooltipStyle();
            ApplyDisabledStyling();
            return;
        }

        SetDefault(preset);
        _lastUsedNavigationButtonStylesheet = null;
        _navigationButtonStyle = null;
        ThemePresetChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void SetDefault(ClientGumpThemePreset preset = ClientGumpThemePreset.Original)
    {
        preset = NormalizeThemePreset(preset);
        CurrentThemePreset = preset;
        _palette = GetThemePalette(preset);
        SynchronizeUpstreamPalette();

        _ninePatchPanel = new NinePatchRegion(
            ModernUIConstants.ModernUIPanel,
            ModernUIConstants.ModernUIPanel.Bounds,
            new Thickness(ModernUIConstants.ModernUIPanel_BorderSize)
        );
        NinePatchButtonUp = new NinePatchRegion(
            ModernUIConstants.ModernUIButtonUp,
            ModernUIConstants.ModernUIButtonUp.Bounds,
            new Thickness(ModernUIConstants.ModernUIButton_BorderSize)
        );
        NinePatchButtonDown = new NinePatchRegion(
            ModernUIConstants.ModernUIButtonDown,
            ModernUIConstants.ModernUIButtonDown.Bounds,
            new Thickness(ModernUIConstants.ModernUIButton_BorderSize)
        );
        _ninePatchButtonDangerUp = new NinePatchRegion(
            ModernUIConstants.ModernUIButtonDangerUp,
            ModernUIConstants.ModernUIButtonDangerUp.Bounds,
            new Thickness(ModernUIConstants.ModernUIButton_BorderSize)
        );
        _ninePatchButtonDangerDown = new NinePatchRegion(
            ModernUIConstants.ModernUIButtonDangerDown,
            ModernUIConstants.ModernUIButtonDangerDown.Bounds,
            new Thickness(ModernUIConstants.ModernUIButton_BorderSize)
        );

        _skillUpButton = new TextureRegion(ModernUIConstants.ModernUISkillUp);
        _skillDownButton = new TextureRegion(ModernUIConstants.ModernUISkillDown);
        _skillLockBtn = new TextureRegion(ModernUIConstants.ModernUISkillLock);

        _uiFont = TrueTypeLoader.Instance.GetFont(ProfileManager.CurrentProfile == null ? EmbeddedFontNames.IBM_PLEX : ProfileManager.CurrentProfile.OptionsFont, UiFontSize);

        if (TryGetDefaultStyle(Stylesheet.Current.WindowStyles, out WindowStyle windowStyle))
        {
            windowStyle.Background = WindowBackgroundBrush;
            windowStyle.Border = UsesLegacyChrome ? null : Brush(BorderColor);
            windowStyle.BorderThickness = UsesLegacyChrome ? new Thickness(0) : new Thickness(1);
            windowStyle.Padding = UsesLegacyChrome ? new Thickness(6) : new Thickness(8);

            if (windowStyle.TitleStyle != null)
            {
                windowStyle.TitleStyle.Font = TrueTypeLoader.Instance.GetFont(EmbeddedFontNames.IBM_PLEX, 18);
                windowStyle.TitleStyle.TextColor = TextHighlightColor;
                windowStyle.TitleStyle.DisabledTextColor = TextMutedColor;
                windowStyle.TitleStyle.Padding = UsesLegacyChrome ? new Thickness(3) : new Thickness(4, 3);
            }
        }

        if (TryGetDefaultStyle(Stylesheet.Current.LabelStyles, out LabelStyle labelStyle))
        {
            labelStyle.Font = _uiFont;
            labelStyle.TextColor = TextColor;
            labelStyle.DisabledTextColor = TextMutedColor;
            labelStyle.OverTextColor = TextHighlightColor;
            labelStyle.PressedTextColor = TextHighlightColor;
        }

        EnsureDefaultTooltipStyle();

        if (TryGetDefaultStyle(Stylesheet.Current.TabControlStyles, out TabControlStyle tabControlStyle))
        {
            tabControlStyle.ButtonSpacing = 2;
            tabControlStyle.HeaderSpacing = 2;
            tabControlStyle.ContentStyle ??= new WidgetStyle();
            tabControlStyle.ContentStyle.Background = UsesLegacyChrome ? Brush(Color.Transparent) : SurfaceMutedBackgroundBrush;
            tabControlStyle.ContentStyle.Border = UsesLegacyChrome
                ? Brush(new Color(0, 0, 0, STANDARD_BORDER_ALPHA))
                : Brush(BorderColor);
            tabControlStyle.ContentStyle.BorderThickness = new Thickness(1);
            tabControlStyle.ContentStyle.Padding = UsesLegacyChrome ? new Thickness(0) : new Thickness(4);

            ImageTextButtonStyle tabItemStyle = tabControlStyle.TabItemStyle;
            tabItemStyle.LabelStyle.Font = _uiFont;
            tabItemStyle.LabelStyle.TextColor = TextColor;
            tabItemStyle.LabelStyle.OverTextColor = TextHighlightColor;
            tabItemStyle.LabelStyle.PressedTextColor = TextHighlightColor;
            tabItemStyle.Background = UsesLegacyChrome ? Brush(Color.Transparent) : SurfaceRaisedBackgroundBrush;
            tabItemStyle.OverBackground = Brush(AccentHoverColor);
            tabItemStyle.PressedBackground = Brush(AccentPressedColor);
            tabItemStyle.Border = UsesLegacyChrome
                ? Brush(new Color(0, 0, 0, STANDARD_BORDER_ALPHA))
                : Brush(BorderColor);
            tabItemStyle.OverBorder = UsesLegacyChrome ? null : Brush(BorderSoftColor);
            tabItemStyle.BorderThickness = new Thickness(1, 1, 1, 0);
            tabItemStyle.Margin = new Thickness(1, 0);
            tabItemStyle.Padding = new Thickness(10, 3);
        }

        if (TryGetDefaultStyle(Stylesheet.Current.HorizontalSliderStyles, out SliderStyle sliderStyle))
        {
            sliderStyle.Background = SurfaceInputBackgroundBrush;
            sliderStyle.OverBackground = Brush(AccentHoverColor);
            sliderStyle.FocusedBackground = Brush(AccentHoverColor);
            sliderStyle.Border = Brush(BorderColor);
            sliderStyle.FocusedBorder = Brush(BorderSoftColor);
            sliderStyle.BorderThickness = new Thickness(1);
            sliderStyle.KnobStyle.ImageStyle.Background = Brush(AccentColor);
            sliderStyle.KnobStyle.ImageStyle.OverBackground = Brush(TextHighlightColor);
            sliderStyle.KnobStyle.ImageStyle.FocusedBackground = Brush(TextHighlightColor);
            sliderStyle.KnobStyle.ImageStyle.PressedImage = null;
            sliderStyle.KnobStyle.ImageStyle.Image = null;
            sliderStyle.Width = 110;
            sliderStyle.Height = 20;
        }

        if (TryGetDefaultStyle(Stylesheet.Current.ButtonStyles, out ButtonStyle buttonStyle))
        {
            if (!UsesLegacyChrome)
            {
                buttonStyle.Background = SurfaceRaisedBackgroundBrush;
                buttonStyle.OverBackground = Brush(AccentHoverColor);
                buttonStyle.PressedBackground = Brush(AccentPressedColor);
                buttonStyle.Border = Brush(BorderColor);
                buttonStyle.OverBorder = Brush(BorderSoftColor);
                buttonStyle.BorderThickness = new Thickness(1);
            }
            else
            {
                buttonStyle.Background = NinePatchButtonUp;
                buttonStyle.OverBackground = NinePatchButtonDown;
                buttonStyle.PressedBackground = NinePatchButtonDown;
                buttonStyle.Border = null;
                buttonStyle.OverBorder = null;
                buttonStyle.BorderThickness = new Thickness(0);
            }

            buttonStyle.DisabledBackground = SurfaceMutedBackgroundBrush;
            buttonStyle.MinWidth = 1;
            buttonStyle.MinHeight = 1;
            buttonStyle.Padding = new Thickness(6, 4);
            buttonStyle.LabelStyle.Font = _uiFont;
            buttonStyle.LabelStyle.TextColor = TextColor;
            buttonStyle.LabelStyle.DisabledTextColor = TextMutedColor;
            buttonStyle.LabelStyle.OverTextColor = TextHighlightColor;
            buttonStyle.LabelStyle.PressedTextColor = TextHighlightColor;
        }

        if (TryGetDefaultStyle(Stylesheet.Current.CheckBoxStyles, out ImageTextButtonStyle checkBoxStyle))
        {
            checkBoxStyle.ImageStyle.Image = UsesLegacyChrome
                ? new TextureRegion(ModernUIConstants.ModernUICheckBoxUnChecked)
                : new ThemedCheckBoxImage(false);
            checkBoxStyle.ImageStyle.PressedImage = UsesLegacyChrome
                ? new TextureRegion(ModernUIConstants.ModernUICheckBoxChecked)
                : new ThemedCheckBoxImage(true);
            checkBoxStyle.ImageStyle.OverImage = UsesLegacyChrome
                ? new TextureRegion(ModernUIConstants.ModernUICheckBoxUnChecked)
                : new ThemedCheckBoxImage(false);
            ApplyChoiceButtonTheme(checkBoxStyle);
            ApplyImageTextButtonTextTheme(checkBoxStyle);
            checkBoxStyle.Padding = new Thickness(2, 1);
        }

        if (TryGetDefaultStyle(Stylesheet.Current.RadioButtonStyles, out ImageTextButtonStyle radioButtonStyle))
        {
            radioButtonStyle.ImageStyle.Image = new ThemedRadioImage(false);
            radioButtonStyle.ImageStyle.PressedImage = new ThemedRadioImage(true);
            radioButtonStyle.ImageStyle.OverImage = new ThemedRadioImage(false);
            ApplyChoiceButtonTheme(radioButtonStyle);
            ApplyImageTextButtonTextTheme(radioButtonStyle);
            radioButtonStyle.Padding = new Thickness(2, 1);
            radioButtonStyle.ImageTextSpacing = STANDARD_SPACING;
        }

        if (TryGetDefaultStyle(Stylesheet.Current.TextBoxStyles, out TextBoxStyle inputStyle))
        {
            inputStyle.Background = SurfaceInputBackgroundBrush;
            inputStyle.FocusedBackground = SurfaceFocusedBackgroundBrush;
            inputStyle.Border = Brush(BorderColor);
            inputStyle.FocusedBorder = Brush(AccentColor);
            inputStyle.BorderThickness = new Thickness(1);
            inputStyle.Padding = new Thickness(4, 3);
            inputStyle.Font = _uiFont;
            inputStyle.MessageFont = _uiFont;
            inputStyle.TextColor = TextColor;
            inputStyle.FocusedTextColor = TextHighlightColor;
            inputStyle.DisabledTextColor = TextMutedColor;
            inputStyle.Selection = Brush(AccentPressedColor);
        }

        if (TryGetDefaultStyle(Stylesheet.Current.ScrollViewerStyles, out ScrollViewerStyle scrollViewerStyle))
        {
            scrollViewerStyle.Background = UsesLegacyChrome ? null : SurfaceBackgroundBrush;
            scrollViewerStyle.Border = null;
            scrollViewerStyle.OverBorder = null;
            scrollViewerStyle.DisabledBorder = null;
            scrollViewerStyle.FocusedBorder = null;
            scrollViewerStyle.BorderThickness = new Thickness(0);
            scrollViewerStyle.VerticalScrollBackground = UsesLegacyChrome
                ? new TextureRegion(ModernUIConstants.ModernUIVerticalScrollbar)
                : new ThemedScrollBarImage(false, true);
            scrollViewerStyle.VerticalScrollKnob = UsesLegacyChrome
                ? new TextureRegion(ModernUIConstants.ModernUIVerticalScrollbarKnob)
                : new ThemedScrollBarImage(true, true);
            scrollViewerStyle.HorizontalScrollBackground = UsesLegacyChrome
                ? new TextureRegion(ModernUIConstants.ModernUIHorizontalScrollbar)
                : new ThemedScrollBarImage(false, false);
            scrollViewerStyle.HorizontalScrollKnob = UsesLegacyChrome
                ? new TextureRegion(ModernUIConstants.ModernUIHorizontalScrollbarKnob)
                : new ThemedScrollBarImage(true, false);
        }

        if (TryGetDefaultStyle(Stylesheet.Current.ComboBoxStyles, out ComboBoxStyle comboStyle))
        {
            comboStyle.Padding = new Thickness(4, 3);
            comboStyle.Background = SurfaceInputBackgroundBrush;
            comboStyle.OverBackground = Brush(AccentHoverColor);
            comboStyle.PressedBackground = Brush(AccentPressedColor);
            comboStyle.Border = Brush(BorderColor);
            comboStyle.OverBorder = Brush(BorderSoftColor);
            comboStyle.LabelStyle.Font = _uiFont;
            comboStyle.LabelStyle.TextColor = TextColor;
            comboStyle.LabelStyle.OverTextColor = TextHighlightColor;
            comboStyle.LabelStyle.PressedTextColor = TextHighlightColor;
            ApplyListBoxTheme(comboStyle.ListBoxStyle);
        }

        if (TryGetDefaultStyle(Stylesheet.Current.ListBoxStyles, out ListBoxStyle listBoxStyle))
        {
            ApplyListBoxTheme(listBoxStyle);
        }

        if (TryGetDefaultStyle(Stylesheet.Current.VerticalMenuStyles, out MenuStyle menuStyle))
        {
            menuStyle.Padding = new Thickness(2);
            menuStyle.Margin = new Thickness(0);
            menuStyle.Background = SurfaceBackgroundBrush;
            menuStyle.Border = Brush(AccentColor);
            menuStyle.BorderThickness = new Thickness(1);
            menuStyle.SelectionBackground = Brush(AccentPressedColor);
            menuStyle.SelectionHoverBackground = Brush(AccentHoverColor);
            menuStyle.SpecialCharColor = TextHighlightColor;
            menuStyle.LabelStyle.Font = _uiFont;
            menuStyle.LabelStyle.TextColor = TextColor;
            menuStyle.LabelStyle.OverTextColor = TextHighlightColor;
            menuStyle.LabelStyle.PressedTextColor = TextHighlightColor;
            menuStyle.LabelStyle.Margin = new Thickness(2);
            menuStyle.ShortcutStyle.Font = _uiFont;
            menuStyle.ShortcutStyle.TextColor = TextMutedColor;
        }

        // PropertyGrid uses TreeStyle too. Keep upstream's typography and disabled-state behavior,
        // but source selection chrome from the active user-selectable preset.
        TreeStyle treeStyle = Stylesheet.Current.TreeStyle;
        treeStyle.LabelStyle ??= new LabelStyle();
        treeStyle.LabelStyle.Font = _uiFont;
        treeStyle.SelectionBackground = Brush(AccentPressedColor);
        treeStyle.SelectionHoverBackground = Brush(AccentHoverColor);

        // Last: fill only stylesheet gaps, so explicit per-preset disabled styling above wins.
        ApplyDisabledStyling();
    }

    public static bool UsesLegacyChrome => CurrentThemePreset == ClientGumpThemePreset.Original;

    private static IBrush CreateSurfaceBrush(ThemedSurface surface)
    {
        Color color = surface switch
        {
            ThemedSurface.Window => SurfaceColor,
            ThemedSurface.Title => WindowTitleBackgroundColor,
            ThemedSurface.Surface => SurfaceColor,
            ThemedSurface.Muted => SurfaceMutedColor,
            ThemedSurface.Raised => SurfaceRaisedColor,
            ThemedSurface.Input => SurfaceInputColor,
            ThemedSurface.Focused => SurfaceFocusedColor,
            _ => SurfaceColor
        };

        return Brush(color);
    }

    private static bool TryGetDefaultStyle<T>(Dictionary<string, T> styles, out T style) where T : WidgetStyle
    {
        return styles.TryGetValue(Stylesheet.DefaultStyleName, out style);
    }

    private static void EnsureDefaultTooltipStyle()
    {
        if (Stylesheet.Current.TooltipStyles.ContainsKey(Stylesheet.DefaultStyleName))
        {
            return;
        }

        Stylesheet.Current.TooltipStyles[Stylesheet.DefaultStyleName] =
            TryGetDefaultStyle(Stylesheet.Current.LabelStyles, out LabelStyle labelStyle)
                ? new LabelStyle(labelStyle)
                : new LabelStyle();
    }

    /// <summary>
    /// Projects the selectable preset into upstream's semantic palette so newly introduced widgets
    /// follow the same theme without depending on this branch's broader chrome palette.
    /// </summary>
    private static void SynchronizeUpstreamPalette()
    {
        MyraTheme.Current = new MyraPalette
        {
            Name = CurrentThemePreset.ToString(),
            PanelFill = SurfaceMutedColor,
            PanelBorder = BorderColor,
            Notice = AccentColor,
            NoticeBorderAlpha = 0.35f,
            ModifiedValue = AccentColor,
            NestingFills = [SurfaceMutedColor, SurfaceRaisedColor, SurfaceInputColor],
            NestingBorders = [BorderSoftColor, BorderColor, AccentColor],
            DisabledText = TextMutedColor,
            DisabledFill = SurfaceMutedColor
        };
    }

    /// <summary>Fills stylesheet gaps for disabled controls using upstream's semantic palette.</summary>
    private static void ApplyDisabledStyling()
    {
        MyraPalette palette = MyraTheme.Current;
        Stylesheet sheet = Stylesheet.Current;

        if (sheet == null)
            return;

        var disabledFill = new SolidBrush(palette.DisabledFill);

        ApplyDisabledText(sheet.LabelStyle, palette);
        ApplyDisabled(sheet.ButtonStyle, disabledFill, palette);
        ApplyDisabled(sheet.CheckBoxStyle, disabledFill, palette);
        ApplyDisabled(sheet.RadioButtonStyle, disabledFill, palette);
        ApplyDisabled(sheet.ComboBoxStyle, disabledFill, palette);

        if (sheet.TextBoxStyle is { } textBoxStyle)
        {
            textBoxStyle.DisabledBackground ??= disabledFill;
            textBoxStyle.DisabledTextColor ??= palette.DisabledText;
        }

        if (sheet.TreeStyle is { } treeStyle)
        {
            treeStyle.LabelStyle ??= new LabelStyle();
            ApplyDisabledText(treeStyle.LabelStyle, palette);
        }
    }

    private static void ApplyDisabled(ButtonStyle style, IBrush disabledFill, MyraPalette palette)
    {
        if (style == null)
            return;

        style.DisabledBackground ??= disabledFill;
        ApplyDisabledText(style.LabelStyle, palette);
    }

    private static void ApplyDisabledText(LabelStyle style, MyraPalette palette)
    {
        if (style != null)
            style.DisabledTextColor ??= palette.DisabledText;
    }

    private static ClientGumpThemePreset NormalizeThemePreset(ClientGumpThemePreset preset) =>
        preset switch
        {
            ClientGumpThemePreset.Original or
                ClientGumpThemePreset.Dark or
                ClientGumpThemePreset.Light or
                ClientGumpThemePreset.UOCom or
                ClientGumpThemePreset.BritanniaParchment or
                ClientGumpThemePreset.ShadowIron or
                ClientGumpThemePreset.RunebookBlue or
                ClientGumpThemePreset.GuildstoneGreen or
                ClientGumpThemePreset.ClassicStone => preset,
            _ => ClientGumpThemePreset.Original
        };

    private static void ApplyChoiceButtonTheme(ImageTextButtonStyle style)
    {
        if (style == null)
        {
            return;
        }

        if (UsesLegacyChrome)
        {
            style.Background = null;
            style.OverBackground = null;
            style.PressedBackground = null;
            style.DisabledBackground = null;
            style.Border = null;
            style.OverBorder = null;
            style.DisabledBorder = null;
            style.FocusedBorder = null;
            style.BorderThickness = new Thickness(0);

            if (style.ImageStyle != null)
            {
                style.ImageStyle.Background = null;
                style.ImageStyle.OverBackground = null;
                style.ImageStyle.DisabledBackground = null;
                style.ImageStyle.FocusedBackground = null;
                style.ImageStyle.Border = null;
                style.ImageStyle.OverBorder = null;
                style.ImageStyle.DisabledBorder = null;
                style.ImageStyle.FocusedBorder = null;
                style.ImageStyle.BorderThickness = new Thickness(0);
            }

            return;
        }

        style.Background = Brush(Color.Transparent);
        style.OverBackground = Brush(AccentHoverColor);
        style.PressedBackground = Brush(AccentPressedColor);
        style.DisabledBackground = SurfaceMutedBackgroundBrush;
        style.Border = null;
        style.OverBorder = null;
        style.BorderThickness = new Thickness(0);

        if (style.ImageStyle == null)
        {
            return;
        }

        style.ImageStyle.Background = null;
        style.ImageStyle.OverBackground = null;
        style.ImageStyle.DisabledBackground = null;
        style.ImageStyle.FocusedBackground = null;
        style.ImageStyle.Border = null;
        style.ImageStyle.OverBorder = null;
        style.ImageStyle.DisabledBorder = null;
        style.ImageStyle.FocusedBorder = null;
        style.ImageStyle.BorderThickness = new Thickness(0);
    }

    private static void ApplyImageTextButtonTextTheme(ImageTextButtonStyle style)
    {
        if (style?.LabelStyle == null)
        {
            return;
        }

        style.LabelStyle.Font = _uiFont;
        style.LabelStyle.TextColor = TextColor;
        style.LabelStyle.DisabledTextColor = TextMutedColor;
        style.LabelStyle.OverTextColor = TextHighlightColor;
        style.LabelStyle.PressedTextColor = TextHighlightColor;
    }

    private static void ApplyListBoxTheme(ListBoxStyle style)
    {
        if (style == null)
        {
            return;
        }

        style.Background = SurfaceBackgroundBrush;
        style.Border = Brush(BorderColor);
        style.BorderThickness = new Thickness(1);
        style.Padding = new Thickness(2);

        if (style.SeparatorStyle != null)
        {
            style.SeparatorStyle.Background = Brush(BorderSoftColor);
            style.SeparatorStyle.Thickness = 1;
        }

        ImageTextButtonStyle itemStyle = style.ListItemStyle;
        if (itemStyle == null)
        {
            return;
        }

        itemStyle.Background = Brush(Color.Transparent);
        itemStyle.OverBackground = Brush(AccentHoverColor);
        itemStyle.PressedBackground = Brush(AccentPressedColor);
        itemStyle.DisabledBackground = SurfaceMutedBackgroundBrush;
        itemStyle.Padding = new Thickness(3, 2);
        ApplyImageTextButtonTextTheme(itemStyle);
    }

    private sealed class ThemedCheckBoxImage(bool isChecked) : IImage
    {
        private const int GlyphSize = 18;

        public Point Size => new(GlyphSize, GlyphSize);

        public void Draw(RenderContext context, Rectangle dest, Color color)
        {
            Rectangle box = InsetSquare(dest, 2);
            Color border = isChecked ? AccentColor : BorderColor;

            if (isChecked)
            {
                context.FillRectangle(box, AccentColor);
            }
            else
            {
                SurfaceInputBackgroundBrush.Draw(context, box, color);
            }

            context.DrawRectangle(box, border, 1);

            if (!isChecked)
            {
                return;
            }

            float x = box.X;
            float y = box.Y;
            float size = box.Width;
            var checkColor = new Color(255, 255, 255, 255);

            context.DrawLine(
                new Vector2(x + size * 0.25f, y + size * 0.53f),
                new Vector2(x + size * 0.43f, y + size * 0.70f),
                checkColor,
                2f
            );
            context.DrawLine(
                new Vector2(x + size * 0.43f, y + size * 0.70f),
                new Vector2(x + size * 0.76f, y + size * 0.30f),
                checkColor,
                2f
            );
        }
    }

    private sealed class ThemedRadioImage(bool isChecked) : IImage
    {
        private const int GlyphSize = 18;

        public Point Size => new(GlyphSize, GlyphSize);

        public void Draw(RenderContext context, Rectangle dest, Color color)
        {
            Rectangle box = InsetSquare(dest, 2);
            var center = new Vector2(box.X + box.Width / 2f, box.Y + box.Height / 2f);
            float radius = box.Width / 2f;

            context.DrawCircle(center, radius, 24, isChecked ? AccentColor : BorderColor, 1.5f);

            if (isChecked)
            {
                context.DrawCircle(center, radius * 0.45f, 18, AccentColor, radius * 0.45f);
            }
        }
    }

    private sealed class ThemedScrollBarImage(bool isKnob, bool isVertical) : IImage
    {
        private const int Thickness = 12;
        private const int MinimumThumbLength = 36;

        public Point Size => isKnob
            ? isVertical
                ? new Point(Thickness, MinimumThumbLength)
                : new Point(MinimumThumbLength, Thickness)
            : new Point(Thickness, Thickness);

        public void Draw(RenderContext context, Rectangle dest, Color color)
        {
            if (dest.Width <= 0 || dest.Height <= 0)
            {
                return;
            }

            if (!isKnob)
            {
                SurfaceInputBackgroundBrush.Draw(context, dest, color);
                Rectangle track = isVertical
                    ? new Rectangle(dest.X + dest.Width / 2 - 1, dest.Y + 2, 2, Math.Max(0, dest.Height - 4))
                    : new Rectangle(dest.X + 2, dest.Y + dest.Height / 2 - 1, Math.Max(0, dest.Width - 4), 2);
                context.FillRectangle(track, BorderSoftColor);
                return;
            }

            Rectangle knob = isVertical
                ? new Rectangle(dest.X + 3, dest.Y + 2, Math.Max(1, dest.Width - 6), Math.Max(1, dest.Height - 4))
                : new Rectangle(dest.X + 2, dest.Y + 3, Math.Max(1, dest.Width - 4), Math.Max(1, dest.Height - 6));

            context.FillRectangle(knob, AccentColor);
            context.DrawRectangle(knob, BorderSoftColor, 1);
        }
    }

    private static Rectangle InsetSquare(Rectangle dest, int inset)
    {
        int size = Math.Max(0, Math.Min(dest.Width, dest.Height) - inset * 2);
        return new Rectangle(
            dest.X + (dest.Width - size) / 2,
            dest.Y + (dest.Height - size) / 2,
            size,
            size
        );
    }

    private static Color Tint(Color source, Color tint)
    {
        if (tint == Color.White)
        {
            return source;
        }

        return new Color(
            source.R * tint.R / 255,
            source.G * tint.G / 255,
            source.B * tint.B / 255,
            source.A * tint.A / 255
        );
    }

    /// <summary>
    /// Applies standard grid chrome that Myra cannot set through the stylesheet.
    /// </summary>
    public static void ApplyStandardGridStyling(Grid grid)
    {
        grid.Border = Brush(GridBorderColor);
        grid.BorderThickness = new Thickness(1);
        grid.GridLinesColor = GridBorderColor;
        grid.ShowGridLines = true;
        grid.Background = SurfaceMutedBackgroundBrush;
        grid.ColumnSpacing = STANDARD_SPACING;
        grid.RowSpacing = 1;
    }

    public static Button ApplyButtonDangerStyle(Button button)
    {
        MarkButton(button, ThemedButtonKind.Danger);
        button.Background = _ninePatchButtonDangerUp;
        button.OverBackground = _ninePatchButtonDangerDown;
        button.PressedBackground = _ninePatchButtonDangerDown;

        return button;
    }

    public static Button ApplyButtonDestructiveStyle(Button button)
    {
        MarkButton(button, ThemedButtonKind.Destructive);
        button.BorderThickness = new Thickness(0, 0, 0, 3);
        button.Border = Brush(DangerBorderColor);
        button.Background = Brush(DangerColor);
        button.OverBackground = Brush(DangerHoverColor);
        button.PressedBackground = Brush(DangerPressedColor);

        return button;
    }

    public static Button ApplyButtonSelectedStyle(Button button)
    {
        MarkButton(button, ThemedButtonKind.Selected);
        button.BorderThickness = new Thickness(0, 0, 1, 2);
        button.Border = Brush(AccentColor);
        button.Background = Brush(AccentPressedColor);
        button.OverBackground = Brush(AccentHoverColor);
        button.PressedBackground = Brush(AccentPressedColor);

        return button;
    }

    /// <summary>
    /// Gives searchable combo-box popups the active theme's accent border.
    /// </summary>
    public static void ApplySearchComboBoxPopupBorder<T>(SearchableComboBox<T> combo)
    {
        combo.PopupBorder = Brush(AccentColor);
        combo.PopupBorderThickness = new Thickness(1);
    }

    public static bool TryApplyMarkedButtonTheme(Button button)
    {
        if (!_buttonThemeMarkers.TryGetValue(button, out ThemedButtonMarker marker))
        {
            return false;
        }

        switch (marker.Kind)
        {
            case ThemedButtonKind.Danger:
                ApplyButtonDangerStyle(button);
                break;
            case ThemedButtonKind.Destructive:
                ApplyButtonDestructiveStyle(button);
                break;
            case ThemedButtonKind.Selected:
                ApplyButtonSelectedStyle(button);
                break;
        }

        return true;
    }

    private static void MarkButton(Button button, ThemedButtonKind kind)
    {
        _buttonThemeMarkers.Remove(button);
        _buttonThemeMarkers.Add(button, new ThemedButtonMarker(kind));
    }

    public static ButtonBase2 ApplyNavigationButtonStyle(ButtonBase2 button, int minWidth = 125)
    {
        if (!TryGetDefaultStyle(Stylesheet.Current.ButtonStyles, out ButtonStyle buttonStyle))
        {
            return button;
        }

        if (_navigationButtonStyle == null || _lastUsedNavigationButtonStylesheet != buttonStyle)
        {
            _lastUsedNavigationButtonStylesheet = buttonStyle;
            _navigationButtonStyle = new ButtonStyle(_lastUsedNavigationButtonStylesheet)
            {
                Background = SurfaceMutedBackgroundBrush,
                Border = Brush(BorderColor),
                BorderThickness = new Thickness(0, 0, 1, 1),
                LabelStyle =
                {
                    Font = UiFont,
                    TextColor = TextColor,
                    OverTextColor = TextHighlightColor,
                    PressedTextColor = TextHighlightColor
                },
                OverBackground = Brush(AccentHoverColor),
                PressedBackground = Brush(AccentPressedColor),
                MinWidth = minWidth
            };
        }

        button.ApplyButtonStyle(new ButtonStyle(_navigationButtonStyle) { MinWidth = minWidth });
        return button;
    }

    private static ThemePalette GetThemePalette(ClientGumpThemePreset preset) =>
        preset switch
        {
            ClientGumpThemePreset.Dark => new ThemePalette(
                WindowTitleBackground: new Color(8, 9, 10, 245),
                Surface: new Color(8, 9, 10, 245),
                SurfaceMuted: new Color(17, 18, 21, 175),
                SurfaceRaised: new Color(20, 21, 24, 245),
                SurfaceInput: new Color(8, 9, 10, 225),
                SurfaceFocused: new Color(25, 26, 30, 245),
                Border: new Color(44, 46, 52, 215),
                BorderSoft: new Color(63, 66, 74, 175),
                Accent: new Color(94, 106, 210, 255),
                AccentHover: new Color(94, 106, 210, 82),
                AccentPressed: new Color(94, 106, 210, 145),
                Text: new Color(255, 255, 255, 255),
                TextMuted: new Color(138, 143, 152, 255),
                TextHighlight: new Color(255, 255, 255, 255),
                Danger: new Color(229, 72, 77, 185),
                DangerHover: new Color(229, 72, 77, 105),
                DangerPressed: new Color(229, 72, 77, 170),
                DangerBorder: new Color(255, 117, 122, 175),
                ScriptRunningBackground: new Color(48, 164, 108, 220),
                ScriptGlobalAutoStart: new Color(245, 166, 35, 255),
                ScriptCharacterAutoStart: new Color(76, 201, 240, 255),
                TableHeaderBackground: new Color(20, 21, 24, 205),
                TableOddRowBackground: new Color(17, 18, 21, 100),
                TableEvenRowBackground: new Color(8, 9, 10, 70),
                TableSelectedRowBackground: new Color(94, 106, 210, 95)
            ),
            ClientGumpThemePreset.Light => new ThemePalette(
                WindowTitleBackground: new Color(247, 248, 250, 245),
                Surface: new Color(255, 255, 255, 248),
                SurfaceMuted: new Color(247, 248, 248, 218),
                SurfaceRaised: new Color(255, 255, 255, 250),
                SurfaceInput: new Color(255, 255, 255, 245),
                SurfaceFocused: new Color(247, 248, 248, 250),
                Border: new Color(218, 220, 226, 230),
                BorderSoft: new Color(232, 234, 239, 220),
                Accent: new Color(94, 106, 210, 255),
                AccentHover: new Color(94, 106, 210, 30),
                AccentPressed: new Color(94, 106, 210, 58),
                Text: new Color(8, 9, 10, 255),
                TextMuted: new Color(138, 143, 152, 255),
                TextHighlight: new Color(8, 9, 10, 255),
                Danger: new Color(217, 45, 32, 160),
                DangerHover: new Color(217, 45, 32, 42),
                DangerPressed: new Color(217, 45, 32, 78),
                DangerBorder: new Color(196, 50, 50, 180),
                ScriptRunningBackground: new Color(48, 164, 108, 80),
                ScriptGlobalAutoStart: new Color(180, 119, 26, 255),
                ScriptCharacterAutoStart: new Color(37, 99, 235, 255),
                TableHeaderBackground: new Color(247, 248, 248, 235),
                TableOddRowBackground: new Color(247, 248, 248, 128),
                TableEvenRowBackground: new Color(255, 255, 255, 105),
                TableSelectedRowBackground: new Color(94, 106, 210, 45)
            ),
            ClientGumpThemePreset.UOCom => new ThemePalette(
                WindowTitleBackground: new Color(28, 20, 15, 180),
                Surface: new Color(44, 36, 28, 230),
                SurfaceMuted: new Color(44, 36, 28, 115),
                SurfaceRaised: new Color(63, 53, 41, 230),
                SurfaceInput: new Color(24, 20, 16, 150),
                SurfaceFocused: new Color(47, 39, 30, 205),
                Border: new Color(81, 67, 46, 175),
                BorderSoft: new Color(131, 104, 64, 145),
                Accent: new Color(190, 142, 66, 255),
                AccentHover: new Color(190, 142, 66, 95),
                AccentPressed: new Color(190, 142, 66, 165),
                Text: new Color(241, 233, 211, 255),
                TextMuted: new Color(173, 160, 132, 255),
                TextHighlight: new Color(255, 223, 159, 255),
                Danger: new Color(170, 48, 54, 175),
                DangerHover: new Color(205, 58, 68, 110),
                DangerPressed: new Color(128, 34, 42, 210),
                DangerBorder: new Color(220, 86, 92, 150),
                ScriptRunningBackground: new Color(74, 133, 63, 235),
                ScriptGlobalAutoStart: new Color(255, 211, 112, 255),
                ScriptCharacterAutoStart: new Color(88, 182, 210, 255),
                TableHeaderBackground: new Color(63, 53, 41, 175),
                TableOddRowBackground: new Color(44, 36, 28, 80),
                TableEvenRowBackground: new Color(24, 20, 16, 55),
                TableSelectedRowBackground: new Color(190, 142, 66, 85)
            ),
            ClientGumpThemePreset.BritanniaParchment => new ThemePalette(
                WindowTitleBackground: new Color(43, 36, 27, 235),
                Surface: new Color(58, 48, 36, 236),
                SurfaceMuted: new Color(45, 37, 28, 150),
                SurfaceRaised: new Color(74, 59, 42, 236),
                SurfaceInput: new Color(36, 29, 22, 205),
                SurfaceFocused: new Color(71, 57, 37, 230),
                Border: new Color(138, 111, 66, 220),
                BorderSoft: new Color(94, 75, 50, 165),
                Accent: new Color(214, 163, 84, 255),
                AccentHover: new Color(214, 163, 84, 88),
                AccentPressed: new Color(214, 163, 84, 158),
                Text: new Color(241, 230, 200, 255),
                TextMuted: new Color(183, 169, 137, 255),
                TextHighlight: new Color(255, 240, 201, 255),
                Danger: new Color(159, 52, 52, 175),
                DangerHover: new Color(190, 62, 62, 105),
                DangerPressed: new Color(121, 38, 38, 205),
                DangerBorder: new Color(214, 94, 84, 160),
                ScriptRunningBackground: new Color(74, 133, 63, 225),
                ScriptGlobalAutoStart: new Color(228, 179, 87, 255),
                ScriptCharacterAutoStart: new Color(88, 166, 190, 255),
                TableHeaderBackground: new Color(74, 59, 42, 190),
                TableOddRowBackground: new Color(58, 48, 36, 92),
                TableEvenRowBackground: new Color(36, 29, 22, 65),
                TableSelectedRowBackground: new Color(214, 163, 84, 82)
            ),
            ClientGumpThemePreset.ShadowIron => new ThemePalette(
                WindowTitleBackground: new Color(23, 24, 24, 245),
                Surface: new Color(32, 34, 35, 242),
                SurfaceMuted: new Color(25, 27, 28, 165),
                SurfaceRaised: new Color(43, 46, 48, 242),
                SurfaceInput: new Color(18, 20, 21, 222),
                SurfaceFocused: new Color(37, 41, 44, 242),
                Border: new Color(95, 104, 112, 215),
                BorderSoft: new Color(63, 70, 75, 165),
                Accent: new Color(199, 154, 72, 255),
                AccentHover: new Color(199, 154, 72, 86),
                AccentPressed: new Color(199, 154, 72, 155),
                Text: new Color(232, 227, 215, 255),
                TextMuted: new Color(170, 163, 151, 255),
                TextHighlight: new Color(255, 226, 166, 255),
                Danger: new Color(175, 58, 60, 180),
                DangerHover: new Color(212, 70, 72, 105),
                DangerPressed: new Color(130, 42, 45, 210),
                DangerBorder: new Color(222, 92, 90, 160),
                ScriptRunningBackground: new Color(54, 130, 88, 225),
                ScriptGlobalAutoStart: new Color(226, 171, 74, 255),
                ScriptCharacterAutoStart: new Color(92, 174, 190, 255),
                TableHeaderBackground: new Color(43, 46, 48, 205),
                TableOddRowBackground: new Color(32, 34, 35, 105),
                TableEvenRowBackground: new Color(18, 20, 21, 75),
                TableSelectedRowBackground: new Color(199, 154, 72, 85)
            ),
            ClientGumpThemePreset.RunebookBlue => new ThemePalette(
                WindowTitleBackground: new Color(17, 26, 40, 242),
                Surface: new Color(23, 34, 51, 240),
                SurfaceMuted: new Color(16, 24, 36, 165),
                SurfaceRaised: new Color(31, 48, 70, 240),
                SurfaceInput: new Color(13, 20, 32, 220),
                SurfaceFocused: new Color(29, 43, 63, 242),
                Border: new Color(62, 95, 131, 215),
                BorderSoft: new Color(44, 68, 95, 165),
                Accent: new Color(214, 182, 90, 255),
                AccentHover: new Color(214, 182, 90, 82),
                AccentPressed: new Color(214, 182, 90, 150),
                Text: new Color(233, 237, 245, 255),
                TextMuted: new Color(158, 174, 196, 255),
                TextHighlight: new Color(255, 236, 175, 255),
                Danger: new Color(178, 58, 74, 180),
                DangerHover: new Color(216, 70, 90, 105),
                DangerPressed: new Color(132, 42, 58, 210),
                DangerBorder: new Color(228, 94, 104, 160),
                ScriptRunningBackground: new Color(55, 142, 112, 225),
                ScriptGlobalAutoStart: new Color(226, 182, 82, 255),
                ScriptCharacterAutoStart: new Color(86, 183, 210, 255),
                TableHeaderBackground: new Color(31, 48, 70, 205),
                TableOddRowBackground: new Color(23, 34, 51, 105),
                TableEvenRowBackground: new Color(13, 20, 32, 76),
                TableSelectedRowBackground: new Color(214, 182, 90, 80)
            ),
            ClientGumpThemePreset.GuildstoneGreen => new ThemePalette(
                WindowTitleBackground: new Color(23, 33, 24, 242),
                Surface: new Color(34, 48, 33, 240),
                SurfaceMuted: new Color(25, 36, 25, 160),
                SurfaceRaised: new Color(47, 66, 44, 240),
                SurfaceInput: new Color(18, 26, 19, 220),
                SurfaceFocused: new Color(39, 56, 38, 242),
                Border: new Color(96, 112, 68, 215),
                BorderSoft: new Color(68, 82, 52, 165),
                Accent: new Color(213, 182, 91, 255),
                AccentHover: new Color(213, 182, 91, 84),
                AccentPressed: new Color(213, 182, 91, 152),
                Text: new Color(237, 232, 204, 255),
                TextMuted: new Color(169, 178, 143, 255),
                TextHighlight: new Color(255, 238, 180, 255),
                Danger: new Color(165, 54, 50, 180),
                DangerHover: new Color(200, 66, 60, 105),
                DangerPressed: new Color(122, 40, 38, 210),
                DangerBorder: new Color(218, 94, 82, 160),
                ScriptRunningBackground: new Color(70, 138, 72, 225),
                ScriptGlobalAutoStart: new Color(226, 184, 86, 255),
                ScriptCharacterAutoStart: new Color(82, 170, 166, 255),
                TableHeaderBackground: new Color(47, 66, 44, 205),
                TableOddRowBackground: new Color(34, 48, 33, 104),
                TableEvenRowBackground: new Color(18, 26, 19, 74),
                TableSelectedRowBackground: new Color(213, 182, 91, 82)
            ),
            ClientGumpThemePreset.ClassicStone => new ThemePalette(
                WindowTitleBackground: new Color(34, 36, 35, 242),
                Surface: new Color(48, 51, 49, 240),
                SurfaceMuted: new Color(36, 38, 37, 160),
                SurfaceRaised: new Color(59, 64, 62, 240),
                SurfaceInput: new Color(29, 31, 30, 220),
                SurfaceFocused: new Color(53, 57, 55, 242),
                Border: new Color(106, 111, 104, 215),
                BorderSoft: new Color(78, 82, 76, 165),
                Accent: new Color(197, 138, 58, 255),
                AccentHover: new Color(197, 138, 58, 84),
                AccentPressed: new Color(197, 138, 58, 152),
                Text: new Color(236, 231, 215, 255),
                TextMuted: new Color(172, 170, 160, 255),
                TextHighlight: new Color(255, 224, 166, 255),
                Danger: new Color(170, 58, 54, 180),
                DangerHover: new Color(205, 70, 65, 105),
                DangerPressed: new Color(128, 42, 40, 210),
                DangerBorder: new Color(220, 94, 84, 160),
                ScriptRunningBackground: new Color(70, 132, 86, 225),
                ScriptGlobalAutoStart: new Color(218, 156, 66, 255),
                ScriptCharacterAutoStart: new Color(86, 166, 188, 255),
                TableHeaderBackground: new Color(59, 64, 62, 205),
                TableOddRowBackground: new Color(48, 51, 49, 104),
                TableEvenRowBackground: new Color(29, 31, 30, 74),
                TableSelectedRowBackground: new Color(197, 138, 58, 82)
            ),
            _ => ThemePalette.Original
        };

    public static Button ApplySkillButtonStyle(Button button, Lock skillLock)
    {
        var img = new MyraImage()
        {
            Renderable = skillLock switch
            {
                Lock.Up => _skillUpButton,
                Lock.Down => _skillDownButton,
                Lock.Locked => _skillLockBtn,
                _ => _skillLockBtn,
            },
        };

        button.Content = img;
        button.HorizontalAlignment = HorizontalAlignment.Center;
        return button;
    }

    private enum ThemedButtonKind
    {
        Danger,
        Destructive,
        Selected
    }

    private enum ThemedSurface
    {
        Window,
        Title,
        Surface,
        Muted,
        Raised,
        Input,
        Focused
    }

    private sealed class ThemedButtonMarker(ThemedButtonKind kind)
    {
        public ThemedButtonKind Kind { get; } = kind;
    }

    private readonly record struct ThemePalette(
        Color WindowTitleBackground,
        Color Surface,
        Color SurfaceMuted,
        Color SurfaceRaised,
        Color SurfaceInput,
        Color SurfaceFocused,
        Color Border,
        Color BorderSoft,
        Color Accent,
        Color AccentHover,
        Color AccentPressed,
        Color Text,
        Color TextMuted,
        Color TextHighlight,
        Color Danger,
        Color DangerHover,
        Color DangerPressed,
        Color DangerBorder,
        Color ScriptRunningBackground,
        Color ScriptGlobalAutoStart,
        Color ScriptCharacterAutoStart,
        Color TableHeaderBackground,
        Color TableOddRowBackground,
        Color TableEvenRowBackground,
        Color TableSelectedRowBackground
    )
    {
        public static ThemePalette Original { get; } = new(
            WindowTitleBackground: new Color(7, 8, 12, 155),
            Surface: new Color(18, 20, 27, 220),
            SurfaceMuted: new Color(18, 20, 27, 90),
            SurfaceRaised: new Color(31, 35, 46, 220),
            SurfaceInput: new Color(12, 14, 20, 135),
            SurfaceFocused: new Color(20, 23, 32, 190),
            Border: new Color(5, 6, 9, STANDARD_BORDER_ALPHA),
            BorderSoft: new Color(63, 72, 95, 110),
            Accent: new Color(180, 112, 22, 255),
            AccentHover: new Color(180, 112, 22, 90),
            AccentPressed: new Color(180, 112, 22, 165),
            Text: new Color(232, 228, 216, 255),
            TextMuted: new Color(150, 150, 144, 255),
            TextHighlight: new Color(255, 228, 170, 255),
            Danger: new Color(185, 32, 64, 175),
            DangerHover: new Color(220, 42, 80, 110),
            DangerPressed: new Color(145, 22, 48, 210),
            DangerBorder: new Color(240, 80, 110, 150),
            ScriptRunningBackground: new Color(51, 153, 51, 255),
            ScriptGlobalAutoStart: Color.Gold,
            ScriptCharacterAutoStart: new Color(0, 204, 255, 255),
            TableHeaderBackground: new Color(31, 35, 46, 170),
            TableOddRowBackground: new Color(18, 20, 27, 75),
            TableEvenRowBackground: new Color(8, 9, 13, 45),
            TableSelectedRowBackground: new Color(180, 112, 22, 85)
        );
    }
}

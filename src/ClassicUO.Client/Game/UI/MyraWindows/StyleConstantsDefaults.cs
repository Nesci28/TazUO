using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;

using ClassicUO.Game.UI.MyraWindows.Theme;

namespace ClassicUO.Game.UI.MyraWindows;

public static class StyleConstantsDefaults
{
    public static Color ModernUiCorpus => MyraStyle.SurfaceColor;
    public static Color ModernUiBorderDark => MyraStyle.BorderColor;
    public static Color ModernUiBorderLight => MyraStyle.BorderSoftColor;

    public const int WINDOW_MIN_WIDTH = 200;
    public const int WINDOW_MIN_HEIGHT = 200;
    public const int WINDOW_MAX_WIDTH = 1200;
    public const int WINDOW_MAX_HEIGHT = 1200;

    #region Resize Handle

    public const int RESIZE_HANDLE_FONT_SIZE = 20;
    public const string BOTTOM_RIGHT_HANDLE_TEXT = "🭿";
    public const string TOP_RIGHT_HANDLE_TEXT = "🭾";
    public const string TOP_LEFT_HANDLE_TEXT = "🭽";
    public const string BOTTOM_LEFT_HANDLE_TEXT = "🭼";

    #endregion

    /// <summary>
    /// A standard icon for 'reset' type operations.
    /// Must be used with a supported font such as <see cref="ClassicUO.Assets.EmbeddedFontNames.NOTO_SANS_2_SYMBOLS"/>
    /// </summary>
    public const string RESET_LABEL_ICON_TEXT = "⭯";

    /// <summary>
    /// Point size the reset glyph is drawn at inside a <see cref="TOOLBAR_BUTTON_SIZE"/> button.
    /// </summary>
    public const int RESET_ICON_FONT_SIZE = 24;

    public const int TOOLBAR_BUTTON_SIZE = 28;

    #region Inputs

    public const int NUMERIC_INPUT_BOX_WIDTH = 80;

    #endregion

    #region Containers

    public static IBrush BorderBackgroundBrush => MyraStyle.SurfaceMutedBackgroundBrush;
    public static IBrush BorderLineBrush => MyraStyle.Brush(MyraStyle.BorderColor);
    public static Thickness BorderThickness => new(2);

    #endregion
}

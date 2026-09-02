using ClassicUO.Assets;
using ClassicUO.Game.UI.Controls;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps;

internal static class LegionTheme
{
    public const string WindowTexture = "LegionXmlWindow.png";
    public const string PanelTexture = "LegionXmlPanel.png";
    public const string InsetTexture = "LegionXmlInset.png";
    public const string ModernPaperdollTexture = "LegionModernPaperdoll.png";

    public static NineSliceControl CreateWindow(int x, int y, int width, int height) =>
        CreateNineSlice(WindowTexture, 10, x, y, width, height);

    public static NineSliceControl CreatePanel(int x, int y, int width, int height) =>
        CreateNineSlice(PanelTexture, 7, x, y, width, height);

    public static NineSliceControl CreateInset(int x, int y, int width, int height) =>
        CreateNineSlice(InsetTexture, 5, x, y, width, height);

    public static bool TryGetTexture(string name, out Texture2D texture) =>
        ExternalImageLoader.Instance.TryGetEmbeddedTexture(name, out texture)
        && texture != null
        && !texture.IsDisposed;

    private static NineSliceControl CreateNineSlice(
        string textureName,
        int border,
        int x,
        int y,
        int width,
        int height
    )
    {
        if (width <= 0 || height <= 0 || !TryGetTexture(textureName, out Texture2D texture))
        {
            return null;
        }

        int maximumBorder = System.Math.Max(1, System.Math.Min(texture.Width, texture.Height) / 2);
        var control = new NineSliceControl(width, height, texture, System.Math.Clamp(border, 1, maximumBorder))
        {
            X = x,
            Y = y,
            AcceptMouseInput = false,
            CanMove = false
        };

        return control;
    }
}

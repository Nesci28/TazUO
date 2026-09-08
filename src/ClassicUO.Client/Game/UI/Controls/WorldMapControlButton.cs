using System;
using ClassicUO.Assets;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Controls;

internal enum WorldMapControlIcon { FreeView, Center, ZoomIn, ZoomOut, Pathfind }

/// <summary>Small map buttons that consume clicks, including when a zoom limit is reached.</summary>
internal sealed class WorldMapControlButton : Control
{
    internal const int ButtonSize = 24;
    private readonly WorldMapControlIcon _icon;
    private readonly Action _onClick;
    private bool _pressed;

    internal WorldMapControlButton(WorldMapControlIcon icon, Action onClick)
    {
        _icon = icon;
        _onClick = onClick;
        Width = Height = ButtonSize;
        CanMove = false;
        CanCloseWithRightClick = false;
        WantUpdateSize = false;
    }

    // Keep hit testing enabled at zoom limits so a click cannot fall through onto the map.
    internal bool CanActivate { get; set; } = true;
    internal bool IsActive { get; set; }
    public override ClickPriority Priority => ClickPriority.High;

    public override void OnMouseDown(int x, int y, MouseButtonType button)
    {
        if (button == MouseButtonType.Left)
        {
            _pressed = CanActivate;
            Mouse.CancelDoubleClick = true;
        }
    }

    public override void OnMouseUp(int x, int y, MouseButtonType button)
    {
        if (button == MouseButtonType.Left)
        {
            bool activate = _pressed && CanActivate && x >= 0 && y >= 0 && x < Width && y < Height;
            _pressed = false;
            if (activate)
                _onClick();
        }
        else if (button == MouseButtonType.Right && !Keyboard.Alt && !Keyboard.Ctrl && !Keyboard.Shift)
            ((RootParent ?? Parent) as Control)?.ContextMenu?.Show();
    }

    public override bool OnMouseDoubleClick(int x, int y, MouseButtonType button) => true;

    public override bool Draw(UltimaBatcher2D batcher, int x, int y)
    {
        if (IsDisposed || !IsVisible)
            return false;

        Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, Alpha);
        Color background = _pressed && MouseIsOver ? new Color(70, 85, 100, 245)
            : MouseIsOver ? new Color(60, 65, 75, 240)
            : IsActive ? new Color(35, 60, 80, 235) : new Color(25, 28, 33, 230);
        batcher.Draw(SolidColorTextureCache.GetTexture(background), new Rectangle(x, y, Width, Height), hue);
        Color border = IsActive ? new Color(85, 155, 205) : Color.DimGray;
        batcher.DrawRectangle(SolidColorTextureCache.GetTexture(border), x, y, Width - 1, Height - 1, hue);

        string image = _icon switch
        {
            WorldMapControlIcon.FreeView => IsActive ? "map-lock-keyhole-open.png" : "map-lock-keyhole.png",
            WorldMapControlIcon.Center => "map-locate-fixed.png",
            WorldMapControlIcon.ZoomIn => "map-plus.png",
            WorldMapControlIcon.ZoomOut => "map-minus.png",
            WorldMapControlIcon.Pathfind => "map-route.png",
            _ => null
        };
        if (image != null && ExternalImageLoader.Instance.TryGetEmbeddedTexture(image, out var texture))
        {
            Vector3 iconHue = ShaderHueTranslator.GetHueVector(0, false, Alpha * (CanActivate ? 1f : 0.4f));
            batcher.Draw(texture,
                new Rectangle(x + (Width - texture.Width) / 2, y + (Height - texture.Height) / 2, texture.Width, texture.Height),
                iconHue);
        }

        return true;
    }
}

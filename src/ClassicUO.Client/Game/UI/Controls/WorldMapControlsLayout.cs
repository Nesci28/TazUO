using System;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Controls;

internal static class WorldMapControlsLayout
{
    internal const int Gap = 4;

    internal static (Rectangle Actions, Rectangle Zoom, Rectangle Pathfind) Calculate(
        int width, int inset, Point lockSize, Rectangle resizeHandle)
    {
        const int buttonSize = WorldMapControlButton.ButtonSize;
        const int pairSize = buttonSize * 2 + Gap;
        int zoomLeft = width - inset - buttonSize;
        int actionsLeft = Math.Max(inset, lockSize.X + Gap);
        int actionsTop = inset;

        // Keep the modifier-lock hotspot clear even while its icon is hidden.
        // Narrow maps place the action row below it instead of crowding the zoom stack.
        if (actionsLeft + pairSize + Gap > zoomLeft)
        {
            actionsLeft = inset;
            actionsTop = Math.Max(inset, lockSize.Y + Gap);
        }

        var pathfind = new Rectangle(zoomLeft, inset + pairSize + Gap, buttonSize, buttonSize);
        // On the shortest maps, leave the resize handle exposed beside the last button.
        if (pathfind.Intersects(resizeHandle))
            pathfind.X = resizeHandle.Left - buttonSize - Gap;

        return (new Rectangle(actionsLeft, actionsTop, pairSize, buttonSize),
            new Rectangle(zoomLeft, inset, buttonSize, pairSize), pathfind);
    }
}

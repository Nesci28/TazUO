using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Data;

internal static class WorldMapCoordinates
{
    // Shared by coordinate display, copying, markers and targeting so they use the same tile.
    internal static Point CanvasToWorld(Point cursor, Point size, Point center, float zoom, bool flipped)
    {
        float newWidth = size.X / zoom;
        float newHeight = size.Y / zoom;
        float newX = cursor.X / zoom;
        float newY = cursor.Y / zoom;

        if (flipped)
        {
            float nw = (newWidth + newHeight) / 1.41f;
            float nh = (newHeight - newWidth) / 1.41f;
            newWidth = (int)nw;
            newHeight = (int)nh;

            float nx = (newX + newY) / 1.41f;
            float ny = (newY - newX) / 1.41f;
            newX = (int)nx;
            newY = (int)ny;
        }

        return new Point(center.X - (int)(newWidth / 2) + (int)newX,
            center.Y - (int)(newHeight / 2) + (int)newY);
    }
}

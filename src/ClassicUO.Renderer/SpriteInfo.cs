using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Renderer
{
    public struct SpriteInfo
    {
        public Texture2D Texture;
        public Rectangle UV;
        public Point Center;
        /// <summary>
        /// Number of source-image pixels per logical UO pixel. A value of zero is treated as 1
        /// so sprites created by the existing loaders remain backwards compatible.
        /// </summary>
        public int SourceScale;

        public readonly int EffectiveSourceScale => SourceScale > 1 ? SourceScale : 1;
        public readonly int LogicalWidth => UV.Width / EffectiveSourceScale;
        public readonly int LogicalHeight => UV.Height / EffectiveSourceScale;
        public readonly Rectangle LogicalBounds => new Rectangle(0, 0, LogicalWidth, LogicalHeight);
        public readonly float InverseSourceScale => 1f / EffectiveSourceScale;
        public readonly Vector2 DrawScale => new Vector2(InverseSourceScale, InverseSourceScale);

        /// <summary>
        /// Converts a rectangle expressed in logical sprite-local coordinates into the physical
        /// source rectangle stored in the texture atlas.
        /// </summary>
        public readonly Rectangle GetPhysicalSourceRectangle(Rectangle logicalRectangle)
        {
            int scale = EffectiveSourceScale;
            return new Rectangle(
                UV.X + logicalRectangle.X * scale,
                UV.Y + logicalRectangle.Y * scale,
                logicalRectangle.Width * scale,
                logicalRectangle.Height * scale
            );
        }

        public static readonly SpriteInfo Empty = new SpriteInfo { Texture = null };
    }
}

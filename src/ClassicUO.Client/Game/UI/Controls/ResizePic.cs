// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using ClassicUO.Assets;
using ClassicUO.Renderer;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Controls
{
    public class ResizePic : Control
    {
        private int _maxIndex;

        private readonly struct GumpPart
        {
            public GumpPart(SpriteInfo sprite)
            {
                Sprite = sprite;
            }

            public readonly SpriteInfo Sprite;
            public Texture2D Texture => Sprite.Texture;
            public Rectangle SourceBounds => Sprite.UV;
            public Rectangle LogicalBounds => Sprite.LogicalBounds;
            public float DrawScale => Sprite.InverseSourceScale;
        }

        public ResizePic(ushort graphic)
        {
            CanMove = true;
            CanCloseWithRightClick = true;
            Graphic = graphic;

            for (_maxIndex = 0; _maxIndex < 9; ++_maxIndex)
            {
                if (Client.Game.UO.Gumps.GetGump((ushort)(Graphic + _maxIndex)).Texture == null)
                {
                    break;
                }
            }
        }

        public ResizePic(List<string> parts) : this(UInt16Converter.Parse(parts[3]))
        {
            X = int.Parse(parts[1]);
            Y = int.Parse(parts[2]);
            Width = int.Parse(parts[4]);
            Height = int.Parse(parts[5]);
            IsFromServer = true;
        }

        public ushort Graphic { get; }

        public override bool Contains(int x, int y)
        {
            x -= Offset.X;
            y -= Offset.Y;

            GumpPart part0 = GetPart(0);
            GumpPart part1 = GetPart(1);
            GumpPart part2 = GetPart(2);
            GumpPart part3 = GetPart(3);
            GumpPart part4 = GetPart(4);
            GumpPart part5 = GetPart(5);
            GumpPart part6 = GetPart(6);
            GumpPart part7 = GetPart(7);
            GumpPart part8 = GetPart(8);

            Rectangle bounds0 = part0.LogicalBounds;
            Rectangle bounds1 = part1.LogicalBounds;
            Rectangle bounds2 = part2.LogicalBounds;
            Rectangle bounds3 = part3.LogicalBounds;
            Rectangle bounds4 = part4.LogicalBounds;
            Rectangle bounds5 = part5.LogicalBounds;
            Rectangle bounds6 = part6.LogicalBounds;
            Rectangle bounds7 = part7.LogicalBounds;
            Rectangle bounds8 = part8.LogicalBounds;

            int offsetTop = Math.Max(bounds0.Height, bounds2.Height) - bounds1.Height;
            int offsetBottom = Math.Max(bounds5.Height, bounds7.Height) - bounds6.Height;
            int offsetLeft = Math.Abs(Math.Max(bounds0.Width, bounds5.Width) - bounds2.Width);
            int offsetRight = Math.Max(bounds2.Width, bounds7.Width) - bounds4.Width;

            if (PixelsInXY(ref bounds0, Graphic, x, y))
            {
                return true;
            }

            int DW = Width - bounds0.Width - bounds2.Width;

            if (DW >= 1 && PixelsInXY(ref bounds1, (ushort)(Graphic + 1), x - bounds0.Width, y, DW))
            {
                return true;
            }

            if (
                PixelsInXY(
                    ref bounds2,
                    (ushort)(Graphic + 2),
                    x - (Width - bounds2.Width),
                    y - offsetTop
                )
            )
            {
                return true;
            }

            int DH = Height - bounds0.Height - bounds5.Height;

            if (
                DH >= 1
                && PixelsInXY(
                    ref bounds3,
                    (ushort)(Graphic + 3),
                    x /*- offsetLeft*/
                    ,
                    y - bounds0.Height,
                    0,
                    DH
                )
            )
            {
                return true;
            }

            DH = Height - bounds2.Height - bounds7.Height;

            if (
                DH >= 1
                && PixelsInXY(
                    ref bounds4,
                    (ushort)(Graphic + 5),
                    x
                        - (
                            Width - bounds4.Width /*- offsetRight*/
                        ),
                    y - bounds2.Height,
                    0,
                    DH
                )
            )
            {
                return true;
            }

            if (PixelsInXY(ref bounds5, (ushort)(Graphic + 6), x, y - (Height - bounds5.Height)))
            {
                return true;
            }

            DW = Width - bounds5.Width - bounds2.Width;

            if (
                DH >= 1
                && PixelsInXY(
                    ref bounds6,
                    (ushort)(Graphic + 7),
                    x - bounds5.Width,
                    y - (Height - bounds6.Height - offsetBottom),
                    DW
                )
            )
            {
                return true;
            }

            if (
                PixelsInXY(
                    ref bounds7,
                    (ushort)(Graphic + 8),
                    x - (Width - bounds7.Width),
                    y - (Height - bounds7.Height)
                )
            )
            {
                return true;
            }

            DW = Width - bounds0.Width - bounds2.Width;
            DW += offsetLeft + offsetRight;
            DH = Height - bounds2.Height - bounds7.Height;

            if (
                DW >= 1
                && DH >= 1
                && PixelsInXY(
                    ref bounds8,
                    (ushort)(Graphic + 4),
                    x - bounds0.Width,
                    y - bounds0.Height,
                    DW,
                    DH
                )
            )
            {
                return true;
            }

            return false;
        }

        private static bool PixelsInXY(
            ref Rectangle bounds,
            ushort graphic,
            int x,
            int y,
            int width = 0,
            int height = 0
        )
        {
            if (x < 0 || y < 0 || width > 0 && x >= width || height > 0 && y >= height)
            {
                return false;
            }

            if (bounds.Width == 0 || bounds.Height == 0)
            {
                return false;
            }

            int textureWidth = bounds.Width;
            int textureHeight = bounds.Height;

            if (width == 0)
            {
                width = textureWidth;
            }

            if (height == 0)
            {
                height = textureHeight;
            }

            while (x >= textureWidth && width >= textureWidth)
            {
                x -= textureWidth;
                width -= textureWidth;
            }

            if (x < 0 || x > width)
            {
                return false;
            }

            while (y >= textureHeight && height >= textureHeight)
            {
                y -= textureHeight;
                height -= textureHeight;
            }

            if (y < 0 || y > height)
            {
                return false;
            }

            return Client.Game.UO.Gumps.PixelCheck(graphic, x, y);
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            if (batcher.ClipBegin(x, y, Width, Height))
            {
                Vector3 hueVector = ShaderHueTranslator.GetHueVector(0, false, Alpha, true);

                DrawInternal(batcher, x, y, hueVector);
                base.Draw(batcher, x, y);

                batcher.ClipEnd();
            }

            return true;
        }

        private void DrawInternal(UltimaBatcher2D batcher, int x, int y, Vector3 color)
        {
            GumpPart part0 = GetPart(0);
            GumpPart part1 = GetPart(1);
            GumpPart part2 = GetPart(2);
            GumpPart part3 = GetPart(3);
            GumpPart part4 = GetPart(4);
            GumpPart part5 = GetPart(5);
            GumpPart part6 = GetPart(6);
            GumpPart part7 = GetPart(7);
            GumpPart part8 = GetPart(8);

            Rectangle bounds0 = part0.LogicalBounds;
            Rectangle bounds1 = part1.LogicalBounds;
            Rectangle bounds2 = part2.LogicalBounds;
            Rectangle bounds3 = part3.LogicalBounds;
            Rectangle bounds4 = part4.LogicalBounds;
            Rectangle bounds5 = part5.LogicalBounds;
            Rectangle bounds6 = part6.LogicalBounds;
            Rectangle bounds7 = part7.LogicalBounds;
            Rectangle bounds8 = part8.LogicalBounds;

            int offsetTop = Math.Max(bounds0.Height, bounds2.Height) - bounds1.Height;
            int offsetBottom = Math.Max(bounds5.Height, bounds7.Height) - bounds6.Height;
            int offsetLeft = Math.Abs(Math.Max(bounds0.Width, bounds5.Width) - bounds2.Width);
            int offsetRight = Math.Max(bounds2.Width, bounds7.Width) - bounds4.Width;

            if (part0.Texture != null)
            {
                batcher.Draw(
                    part0.Texture,
                    new Rectangle(x, y, bounds0.Width, bounds0.Height),
                    part0.SourceBounds,
                    color
                );
            }

            if (part1.Texture != null)
            {
                batcher.DrawTiled(
                    part1.Texture,
                    new Rectangle(
                        x + bounds0.Width,
                        y,
                        Width - bounds0.Width - bounds2.Width,
                        bounds1.Height
                    ),
                    part1.SourceBounds,
                    color,
                    part1.DrawScale
                );
            }

            if (part2.Texture != null)
            {
                batcher.Draw(
                    part2.Texture,
                    new Rectangle(
                        x + (Width - bounds2.Width),
                        y + offsetTop,
                        bounds2.Width,
                        bounds2.Height
                    ),
                    part2.SourceBounds,
                    color
                );
            }

            if (part3.Texture != null)
            {
                batcher.DrawTiled(
                    part3.Texture,
                    new Rectangle(
                        x,
                        y + bounds0.Height,
                        bounds3.Width,
                        Height - bounds0.Height - bounds5.Height
                    ),
                    part3.SourceBounds,
                    color,
                    part3.DrawScale
                );
            }

            if (part4.Texture != null)
            {
                batcher.DrawTiled(
                    part4.Texture,
                    new Rectangle(
                        x + (Width - bounds4.Width),
                        y + bounds2.Height,
                        bounds4.Width,
                        Height - bounds2.Height - bounds7.Height
                    ),
                    part4.SourceBounds,
                    color,
                    part4.DrawScale
                );
            }

            if (part5.Texture != null)
            {
                batcher.Draw(
                    part5.Texture,
                    new Rectangle(
                        x,
                        y + (Height - bounds5.Height),
                        bounds5.Width,
                        bounds5.Height
                    ),
                    part5.SourceBounds,
                    color
                );
            }

            if (part6.Texture != null)
            {
                batcher.DrawTiled(
                    part6.Texture,
                    new Rectangle(
                        x + bounds5.Width,
                        y + (Height - bounds6.Height - offsetBottom),
                        Width - bounds5.Width - bounds7.Width,
                        bounds6.Height
                    ),
                    part6.SourceBounds,
                    color,
                    part6.DrawScale
                );
            }

            if (part7.Texture != null)
            {
                batcher.Draw(
                    part7.Texture,
                    new Rectangle(
                        x + (Width - bounds7.Width),
                        y + (Height - bounds7.Height),
                        bounds7.Width,
                        bounds7.Height
                    ),
                    part7.SourceBounds,
                    color
                );
            }

            if (part8.Texture != null)
            {
                batcher.DrawTiled(
                    part8.Texture,
                    new Rectangle(
                        x + bounds0.Width,
                        y + bounds0.Height,
                        (Width - bounds0.Width - bounds2.Width) + (offsetLeft + offsetRight),
                        Height - bounds2.Height - bounds7.Height
                    ),
                    part8.SourceBounds,
                    color,
                    part8.DrawScale
                );
            }
        }

        private GumpPart GetPart(int index)
        {
            if (index >= 0 && index <= _maxIndex)
            {
                if (index >= 8)
                {
                    index = 4;
                }
                else if (index >= 4)
                {
                    ++index;
                }

                ref readonly SpriteInfo gumpInfo = ref Client.Game.UO.Gumps.GetGump(
                    (ushort)(Graphic + index)
                );

                return new GumpPart(gumpInfo);
            }

            return default;
        }
    }
}

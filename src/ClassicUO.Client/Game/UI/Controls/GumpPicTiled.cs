// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Renderer;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace ClassicUO.Game.UI.Controls
{
    public class GumpPicTiled : Control
    {
        private ushort _graphic;
        private ushort hue;
        Vector3 hueVector;

        /// <summary>
        /// When true and <see cref="Control.InternalScale"/> is not 1.0, the tiled texture is drawn at
        /// the scaled tile size (so any edge baked into the texture lands on the scaled bounds). Opt-in
        /// so ordinary/server tiled graphics keep tiling at their native size.
        /// </summary>
        public bool ScaleTiledTexture { get; set; }

        public GumpPicTiled(ushort graphic)
        {
            CanMove = true;
            AcceptMouseInput = true;
            Graphic = graphic;
        }

        public GumpPicTiled(int x, int y, int width, int heigth, ushort graphic) : this(graphic)
        {
            X = x;
            Y = y;

            if (width > 0)
            {
                Width = width;
            }

            if (heigth > 0)
            {
                Height = heigth;
            }
        }

        public GumpPicTiled(List<string> parts) : this(UInt16Converter.Parse(parts[5]))
        {
            X = int.Parse(parts[1]);
            Y = int.Parse(parts[2]);
            Width = int.Parse(parts[3]);
            Height = int.Parse(parts[4]);
            IsFromServer = true;
        }

        public ushort Graphic
        {
            get => _graphic;
            set
            {
                if (_graphic != value && value != 0xFFFF)
                {
                    _graphic = value;

                    ref readonly SpriteInfo gumpInfo = ref Client.Game.UO.Gumps.GetGump(_graphic);

                    if (gumpInfo.Texture == null)
                    {
                        Dispose();

                        return;
                    }

                    Width = gumpInfo.LogicalWidth;
                    Height = gumpInfo.LogicalHeight;
                }
            }
        }

        public ushort Hue
        {
            get => hue; set
            {
                hue = value;
                hueVector = ShaderHueTranslator.GetHueVector(value, false, Alpha, true);
            }
        }

        public override void AlphaChanged(float oldValue, float newValue)
        {
            base.AlphaChanged(oldValue, newValue);
            hueVector = ShaderHueTranslator.GetHueVector(Hue, false, newValue, true);
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            if (hueVector == default)
            {
                hueVector = ShaderHueTranslator.GetHueVector(Hue, false, Alpha, true);
            }

            ref readonly SpriteInfo gumpInfo = ref Client.Game.UO.Gumps.GetGump(Graphic);

            if (gumpInfo.Texture != null)
            {
                float tileScale = gumpInfo.InverseSourceScale;
                if (ScaleTiledTexture && InternalScale != 1.0)
                    tileScale *= (float)InternalScale;

                batcher.DrawTiled(
                    gumpInfo.Texture,
                    new Rectangle(x, y, Width, Height),
                    gumpInfo.UV,
                    hueVector,
                    tileScale
                );
            }

            return base.Draw(batcher, x, y);
        }

        public override bool Contains(int x, int y)
        {
            int width = Width;
            int height = Height;

            x -= Offset.X;
            y -= Offset.Y;

            ref readonly SpriteInfo gumpInfo = ref Client.Game.UO.Gumps.GetGump(Graphic);

            if (gumpInfo.Texture == null)
            {
                return false;
            }

            if (width == 0)
            {
                width = gumpInfo.LogicalWidth;
            }

            if (height == 0)
            {
                height = gumpInfo.LogicalHeight;
            }

            while (x > gumpInfo.LogicalWidth && width > gumpInfo.LogicalWidth)
            {
                x -= gumpInfo.LogicalWidth;
                width -= gumpInfo.LogicalWidth;
            }

            while (y > gumpInfo.LogicalHeight && height > gumpInfo.LogicalHeight)
            {
                y -= gumpInfo.LogicalHeight;
                height -= gumpInfo.LogicalHeight;
            }

            if (x > width || y > height)
            {
                return false;
            }

            return Client.Game.UO.Gumps.PixelCheck(Graphic, x, y);
        }
    }
}

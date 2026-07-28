// SPDX-License-Identifier: BSD-2-Clause

using System;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Controls
{
    public class ScrollBar : ScrollBarBase
    {
        private Rectangle _rectSlider,
            _emptySpace;

        const ushort BUTTON_UP_0 = 251;
        const ushort BUTTON_UP_1 = 250;
        const ushort BUTTON_DOWN_0 = 253;
        const ushort BUTTON_DOWN_1 = 252;
        const ushort BACKGROUND_0 = 257;
        const ushort BACKGROUND_1 = 256;
        const ushort BACKGROUND_2 = 255;
        const ushort SLIDER = 254;

        private Vector3 hueVector = ShaderHueTranslator.GetHueVector(0);

        public ScrollBar(int x, int y, int height)
        {
            Height = height;
            Location = new Point(x, y);
            AcceptMouseInput = true;

            ref readonly SpriteInfo gumpInfoUp = ref Client.Game.UO.Gumps.GetGump(BUTTON_UP_0);
            ref readonly SpriteInfo gumpInfoDown = ref Client.Game.UO.Gumps.GetGump(BUTTON_DOWN_0);
            ref readonly SpriteInfo gumpInfoBackground = ref Client.Game.UO.Gumps.GetGump(BACKGROUND_0);
            ref readonly SpriteInfo gumpInfoSlider = ref Client.Game.UO.Gumps.GetGump(SLIDER);

            Width = gumpInfoBackground.LogicalWidth;

            _rectDownButton = new Rectangle(
                0,
                Height - gumpInfoDown.LogicalHeight,
                gumpInfoDown.LogicalWidth,
                gumpInfoDown.LogicalHeight
            );
            _rectUpButton = new Rectangle(0, 0, gumpInfoUp.LogicalWidth, gumpInfoUp.LogicalHeight);
            _rectSlider = new Rectangle(
                (gumpInfoBackground.LogicalWidth - gumpInfoSlider.LogicalWidth) >> 1,
                gumpInfoUp.LogicalHeight + _sliderPosition,
                gumpInfoSlider.LogicalWidth,
                gumpInfoSlider.LogicalHeight
            );
            _emptySpace.X = 0;
            _emptySpace.Y = gumpInfoUp.LogicalHeight;
            _emptySpace.Width = gumpInfoSlider.LogicalWidth;
            _emptySpace.Height = Height - (gumpInfoDown.LogicalHeight + gumpInfoUp.LogicalHeight);
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            if (Height <= 0 || !IsVisible)
            {
                return false;
            }

            ref readonly SpriteInfo gumpInfoUp0 = ref Client.Game.UO.Gumps.GetGump(BUTTON_UP_0);
            ref readonly SpriteInfo gumpInfoUp1 = ref Client.Game.UO.Gumps.GetGump(BUTTON_UP_1);
            ref readonly SpriteInfo gumpInfoDown0 = ref Client.Game.UO.Gumps.GetGump(BUTTON_DOWN_0);
            ref readonly SpriteInfo gumpInfoDown1 = ref Client.Game.UO.Gumps.GetGump(BUTTON_DOWN_1);
            ref readonly SpriteInfo gumpInfoBackground0 = ref Client.Game.UO.Gumps.GetGump(BACKGROUND_0);
            ref readonly SpriteInfo gumpInfoBackground1 = ref Client.Game.UO.Gumps.GetGump(BACKGROUND_1);
            ref readonly SpriteInfo gumpInfoBackground2 = ref Client.Game.UO.Gumps.GetGump(BACKGROUND_2);
            ref readonly SpriteInfo gumpInfoSlider = ref Client.Game.UO.Gumps.GetGump(SLIDER);

            // draw scrollbar background
            int middleHeight =
                Height
                - gumpInfoUp0.LogicalHeight
                - gumpInfoDown0.LogicalHeight
                - gumpInfoBackground0.LogicalHeight
                - gumpInfoBackground2.LogicalHeight;

            if (middleHeight > 0)
            {
                batcher.Draw(
                    gumpInfoBackground0.Texture,
                    new Rectangle(
                        x,
                        y + gumpInfoUp0.LogicalHeight,
                        gumpInfoBackground0.LogicalWidth,
                        gumpInfoBackground0.LogicalHeight
                    ),
                    gumpInfoBackground0.UV,
                    hueVector
                );

                batcher.DrawTiled(
                    gumpInfoBackground1.Texture,
                    new Rectangle(
                        x,
                        y + gumpInfoUp1.LogicalHeight + gumpInfoBackground0.LogicalHeight,
                        gumpInfoBackground0.LogicalWidth,
                        middleHeight
                    ),
                    gumpInfoBackground1.UV,
                    hueVector,
                    gumpInfoBackground1.InverseSourceScale
                );

                batcher.Draw(
                    gumpInfoBackground2.Texture,
                    new Rectangle(
                        x,
                        y + Height - gumpInfoDown0.LogicalHeight - gumpInfoBackground2.LogicalHeight,
                        gumpInfoBackground2.LogicalWidth,
                        gumpInfoBackground2.LogicalHeight
                    ),
                    gumpInfoBackground2.UV,
                    hueVector
                );
            }
            else
            {
                middleHeight = Height - gumpInfoUp0.LogicalHeight - gumpInfoDown0.LogicalHeight;

                batcher.DrawTiled(
                    gumpInfoBackground1.Texture,
                    new Rectangle(
                        x,
                        y + gumpInfoUp0.LogicalHeight,
                        gumpInfoBackground0.LogicalWidth,
                        middleHeight
                    ),
                    gumpInfoBackground1.UV,
                    hueVector,
                    gumpInfoBackground1.InverseSourceScale
                );
            }

            // draw up button
            if (_btUpClicked)
            {
                batcher.Draw(
                    gumpInfoUp1.Texture,
                    new Rectangle(x, y, gumpInfoUp1.LogicalWidth, gumpInfoUp1.LogicalHeight),
                    gumpInfoUp1.UV,
                    hueVector
                );
            }
            else
            {
                batcher.Draw(
                    gumpInfoUp0.Texture,
                    new Rectangle(x, y, gumpInfoUp0.LogicalWidth, gumpInfoUp0.LogicalHeight),
                    gumpInfoUp0.UV,
                    hueVector
                );
            }

            // draw down button
            if (_btDownClicked)
            {
                batcher.Draw(
                    gumpInfoDown1.Texture,
                    new Rectangle(
                        x,
                        y + Height - gumpInfoDown0.LogicalHeight,
                        gumpInfoDown1.LogicalWidth,
                        gumpInfoDown1.LogicalHeight
                    ),
                    gumpInfoDown1.UV,
                    hueVector
                );
            }
            else
            {
                batcher.Draw(
                    gumpInfoDown0.Texture,
                    new Rectangle(
                        x,
                        y + Height - gumpInfoDown0.LogicalHeight,
                        gumpInfoDown0.LogicalWidth,
                        gumpInfoDown0.LogicalHeight
                    ),
                    gumpInfoDown0.UV,
                    hueVector
                );
            }

            // draw slider
            if (MaxValue > MinValue && middleHeight > 0)
            {
                batcher.Draw(
                    gumpInfoSlider.Texture,
                    new Rectangle(
                        x + ((gumpInfoBackground0.LogicalWidth - gumpInfoSlider.LogicalWidth) >> 1),
                        y + gumpInfoUp0.LogicalHeight + _sliderPosition,
                        gumpInfoSlider.LogicalWidth,
                        gumpInfoSlider.LogicalHeight
                    ),
                    gumpInfoSlider.UV,
                    hueVector
                );
            }

            return base.Draw(batcher, x, y);
        }

        protected override int GetScrollableArea()
        {
            ref readonly SpriteInfo gumpInfoUp = ref Client.Game.UO.Gumps.GetGump(BUTTON_UP_0);
            ref readonly SpriteInfo gumpInfoDown = ref Client.Game.UO.Gumps.GetGump(BUTTON_DOWN_0);
            ref readonly SpriteInfo gumpInfoSlider = ref Client.Game.UO.Gumps.GetGump(SLIDER);

            return Height
                - gumpInfoUp.LogicalHeight
                - gumpInfoDown.LogicalHeight
                - gumpInfoSlider.LogicalHeight;
        }

        public override void OnMouseDown(int x, int y, MouseButtonType button)
        {
            base.OnMouseDown(x, y, button);

            if (_btnSliderClicked && _emptySpace.Contains(x, y))
            {
                CalculateByPosition(x, y);
            }
        }

        protected override void CalculateByPosition(int x, int y)
        {
            if (y != _clickPosition.Y)
            {
                y -= _emptySpace.Y + (_rectSlider.Height >> 1);

                if (y < 0)
                {
                    y = 0;
                }

                int scrollableArea = GetScrollableArea();

                if (y > scrollableArea)
                {
                    y = scrollableArea;
                }

                _sliderPosition = y;
                _clickPosition.X = x;
                _clickPosition.Y = y;

                ref readonly SpriteInfo gumpInfoUp = ref Client.Game.UO.Gumps.GetGump(BUTTON_UP_0);
                ref readonly SpriteInfo gumpInfoDown = ref Client.Game.UO.Gumps.GetGump(BUTTON_DOWN_0);
                ref readonly SpriteInfo gumpInfoSlider = ref Client.Game.UO.Gumps.GetGump(SLIDER);

                if (
                    y == 0
                    && _clickPosition.Y < gumpInfoUp.LogicalHeight + (gumpInfoSlider.LogicalHeight >> 1)
                )
                {
                    _clickPosition.Y = gumpInfoUp.LogicalHeight + (gumpInfoSlider.LogicalHeight >> 1);
                }
                else if (
                    y == scrollableArea
                    && _clickPosition.Y
                        > Height - gumpInfoDown.LogicalHeight - (gumpInfoSlider.LogicalHeight >> 1)
                )
                {
                    _clickPosition.Y =
                        Height - gumpInfoDown.LogicalHeight - (gumpInfoSlider.LogicalHeight >> 1);
                }

                _value = (int)
                    Math.Round(y / (float)scrollableArea * (MaxValue - MinValue) + MinValue);
            }
        }

        public override bool Contains(int x, int y) => x >= 0 && x <= Width && y >= 0 && y <= Height;
    }
}

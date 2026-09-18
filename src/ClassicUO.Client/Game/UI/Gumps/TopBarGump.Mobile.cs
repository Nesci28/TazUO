#if TAZUO_IOS
using System;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    public partial class TopBarGump
    {
        private Rectangle _mobileLayoutBounds;
        private int _mobileLayoutPage = -1;
        private ResizePic _mobileToggleBackground;

        public override void Update()
        {
            const int toggleSize = 44;
            const int menuLeft = toggleSize + 8;

            if (_mobileToggleBackground == null)
            {
                // Keep the original 30x27 frame around either arrow. The
                // transparent 44x44 button bounds provide the larger tap area.
                _mobileToggleBackground = new ResizePic(0x13BE)
                {
                    X = 4 + (toggleSize - 30) / 2,
                    Y = 4 + (toggleSize - 27) / 2,
                    Width = 30,
                    Height = 27,
                    AcceptMouseInput = false,
                    CanMove = false
                };
                Add(_mobileToggleBackground);
                Children.Remove(_mobileToggleBackground);
                Children.Insert(0, _mobileToggleBackground);
            }

            Rectangle safe = MobileControlBridge.GetSafeUIBounds();
            CanMove = false;
            X = safe.X + 4;
            Y = safe.Y + 4;

            if (safe != _mobileLayoutBounds || ActivePage != _mobileLayoutPage)
            {
                _mobileLayoutBounds = safe;
                _mobileLayoutPage = ActivePage;
                int availableWidth = Math.Max(1, safe.Width - 8);
                int x = menuLeft, y = 4, rowHeight = 0, rows = 1, buttons = 0;
                ResizePic background = null;

                foreach (Control control in Children)
                {
                    if (control == _mobileToggleBackground || control.Page != ActivePage)
                        continue;
                    if (control is ResizePic pic)
                    {
                        background = pic;
                        continue;
                    }
                    if (control is not Button button)
                        continue;

                    if (button.ButtonAction == ButtonAction.SwitchPage)
                    {
                        button.ContainsByBounds = true;
                        button.DrawTextureAtNativeSize = true;
                        button.Width = toggleSize;
                        button.Height = toggleSize;
                        button.X = 4;
                        button.Y = 4;
                        buttons++;
                        continue;
                    }

                    // Preserve readable captions and wrap the real TazUO
                    // actions, including Assistant and Legion, onto more rows.
                    button.Height = Math.Max(button.Height, 32);
                    button.ContainsByBounds = true;
                    if (x > menuLeft && x + button.Width + 4 > availableWidth)
                    {
                        x = menuLeft;
                        y += rowHeight + 3;
                        rowHeight = 0;
                        rows++;
                    }
                    button.X = x;
                    button.Y = y;
                    x += button.Width + 3;
                    rowHeight = Math.Max(rowHeight, button.Height);
                    buttons++;
                }

                if (background != null)
                {
                    // The menu has its own background so opening it never
                    // changes the frame or padding surrounding the arrow.
                    background.IsVisible = ActivePage != 2;
                    background.X = menuLeft - 4;
                    background.Width = Math.Max(1, availableWidth - background.X);
                    background.Height = y + rowHeight + 4;
                }
                WantUpdateSize = true;
                Log.Debug($"iOS top bar: safe={safe} rows={rows} buttons={buttons}");
            }

            base.Update();
        }
    }
}
#endif

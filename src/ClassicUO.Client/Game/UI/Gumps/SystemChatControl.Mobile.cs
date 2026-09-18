// SPDX-License-Identifier: BSD-2-Clause

#if TAZUO_IOS
using System;
using ClassicUO.Configuration;
using ClassicUO.Input;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    public partial class SystemChatControl
    {
        partial void UpdateMobileLayout()
        {
            const int padding = 4;
            int border = ProfileManager.CurrentProfile.GameWindowFullSize
                ? 0 : WorldViewportGump.BORDER_WIDTH;
            Rectangle viewport = new(
                _gump.ScreenCoordinateX + border,
                _gump.ScreenCoordinateY + border,
                Math.Max(1, _gump.Width - 2 * border),
                Math.Max(1, _gump.Height - 2 * border));
            Rectangle safe = MobileControlBridge.GetSafeUIBounds();
            Rectangle visible = Rectangle.Intersect(viewport, safe);

            // Derive from the viewport each time, rather than shrinking the
            // previous layout. This also follows rotation and viewport moves.
            X = visible.X - _gump.ScreenCoordinateX + padding;
            Y = visible.Y - _gump.ScreenCoordinateY + padding;
            Width = Math.Max(1, visible.Width - 2 * padding);
            Height = Math.Max(CHAT_HEIGHT + 5, visible.Height - 2 * padding);

            int prefixWidth = _currentChatModeLabel.IsVisible
                ? _currentChatModeLabel.Width + CHAT_X_OFFSET : 0;
            TextBoxControl.X = CHAT_X_OFFSET + prefixWidth;
            TextBoxControl.Y = Math.Max(0, Height - CHAT_HEIGHT - 5);
            TextBoxControl.Width = Math.Max(1, Width - TextBoxControl.X - CHAT_X_OFFSET);
            TextBoxControl.Height = CHAT_HEIGHT + CHAT_X_OFFSET;

            _currentChatModeLabel.X = CHAT_X_OFFSET;
            _currentChatModeLabel.Y = TextBoxControl.Y;
            _trans.X = 0;
            _trans.Y = TextBoxControl.Y;
            _trans.Width = Width;
            _trans.Height = CHAT_HEIGHT + 5;
        }
    }
}
#endif

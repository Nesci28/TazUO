// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Game.Managers;
using ClassicUO.Input;

namespace ClassicUO.Game.UI.Gumps.Login;

public partial class LoginGump
{
#if TAZUO_IOS
    private int _baseY;

    internal void FocusMobileTextInput()
    {
        if (string.IsNullOrEmpty(_textboxAccount.Text))
            _textboxAccount.SetKeyboardFocus();
        else
            _passwordFake.SetKeyboardFocus();
    }
#endif

    private void InitializeMobileLayout()
    {
#if TAZUO_IOS
        _baseY = Y;
#endif
    }

    private void UpdateMobileLayout()
    {
#if TAZUO_IOS
        bool editingLogin = MobileControlBridge.SoftwareKeyboardVisible
            && UIManager.MobileTextInputRequested
            && (_textboxAccount.HasKeyboardFocus || _passwordFake.HasKeyboardFocus);
        int targetY = editingLogin ? _baseY - 165 : _baseY;
        if (Y != targetY && !Mouse.LButtonPressed)
            Y = targetY;
#endif
    }
}

using System;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using Microsoft.Xna.Framework.Input;
using Myra;

namespace ClassicUO.Input;

internal static class MyraKeyboardInput
{
    internal static void GetDownKeys(bool[] keys)
    {
        MyraEnvironment.DefaultDownKeysGetter(keys);
        ApplyCommandShortcuts(keys, OperatingSystem.IsMacOS(),
            UIManager.KeyboardFocusControl is MyraControl { HasFocusedTextInput: true });
    }

    internal static void ApplyCommandShortcuts(bool[] keys, bool isMacOS, bool hasTextFocus)
    {
        if (!isMacOS || !hasTextFocus)
            return;

        // Myra's text editor handles these actions through Control. Translate only its
        // polled input snapshot so Command shortcuts retain selection, undo and change events.
        if (keys[(int)Keys.A] || keys[(int)Keys.C] || keys[(int)Keys.V] || keys[(int)Keys.X])
        {
            keys[(int)Keys.LeftControl] |= keys[(int)Keys.LeftWindows];
            keys[(int)Keys.RightControl] |= keys[(int)Keys.RightWindows];
        }
    }
}

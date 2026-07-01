#nullable enable

using System;

namespace ClassicUO.Game.UI.MyraWindows.Widgets;

public class MyraNavigationButton : MyraButton
{
    public MyraNavigationButton(string text, Action? onClick = null, int minWidth = 72)
        : base(text, onClick)
    {
        MyraStyle.ApplyNavigationButtonStyle(this, minWidth);
    }
}

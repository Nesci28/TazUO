#nullable enable

using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace ClassicUO.Game.UI.MyraWindows.Widgets;

public class MyraSearchBox : MyraInputBox
{
    public const int DefaultWidth = 180;

    public MyraSearchBox(string? hintText = null, int width = DefaultWidth)
    {
        HintText = hintText ?? "Search...";
        Width = width;
        TextVerticalAlignment = VerticalAlignment.Center;
        Margin = new Thickness(
            MyraStyle.STANDARD_SPACING,
            0,
            MyraStyle.STANDARD_SPACING,
            MyraStyle.STANDARD_SPACING
        );
        Padding = new Thickness(
            MyraStyle.STANDARD_SPACING,
            5,
            MyraStyle.STANDARD_SPACING,
            5
        );
    }
}

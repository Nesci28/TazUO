#nullable enable

using Myra.Graphics2D.UI;

namespace ClassicUO.Game.UI.MyraWindows.Widgets;

public class MyraListRow : MyraHorizontalStackPanel
{
    public MyraListRow()
    {
        Spacing = MyraStyle.STANDARD_SPACING;
        VerticalAlignment = VerticalAlignment.Center;
    }
}

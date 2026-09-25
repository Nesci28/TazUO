#nullable enable

using Myra.Graphics2D.UI;

namespace ClassicUO.Game.UI.MyraWindows.Widgets;

public class MyraVerticalStackPanel : VerticalStackPanel
{
    public MyraVerticalStackPanel()
    {
        Spacing = MyraStyle.STANDARD_SPACING;
    }
}

public class MyraHorizontalStackPanel : HorizontalStackPanel
{
    public MyraHorizontalStackPanel()
    {
        Spacing = MyraStyle.STANDARD_SPACING;
        VerticalAlignment = VerticalAlignment.Center;
    }
}

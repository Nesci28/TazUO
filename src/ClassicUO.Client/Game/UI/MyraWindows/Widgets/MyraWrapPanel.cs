#nullable enable

using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.WrapPanel;

namespace ClassicUO.Game.UI.MyraWindows.Widgets;

public class MyraWrapPanel : WrapPanel
{
    public MyraWrapPanel()
    {
        UniformSizing = false;
        HorizontalSpacing = MyraStyle.STANDARD_SPACING;
        VerticalSpacing = MyraStyle.STANDARD_SPACING;
        Margin = new Thickness(MyraStyle.STANDARD_SPACING);
        VerticalAlignment = VerticalAlignment.Top;
    }
}

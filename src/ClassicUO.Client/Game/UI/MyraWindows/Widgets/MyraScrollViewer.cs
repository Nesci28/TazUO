#nullable enable

using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.Styles;

namespace ClassicUO.Game.UI.MyraWindows.Widgets;

public class MyraScrollViewer : ScrollViewer
{
    public bool UseThemedBorder { get; set; }

    public void ApplyCurrentTheme(ScrollViewerStyle style)
    {
        ApplyScrollViewerStyle(style);

        if (!UseThemedBorder)
        {
            return;
        }

        Border = MyraStyle.Brush(MyraStyle.BorderColor);
        BorderThickness = new Myra.Graphics2D.Thickness(1);
    }
}

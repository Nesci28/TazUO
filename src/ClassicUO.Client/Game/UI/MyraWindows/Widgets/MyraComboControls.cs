#nullable enable

using Myra.Graphics2D.UI;

namespace ClassicUO.Game.UI.MyraWindows.Widgets;

public class MyraComboView : ComboView
{
    public MyraComboView()
    {
        MinWidth = 120;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Center;
    }
}

public class MyraComboBox : ComboBox
{
    public MyraComboBox()
    {
        MinWidth = 120;
        VerticalAlignment = VerticalAlignment.Center;
    }
}

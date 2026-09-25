#nullable enable

using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace ClassicUO.Game.UI.MyraWindows.Widgets;

public class MyraCommandBar : MyraGrid
{
    private readonly MyraHorizontalStackPanel _leftItems = new()
    {
        Spacing = MyraStyle.STANDARD_SPACING,
        VerticalAlignment = VerticalAlignment.Center
    };

    private readonly MyraHorizontalStackPanel _rightItems = new()
    {
        Spacing = MyraStyle.STANDARD_SPACING,
        VerticalAlignment = VerticalAlignment.Center
    };

    public MyraCommandBar(params Widget[] leftItems) : this(leftItems, [])
    {
    }

    public MyraCommandBar(Widget[] leftItems, Widget[] rightItems)
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Center;
        Padding = new Thickness(MyraStyle.STANDARD_SPACING, 0, MyraStyle.STANDARD_SPACING, MyraStyle.STANDARD_SPACING);
        ApplyCurrentTheme();

        AddColumn(Proportion.Auto);
        AddColumn(Proportion.Fill);
        AddColumn(Proportion.Auto);
        AddRow(Proportion.Auto);

        AddWidget(_leftItems, 0, 0);
        AddWidget(new MyraSpacer(1, 1), 0, 1);
        AddWidget(_rightItems, 0, 2);

        AddLeft(leftItems);
        AddRight(rightItems);
    }

    public void AddLeft(params Widget[] widgets) => Add(_leftItems, widgets);

    public void AddRight(params Widget[] widgets) => Add(_rightItems, widgets);

    public void ApplyCurrentTheme()
    {
        Background = MyraStyle.SurfaceMutedBackgroundBrush;
        Border = MyraStyle.Brush(MyraStyle.BorderSoftColor);
        BorderThickness = new Thickness(0, 0, 0, 1);
    }

    private static void Add(MyraHorizontalStackPanel panel, params Widget[] widgets)
    {
        foreach (Widget widget in widgets)
        {
            if (widget != null)
                panel.Widgets.Add(widget);
        }
    }
}

#nullable enable

using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace ClassicUO.Game.UI.MyraWindows.Widgets;

public class MyraHorizontalSeparator : HorizontalSeparator
{
    public MyraHorizontalSeparator()
    {
        Thickness = 2;
        Color = MyraStyle.BorderColor;
        BorderThickness = StyleConstantsDefaults.BorderThickness;
    }
}

public class MyraVerticalSeparator : VerticalSeparator
{
    public MyraVerticalSeparator()
    {
        Thickness = 2;
        Color = MyraStyle.BorderColor;
        BorderThickness = StyleConstantsDefaults.BorderThickness;
    }
}

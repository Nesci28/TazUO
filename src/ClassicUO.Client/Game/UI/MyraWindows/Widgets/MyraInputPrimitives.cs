#nullable enable

using Myra.Graphics2D.UI;

namespace ClassicUO.Game.UI.MyraWindows.Widgets;

public class MyraTextBox : TextBox
{
    public MyraTextBox()
    {
        VerticalAlignment = VerticalAlignment.Center;
    }
}

public class MyraSpinButton : SpinButton
{
    public MyraSpinButton()
    {
        VerticalAlignment = VerticalAlignment.Center;
    }
}

public class MyraRadioButton : RadioButton
{
    public MyraRadioButton()
    {
        Padding = new Myra.Graphics2D.Thickness(2, 1);
        VerticalAlignment = VerticalAlignment.Center;
    }
}

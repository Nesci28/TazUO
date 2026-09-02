using System;
using ClassicUO.Utility.Platforms;
using Microsoft.Xna.Framework;
using Myra.Events;

namespace ClassicUO.Game.UI.MyraWindows.Widgets;

public class LinkLabel : MyraLabel
{
    private string _link;
    private bool _visited;

    public LinkLabel(
        string text,
        string link,
        int fontSizeOffset
    ) : base(text, fontSizeOffset)
    {
        Init(link);
    }

    public LinkLabel(
        string text,
        string link,
        TextStyle style = TextStyle.P,
        AlignMode align = AlignMode.Left
    ) : base(text, style, align)
    {
        Init(link);
    }

    protected void Init(string link)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(link);
        _link = link;
        ApplyCurrentTheme();
    }

    public override void ApplyCurrentTheme()
    {
        base.ApplyCurrentTheme();

        TextColor = _visited ? MyraStyle.AccentPressedColor : MyraStyle.AccentColor;
        OverTextColor = MyraStyle.TextHighlightColor;
    }

    public override void OnTouchDown(TouchEventArgs args)
    {
        base.OnTouchDown(args);
        PlatformHelper.LaunchBrowser(_link);
        _visited = true;
        ApplyCurrentTheme();
    }
}

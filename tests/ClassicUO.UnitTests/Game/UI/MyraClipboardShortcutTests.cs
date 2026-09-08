using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using ClassicUO.Game.UI.MyraWindows.Widgets;
using ClassicUO.Input;
using ClassicUO.UnitTests.Game.LegionScript;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.Styles;
using Xunit;

namespace ClassicUO.UnitTests.Game.UI;

[Collection(MainThreadCollection.Name)]
public class MyraClipboardShortcutTests : IDisposable
{
    private readonly Stylesheet _previousStylesheet;
    private readonly bool _previousLocalClipboard;
    private readonly string _previousClipboard;
    private readonly bool[] _keys = new bool[0xff];
    private readonly Desktop _desktop;

    public MyraClipboardShortcutTests()
    {
        // Run the real Myra text editor without creating a graphics device or touching the OS clipboard.
        _previousStylesheet = (Stylesheet)Field(typeof(Stylesheet), "_current", true).GetValue(null);
        Stylesheet.Current = new Stylesheet();
        Stylesheet.Current.TextBoxStyles[Stylesheet.DefaultStyleName] = new TextBoxStyle
        {
            Font = new StaticSpriteFont(16, 16)
        };
        _previousLocalClipboard = TextCopy.Clipboard.UseLocalClipboard;
        TextCopy.Clipboard.UseLocalClipboard = true;
        _previousClipboard = TextCopy.Clipboard.GetText();

        _desktop = (Desktop)RuntimeHelpers.GetUninitializedObject(typeof(Desktop));
        GC.SuppressFinalize(_desktop);
        Field(typeof(Desktop), "_downKeys").SetValue(_desktop, _keys);
    }

    [Theory]
    [InlineData(Keys.LeftWindows)]
    [InlineData(Keys.RightWindows)]
    public void CommandCopyAndPasteRoundTripCoordinatesThroughMyra(Keys command)
    {
        MyraInputBox source = Input("1639, 1532");
        Press(source, command, Keys.A);
        Press(source, command, Keys.C);
        Assert.Equal("1639, 1532", TextCopy.Clipboard.GetText());

        MyraInputBox destination = Input("100, 200");
        int changes = 0;
        destination.TextChangedByUser += (_, _) => changes++;
        Press(destination, command, Keys.A);
        Press(destination, command, Keys.V);

        Assert.Equal("1639, 1532", destination.Text);
        Assert.Equal(destination.Text.Length, destination.CursorPosition);
        Assert.Equal(destination.SelectStart, destination.SelectEnd);
        Assert.True(changes > 0); // The Go To / Pathfind validation must run after pasting.
    }

    [Fact]
    public void CommandCutIsUndoableAndReadonlyFieldsCannotBeChanged()
    {
        MyraInputBox input = Input("Home");
        Press(input, Keys.LeftWindows, Keys.A);
        Press(input, Keys.LeftWindows, Keys.X);
        Assert.Equal("Home", TextCopy.Clipboard.GetText());
        Assert.Equal("", input.Text);

        Press(input, Keys.LeftControl, Keys.Z);
        Assert.Equal("Home", input.Text);
        input.Readonly = true;
        Press(input, Keys.LeftWindows, Keys.A);
        Press(input, Keys.LeftWindows, Keys.X);
        TextCopy.Clipboard.SetText("Replacement");
        Press(input, Keys.LeftWindows, Keys.V);
        Assert.Equal("Home", input.Text);
    }

    [Fact]
    public void ControlShortcutsStillWork()
    {
        MyraInputBox input = Input("A coordinate");
        Press(input, Keys.LeftControl, Keys.A);
        Press(input, Keys.LeftControl, Keys.C);
        Assert.Equal("A coordinate", TextCopy.Clipboard.GetText());
        TextCopy.Clipboard.SetText("123, 456");
        Press(input, Keys.RightControl, Keys.V);
        Assert.Equal("123, 456", input.Text);
    }

    [Theory]
    [InlineData(false, true, Keys.C)]
    [InlineData(true, false, Keys.V)]
    [InlineData(true, true, Keys.Q)]
    public void OtherPlatformsContextsAndShortcutsKeepTheirOriginalModifiers(bool isMacOS, bool hasTextFocus, Keys key)
    {
        _keys[(int)Keys.LeftWindows] = true;
        _keys[(int)key] = true;
        var original = (bool[])_keys.Clone();

        MyraKeyboardInput.ApplyCommandShortcuts(_keys, isMacOS, hasTextFocus);

        Assert.Equal(original, _keys);
    }

    private MyraInputBox Input(string text)
    {
        var input = new MyraInputBox { Text = text };
        Field(typeof(Widget), "_desktop").SetValue(input, _desktop);
        // The empty test font keeps cursor layout at the origin; only text editing is exercised.
        Field(typeof(TextBox), "_lastCursorPosition").SetValue(input, Point.Zero);
        return input;
    }

    private void Press(MyraInputBox input, Keys modifier, Keys key)
    {
        Array.Clear(_keys);
        _keys[(int)modifier] = true;
        _keys[(int)key] = true;
        MyraKeyboardInput.ApplyCommandShortcuts(_keys, isMacOS: true, hasTextFocus: true);
        input.OnKeyDown(key);
    }

    private static FieldInfo Field(Type type, string name, bool isStatic = false) =>
        type.GetField(name, BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance));

    public void Dispose()
    {
        Stylesheet.Current = _previousStylesheet;
        TextCopy.Clipboard.SetText(_previousClipboard ?? "");
        TextCopy.Clipboard.UseLocalClipboard = _previousLocalClipboard;
    }
}

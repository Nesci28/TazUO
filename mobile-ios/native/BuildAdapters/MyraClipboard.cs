using System;

namespace TextCopy;

/// <summary>Clipboard surface used by Myra on iOS before the SDL window exists.</summary>
public static class Clipboard
{
    public static bool UseLocalClipboard = true;
    private static string _text = string.Empty;

    public static void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
    }

    public static string GetText() => _text;
}

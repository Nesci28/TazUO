#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.Styles;

namespace ClassicUO.Game.UI.MyraWindows.Widgets;

/// <summary>A dropdown whose popup contains a live search field above its selectable values.</summary>
public sealed class MyraSearchableDropdown : MyraButton
{
    private readonly string[] _values;
    private readonly Action<string> _onSelected;
    private readonly string _placeholder;
    private readonly string _searchHint;
    private readonly string _noMatchesText;
    private readonly int _dropdownWidth;
    private string? _selectedValue;

    public MyraSearchableDropdown(
        IEnumerable<string> values,
        string? selectedValue,
        Action<string> onSelected,
        string placeholder = "Select...",
        string searchHint = "Search...",
        string noMatchesText = "No matches",
        int width = 220)
        : base(string.Empty)
    {
        _values = values?.Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();
        _onSelected = onSelected ?? throw new ArgumentNullException(nameof(onSelected));
        _placeholder = placeholder;
        _searchHint = searchHint;
        _noMatchesText = noMatchesText;
        _dropdownWidth = width;

        Width = width;
        MinWidth = width;
        OnClick = ShowDropdown;
        SetSelectedValue(selectedValue, notify: false);
    }

    public string? SelectedValue => _selectedValue;

    public void SetSelectedValue(string? value, bool notify = true)
    {
        _selectedValue = value;
        string display = string.IsNullOrWhiteSpace(value) ? _placeholder : value;
        Content = new MyraLabel($"{display}  ▼", MyraLabel.TextStyle.P);

        if (notify && !string.IsNullOrWhiteSpace(value))
            _onSelected(value);
    }

    private void ShowDropdown()
    {
        if (Desktop == null || _values.Length == 0)
            return;

        var results = new VerticalStackPanel { Spacing = 1 };
        var search = new MyraInputBox
        {
            HintText = _searchHint,
            Width = _dropdownWidth - 8
        };

        void RebuildResults()
        {
            results.Widgets.Clear();
            string query = search.Text?.Trim() ?? string.Empty;
            string[] matches = _values.Where(value =>
                query.Length == 0 || value.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();

            if (matches.Length == 0)
            {
                results.Widgets.Add(new MyraLabel(_noMatchesText, MyraLabel.TextStyle.P));
                return;
            }

            foreach (string match in matches)
            {
                string capturedValue = match;
                results.Widgets.Add(new MyraButton(capturedValue, () =>
                {
                    SetSelectedValue(capturedValue);
                    Desktop?.HideContextMenu();
                })
                {
                    Width = _dropdownWidth - 8,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                });
            }
        }

        search.TextChangedByUser += (_, _) => RebuildResults();
        RebuildResults();

        var popup = new VerticalStackPanel
        {
            Spacing = MyraStyle.STANDARD_SPACING,
            Width = _dropdownWidth,
            Padding = new Thickness(4),
            AcceptsKeyboardFocus = true
        };

        ListBoxStyle style = Stylesheet.Current.ListBoxStyle;
        popup.Background = style.Background;
        popup.Border = style.Border;
        popup.BorderThickness = style.BorderThickness;
        popup.Widgets.Add(search);
        popup.Widgets.Add(new ScrollViewer
        {
            Content = results,
            Width = _dropdownWidth - 8,
            MaxHeight = 260
        });

        Desktop desktop = Desktop;
        desktop.ShowContextMenu(popup, ToGlobal(new Point(0, Bounds.Height)));
        desktop.FocusedKeyboardWidget = search;
    }
}

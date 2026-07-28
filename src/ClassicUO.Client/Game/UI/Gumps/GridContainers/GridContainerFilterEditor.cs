using System;
using System.Collections.Generic;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps.GridHighLight;
using ClassicUO.Game.UI.MyraWindows;
using ClassicUO.Game.UI.MyraWindows.Widgets;
using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.WrapPanel;

namespace ClassicUO.Game.UI.Gumps.GridContainers;

/// <summary>Live editor for the advanced item filter attached to one grid container.</summary>
internal sealed class GridContainerFilterEditor : MyraControl
{
    private static readonly Func<char, bool> IntInputFilter = character => char.IsDigit(character) || character == '-';
    private static readonly Layer[] FilterLayers =
    {
        Layer.Invalid, Layer.OneHanded, Layer.TwoHanded, Layer.Shoes, Layer.Pants, Layer.Shirt,
        Layer.Helmet, Layer.Gloves, Layer.Ring, Layer.Talisman, Layer.Necklace, Layer.Waist,
        Layer.Torso, Layer.Bracelet, Layer.Tunic, Layer.Earrings, Layer.Arms, Layer.Cloak,
        Layer.Backpack, Layer.Robe, Layer.Skirt, Layer.Legs
    };

    private readonly GridContainer _container;
    private readonly GridContainerFilter _filter;
    private readonly VerticalStackPanel _content = new() { Spacing = MyraStyle.STANDARD_SPACING };
    private MyraCheckButton _enabledCheckButton;

    private GridContainerFilterEditor(GridContainer container)
        : base(TazLang.Get("gridcontainer_filter_title", "Container Filter"))
    {
        _container = container;
        _filter = container.ContainerFilter;

        SetRootContent(new ScrollViewer { MaxHeight = 560, Content = _content });
        Rebuild();
        CenterInViewPort();
    }

    public static void Open(GridContainer container)
    {
        foreach (IGui gump in UIManager.Gumps)
        {
            if (gump is GridContainerFilterEditor editor && !editor.IsDisposed &&
                editor._container.LocalSerial == container.LocalSerial)
            {
                editor.BringOnTop();
                return;
            }
        }

        UIManager.Add(new GridContainerFilterEditor(container));
    }

    public static void RefreshFor(uint containerSerial)
    {
        foreach (IGui gump in UIManager.Gumps)
            if (gump is GridContainerFilterEditor editor && !editor.IsDisposed &&
                editor._container.LocalSerial == containerSerial)
                editor.Rebuild();
    }

    public static void CloseFor(uint containerSerial)
    {
        foreach (IGui gump in UIManager.Gumps)
            if (gump is GridContainerFilterEditor editor && !editor.IsDisposed &&
                editor._container.LocalSerial == containerSerial)
                editor.Dispose();
    }

    public override void Update()
    {
        if ((_disposeRequested || _container.IsDisposed) && !IsDisposed)
        {
            _filter.Normalize();
            _container.NotifyContainerFilterChanged(true);
        }

        if (_container.IsDisposed)
            Dispose();

        base.Update();
    }

    private void Rebuild()
    {
        _content.Widgets.Clear();

        _enabledCheckButton = MyraCheckButton.CreateWithCallback(
            _filter.Enabled,
            value =>
            {
                _filter.Enabled = value;
                FilterChanged(true);
            },
            TazLang.Get("gridcontainer_filter_enabled", "Enabled"),
            TazLang.Get("gridcontainer_filter_enabled_tooltip", "Show only items that match every configured filter category."));
        _content.Widgets.Add(_enabledCheckButton);

        _content.Widgets.Add(Divider());
        BuildNeedles();
        _content.Widgets.Add(Divider());
        BuildProperties();
        _content.Widgets.Add(Divider());
        BuildCurses();
        _content.Widgets.Add(Divider());
        BuildLayers();
        _content.Widgets.Add(Divider());
        BuildItemTypes();

        ForceSizeUpdate();
    }

    private void BuildNeedles()
    {
        _content.Widgets.Add(new MyraLabel(
            TazLang.Get("gridcontainer_filter_needles", "Name needles (all required)"),
            MyraLabel.TextStyle.P));

        for (int index = 0; index < _filter.Needles.Count; index++)
        {
            int capturedIndex = index;
            var row = new HorizontalStackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            var input = new MyraInputBox { Text = _filter.Needles[index] ?? string.Empty, Width = 260 };
            input.TextChangedByUser += (_, _) =>
            {
                _filter.Needles[capturedIndex] = input.Text ?? string.Empty;
                CriteriaChanged(false);
            };
            input.LostFocus = () => CommitStringRow(_filter.Needles, capturedIndex);
            row.Widgets.Add(input);
            row.Widgets.Add(DeleteButton(() =>
            {
                _filter.Needles.RemoveAt(capturedIndex);
                FilterChanged(true);
                Rebuild();
            }));
            _content.Widgets.Add(row);
        }

        _content.Widgets.Add(new MyraButton(TazLang.Get("gridcontainer_filter_addneedle", "Add Name Needle"), () =>
        {
            _filter.Needles.Add(string.Empty);
            Rebuild();
        }));
    }

    private void BuildProperties()
    {
        _content.Widgets.Add(new MyraLabel(
            TazLang.Get("gridcontainer_filter_properties", "Properties (all required)"),
            MyraLabel.TextStyle.P));

        string[] suggestions = GridHighlightRules.FlattenAndDistinctParameters(
            GridHighlightRules.Properties,
            GridHighlightRules.Resistances,
            GridHighlightRules.SuperSlayerProperties,
            GridHighlightRules.SlayerProperties);

        var grid = new MyraGrid();
        grid.SetupWithHeaders(
            GridColumnInfo.Auto(TazLang.Get("gridhighlight_propertyname", "Property name")),
            GridColumnInfo.Auto(TazLang.Get("gridhighlight_minvalue", "Min value")),
            GridColumnInfo.Auto(string.Empty));

        for (int index = 0; index < _filter.Properties.Count; index++)
            AddPropertyRow(grid, index + 1, index, suggestions);

        _content.Widgets.Add(grid);
        _content.Widgets.Add(new MyraButton(TazLang.Get("gridhighlight_addproperty", "Add Property"), () =>
        {
            _filter.Properties.Add(new GridContainerFilterProperty());
            Rebuild();
        }));
    }

    private void AddPropertyRow(MyraGrid grid, int row, int index, string[] suggestions)
    {
        GridContainerFilterProperty property = _filter.Properties[index];
        grid.AddWidget(new MyraSearchableDropdown(
            suggestions,
            property.Name,
            selected =>
            {
                property.Name = selected;
                CriteriaChanged(true);
            },
            TazLang.Get("gridcontainer_filter_selectproperty", "Select property..."),
            TazLang.Get("gridcontainer_filter_dropdown_search", "Search..."),
            TazLang.Get("gridcontainer_filter_dropdown_empty", "No matches")), row, 0);

        var minimumInput = new MyraInputBox
        {
            Text = property.MinimumValue.ToString(),
            Width = 55,
            InputFilter = IntInputFilter
        };
        minimumInput.TextChangedByUser += (_, _) =>
        {
            if (int.TryParse(minimumInput.Text, out int value))
            {
                property.MinimumValue = Math.Max(-1, value);
                if (!string.IsNullOrWhiteSpace(property.Name))
                    CriteriaChanged(false);
            }
        };
        minimumInput.LostFocus = () =>
        {
            if (!int.TryParse(minimumInput.Text, out int value))
                value = -1;
            property.MinimumValue = Math.Max(-1, value);
            minimumInput.Text = property.MinimumValue.ToString();
            FilterChanged(true);
        };
        grid.AddWidget(minimumInput, row, 1);
        grid.AddWidget(DeleteButton(() =>
        {
            _filter.Properties.RemoveAt(index);
            FilterChanged(true);
            Rebuild();
        }), row, 2);
    }

    private void BuildCurses()
    {
        _content.Widgets.Add(new MyraLabel(
            TazLang.Get("gridcontainer_filter_curses", "Curses / negative properties"),
            MyraLabel.TextStyle.P));

        string[] suggestions = GridHighlightRules.FlattenAndDistinctParameters(GridHighlightRules.NegativeProperties);
        var grid = new MyraGrid();
        grid.SetupWithHeaders(
            GridColumnInfo.Auto(TazLang.Get("gridhighlight_propertyname", "Property name")),
            GridColumnInfo.Auto(TazLang.Get("gridcontainer_filter_curse_mode", "Mode")),
            GridColumnInfo.Auto(string.Empty));

        for (int index = 0; index < _filter.Curses.Count; index++)
            AddCurseRow(grid, index + 1, index, suggestions);

        _content.Widgets.Add(grid);
        _content.Widgets.Add(new MyraButton(TazLang.Get("gridcontainer_filter_addcurse", "Add Curse"), () =>
        {
            _filter.Curses.Add(new GridContainerFilterCurse());
            Rebuild();
        }));
    }

    private void AddCurseRow(MyraGrid grid, int row, int index, string[] suggestions)
    {
        GridContainerFilterCurse curse = _filter.Curses[index];
        grid.AddWidget(new MyraSearchableDropdown(
            suggestions,
            curse.Name,
            selected =>
            {
                curse.Name = selected;
                CriteriaChanged(true);
            },
            TazLang.Get("gridcontainer_filter_selectcurse", "Select curse..."),
            TazLang.Get("gridcontainer_filter_dropdown_search", "Search..."),
            TazLang.Get("gridcontainer_filter_dropdown_empty", "No matches")), row, 0);

        string[] modes =
        {
            TazLang.Get("gridcontainer_filter_require", "Require"),
            TazLang.Get("gridcontainer_filter_exclude", "Exclude")
        };
        string selectedMode = curse.Mode == GridContainerFilterCurseMode.Exclude ? modes[1] : modes[0];
        grid.AddWidget(new MyraSearchableDropdown(
            modes,
            selectedMode,
            selected =>
            {
                curse.Mode = selected == modes[1]
                    ? GridContainerFilterCurseMode.Exclude
                    : GridContainerFilterCurseMode.Require;
                if (!string.IsNullOrWhiteSpace(curse.Name))
                    CriteriaChanged(true);
                else
                    FilterChanged(true);
            },
            TazLang.Get("gridcontainer_filter_selectmode", "Select mode..."),
            TazLang.Get("gridcontainer_filter_dropdown_search", "Search..."),
            TazLang.Get("gridcontainer_filter_dropdown_empty", "No matches"),
            140), row, 1);
        grid.AddWidget(DeleteButton(() =>
        {
            _filter.Curses.RemoveAt(index);
            FilterChanged(true);
            Rebuild();
        }), row, 2);
    }

    private void BuildLayers()
    {
        _content.Widgets.Add(new MyraLabel(
            TazLang.Get("gridcontainer_filter_layers", "Item layers (any selected)"),
            MyraLabel.TextStyle.P));

        var wrap = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalSpacing = 12,
            VerticalSpacing = 2,
            Width = 480
        };

        foreach (Layer layer in FilterLayers)
        {
            byte value = (byte)layer;
            string label = layer == Layer.Invalid
                ? TazLang.Get("gridhighlight_otherslot", "Other / No Slot Assigned")
                : layer.ToString();
            wrap.Widgets.Add(MyraCheckButton.CreateWithCallback(_filter.Layers.Contains(value), isChecked =>
            {
                if (isChecked)
                {
                    if (!_filter.Layers.Contains(value))
                        _filter.Layers.Add(value);
                }
                else
                {
                    _filter.Layers.Remove(value);
                }
                CriteriaChanged(true);
            }, label));
        }

        _content.Widgets.Add(wrap);
    }

    private void BuildItemTypes()
    {
        _content.Widgets.Add(new MyraLabel(
            TazLang.Get("gridcontainer_filter_itemtypes", "Item types / rarities (any selected)"),
            MyraLabel.TextStyle.P));

        var wrap = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalSpacing = 12,
            VerticalSpacing = 2,
            Width = 480
        };

        foreach (string itemType in GridHighlightRules.RarityProperties.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            string capturedType = itemType;
            wrap.Widgets.Add(MyraCheckButton.CreateWithCallback(
                _filter.ItemTypes.Any(value => value.Equals(capturedType, StringComparison.OrdinalIgnoreCase)),
                isChecked =>
                {
                    _filter.ItemTypes.RemoveAll(value => value.Equals(capturedType, StringComparison.OrdinalIgnoreCase));
                    if (isChecked)
                        _filter.ItemTypes.Add(capturedType);
                    CriteriaChanged(true);
                },
                capturedType));
        }

        _content.Widgets.Add(wrap);
    }

    private void CommitStringRow(List<string> values, int index)
    {
        if (index < values.Count && string.IsNullOrWhiteSpace(values[index]))
        {
            values.RemoveAt(index);
            Rebuild();
        }
        FilterChanged(true);
    }

    private void CriteriaChanged(bool persist)
    {
        if (!_filter.Enabled)
        {
            _filter.Enabled = true;
            if (_enabledCheckButton != null && !_enabledCheckButton.IsChecked)
                _enabledCheckButton.IsChecked = true;
        }

        _container.NotifyContainerFilterChanged(persist);
    }

    private void FilterChanged(bool persist) => _container.NotifyContainerFilterChanged(persist);

    private static Widget DeleteButton(Action onClick) =>
        MyraStyle.ApplyButtonDangerStyle(new MyraButton("X", onClick)
        {
            Tooltip = TazLang.Get("gridcontainer_filter_delete", "Delete this filter entry")
        });

    private static Widget Divider() =>
        new HorizontalSeparator { Thickness = 2, Color = new Color(0, 0, 0, 75) };
}

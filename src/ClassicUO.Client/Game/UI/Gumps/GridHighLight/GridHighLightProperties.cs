using System;
using System.Collections.Generic;
using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.MyraWindows;
using ClassicUO.Game.UI.MyraWindows.Widgets;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.WrapPanel;

namespace ClassicUO.Game.UI.Gumps.GridHighLight
{
    /// <summary>
    /// Myra-based editor for a single grid highlight configuration. Exposes the matching options
    /// (allowed extra properties, auto loot, match/property counts), the item name list, the property
    /// rules, the equipment slot filter and the disqualifying/rarity lists. Structural changes
    /// (adding/removing rows) rebuild the scrollable body in place.
    /// </summary>
    public class GridHighlightProperties : MyraControl
    {
        private static readonly Func<char, bool> IntInputFilter = c => char.IsDigit(c) || c == '-';
        private static readonly Func<char, bool> NonNegativeIntInputFilter = char.IsDigit;

        private readonly World _world;
        private readonly int _keyLoc;
        private readonly GridHighlightData _data;

        private readonly VerticalStackPanel _content = new() { Spacing = MyraStyle.STANDARD_SPACING };

        public GridHighlightProperties(World world, int keyLoc) : base(TazLang.Get("gridhighlight_properties"))
        {
            _world = world;
            _keyLoc = keyLoc;
            _data = GridHighlightData.GetGridHighlightData(keyLoc);

            SetRootContent(new ScrollViewer { MaxHeight = 560, Content = _content });
            Rebuild();
            CenterInViewPort();
        }

        public static void Show(World world, int keyLoc)
        {
            foreach (IGui gump in UIManager.Gumps)
            {
                if (gump is GridHighlightProperties w && !w.IsDisposed && w._keyLoc == keyLoc)
                {
                    w.BringOnTop();
                    return;
                }
            }

            UIManager.Add(new GridHighlightProperties(world, keyLoc));
        }

        private void Rebuild()
        {
            _content.Widgets.Clear();

            BuildMatchingOptions();
            _content.Widgets.Add(Divider());
            BuildItemNames();
            _content.Widgets.Add(Divider());
            BuildProperties();
            _content.Widgets.Add(Divider());
            BuildEquipmentSlots();
            _content.Widgets.Add(Divider());
            BuildNegatives();
            _content.Widgets.Add(Divider());
            BuildRarities();

            ForceSizeUpdate();
        }

        #region Matching options

        private void BuildMatchingOptions()
        {
            _content.Widgets.Add(MyraCheckButton.CreateWithCallback(
                _data.AcceptExtraProperties,
                v => { _data.AcceptExtraProperties = v; GridHighlightData.ConfigurationChanged(); },
                TazLang.Get("gridhighlight_allowextra"),
                TazLang.Get("gridhighlight_acceptextra_tooltip")));

            _content.Widgets.Add(MyraCheckButton.CreateWithCallback(
                _data.LootOnMatch,
                v => { _data.LootOnMatch = v; GridHighlightData.ConfigurationChanged(); },
                TazLang.Get("gridhighlight_lootonmatch"),
                TazLang.Get("gridhighlight_lootonmatch_tooltip")));

            // Destination container + target picker
            var destRow = new HorizontalStackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            var destInput = new MyraInputBox
            {
                Text = _data.DestinationContainer == 0 ? "" : $"0x{_data.DestinationContainer:X}",
                Width = 110,
                Tooltip = TazLang.Get("gridhighlight_destcontainer_tooltip")
            };
            destInput.LostFocus = () =>
            {
                string entered = destInput.Text?.Trim() ?? string.Empty;
                if (entered.Length == 0)
                {
                    _data.DestinationContainer = 0;
                    GridHighlightData.ConfigurationChanged();
                }
                else if (uint.TryParse(entered.Replace("0x", "", StringComparison.OrdinalIgnoreCase),
                             System.Globalization.NumberStyles.HexNumber, null, out uint destSerial))
                {
                    _data.DestinationContainer = destSerial;
                    GridHighlightData.ConfigurationChanged();
                }

                string expected = _data.DestinationContainer == 0 ? "" : $"0x{_data.DestinationContainer:X}";
                if (!string.Equals(destInput.Text, expected, StringComparison.OrdinalIgnoreCase))
                    destInput.Text = expected;
            };
            destRow.Widgets.Add(destInput);
            destRow.Widgets.Add(new MyraLabel(TazLang.Get("gridhighlight_loottocontainer"), MyraLabel.TextStyle.P));
            destRow.Widgets.Add(new MyraButton(TazLang.Get("gridhighlight_target"), () =>
            {
                _world.TargetManager.SetTargeting(targeted =>
                {
                    if (targeted is Item item && item.ItemData.IsContainer)
                    {
                        _data.DestinationContainer = item.Serial;
                        destInput.Text = $"0x{item.Serial:X}";
                        GridHighlightData.ConfigurationChanged();
                    }
                });
            }) { Tooltip = TazLang.Get("gridhighlight_target_tooltip") });
            _content.Widgets.Add(destRow);

            // Match count / property count ranges
            var countRow = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalSpacing = 8, VerticalSpacing = 4 };
            countRow.Widgets.Add(LabeledNumber(TazLang.Get("gridhighlight_minmatchcount"), _data.MinimumMatchingProperty, v => { _data.MinimumMatchingProperty = v; GridHighlightData.ConfigurationChanged(); }));
            countRow.Widgets.Add(LabeledNumber(TazLang.Get("gridhighlight_maxmatchcount"), _data.MaximumMatchingProperty, v => { _data.MaximumMatchingProperty = v; GridHighlightData.ConfigurationChanged(); }));
            _content.Widgets.Add(countRow);

            var propCountRow = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalSpacing = 8, VerticalSpacing = 4 };
            propCountRow.Widgets.Add(LabeledNumber(TazLang.Get("gridhighlight_minpropcount"), _data.MinimumProperty, v => { _data.MinimumProperty = v; GridHighlightData.ConfigurationChanged(); }));
            propCountRow.Widgets.Add(LabeledNumber(TazLang.Get("gridhighlight_maxpropcount"), _data.MaximumProperty, v => { _data.MaximumProperty = v; GridHighlightData.ConfigurationChanged(); }));
            _content.Widgets.Add(propCountRow);
        }

        #endregion

        #region Item names

        private void BuildItemNames()
        {
            _content.Widgets.Add(new MyraLabel(TazLang.Get("gridhighlight_itemname"), MyraLabel.TextStyle.P));

            for (int i = 0; i < _data.ItemNames.Count; i++)
                _content.Widgets.Add(BuildStringRow(_data.ItemNames, i, null));

            _content.Widgets.Add(new MyraButton(TazLang.Get("gridhighlight_additemname"), () =>
            {
                _data.ItemNames.Add("");
                Rebuild();
            }));
        }

        #endregion

        #region Properties

        private void BuildProperties()
        {
            string[] values = GridHighlightRules.FlattenAndDistinctParameters(
                GridHighlightRules.Properties, GridHighlightRules.Resistances,
                GridHighlightRules.SuperSlayerProperties, GridHighlightRules.SlayerProperties);

            // A grid keeps the header labels aligned above their columns (a plain stack sizes each
            // label to its own text width, so the titles drift out of line with the fields).
            var grid = new MyraGrid();
            grid.SetupWithHeaders(
                GridColumnInfo.Auto(TazLang.Get("gridhighlight_propertyname")),
                GridColumnInfo.Auto(TazLang.Get("gridhighlight_minvalue")),
                GridColumnInfo.Auto(TazLang.Get("gridhighlight_optional")),
                GridColumnInfo.Auto("")
            );

            for (int i = 0; i < _data.Properties.Count; i++)
                AddPropertyRow(grid, i + 1, i, values);

            _content.Widgets.Add(grid);

            _content.Widgets.Add(new MyraButton(TazLang.Get("gridhighlight_addproperty"), () =>
            {
                _data.Properties.Add(new GridHighlightProperty { Name = "", MinValue = -1, IsOptional = false });
                Rebuild();
            }));
        }

        private void AddPropertyRow(MyraGrid grid, int row, int index, string[] values)
        {
            GridHighlightProperty property = _data.Properties[index];

            var nameInput = new MyraInputBox { Text = property.Name ?? "", Width = 170 };
            nameInput.TextChangedByUser += (_, _) => property.Name = nameInput.Text ?? "";
            nameInput.LostFocus = () =>
            {
                if (string.IsNullOrWhiteSpace(property.Name))
                {
                    _data.Properties.Remove(property);
                    Rebuild();
                }
                GridHighlightData.ConfigurationChanged();
            };

            // Property picker: a suggestion dropdown that fills the editable name box next to it.
            // Setting Text programmatically doesn't raise TextChangedByUser, so commit the value to
            // the model here too or the picked property is shown but never saved.
            var propCell = new HorizontalStackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            propCell.Widgets.Add(SuggestionCombo(values, v =>
            {
                property.Name = v;
                nameInput.Text = v;
                GridHighlightData.ConfigurationChanged();
            }));
            propCell.Widgets.Add(nameInput);
            grid.AddWidget(propCell, row, 0);

            var minInput = new MyraInputBox { Text = property.MinValue.ToString(), Width = 55, InputFilter = IntInputFilter };
            minInput.TextChangedByUser += (_, _) =>
            {
                if (int.TryParse(minInput.Text, out int v))
                    property.MinValue = v;
            };
            minInput.LostFocus = () =>
            {
                if (!int.TryParse(minInput.Text, out int value))
                {
                    value = -1;
                }
                property.MinValue = Math.Max(-1, value);
                minInput.Text = property.MinValue.ToString();
                GridHighlightData.ConfigurationChanged();
            };
            grid.AddWidget(minInput, row, 1);

            grid.AddWidget(MyraCheckButton.CreateWithCallback(property.IsOptional, v =>
            {
                property.IsOptional = v;
                GridHighlightData.ConfigurationChanged();
            }), row, 2);

            grid.AddWidget(MyraStyle.ApplyButtonDangerStyle(new MyraButton("X", () =>
            {
                _data.Properties.RemoveAt(index);
                Rebuild();
                GridHighlightData.ConfigurationChanged();
            }) { Tooltip = TazLang.Get("gridhighlight_deleteproperty_tooltip") }), row, 3);
        }

        #endregion

        #region Equipment slots

        private void BuildEquipmentSlots()
        {
            (string Label, Func<GridHighlightSlot, bool> Get, Action<GridHighlightSlot, bool> Set)[] slots =
            {
                ("Talisman",   s => s.Talisman,  (s, v) => s.Talisman = v),
                ("Right Hand", s => s.RightHand, (s, v) => s.RightHand = v),
                ("Left Hand",  s => s.LeftHand,  (s, v) => s.LeftHand = v),
                ("Head",       s => s.Head,      (s, v) => s.Head = v),
                ("Earring",    s => s.Earring,   (s, v) => s.Earring = v),
                ("Neck",       s => s.Neck,      (s, v) => s.Neck = v),
                ("Chest",      s => s.Chest,     (s, v) => s.Chest = v),
                ("Shirt",      s => s.Shirt,     (s, v) => s.Shirt = v),
                ("Back",       s => s.Back,      (s, v) => s.Back = v),
                ("Robe",       s => s.Robe,      (s, v) => s.Robe = v),
                ("Arms",       s => s.Arms,      (s, v) => s.Arms = v),
                ("Hands",      s => s.Hands,     (s, v) => s.Hands = v),
                ("Bracelet",   s => s.Bracelet,  (s, v) => s.Bracelet = v),
                ("Ring",       s => s.Ring,      (s, v) => s.Ring = v),
                ("Belt",       s => s.Belt,      (s, v) => s.Belt = v),
                ("Skirt",      s => s.Skirt,     (s, v) => s.Skirt = v),
                ("Legs",       s => s.Legs,      (s, v) => s.Legs = v),
                ("Footwear",   s => s.Footwear,  (s, v) => s.Footwear = v),
            };

            var headerRow = new HorizontalStackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            headerRow.Widgets.Add(new MyraLabel(TazLang.Get("gridhighlight_selectslots"), MyraLabel.TextStyle.P));

            headerRow.Widgets.Add(MyraCheckButton.CreateWithCallback(_data.EquipmentSlots.Other, otherChecked =>
            {
                _data.EquipmentSlots.Other = otherChecked;
                GridHighlightData.ConfigurationChanged();
            }, TazLang.Get("gridhighlight_otherslot")));

            _content.Widgets.Add(headerRow);

            var wrap = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalSpacing = 12, VerticalSpacing = 2, Width = 380 };

            foreach ((string label, Func<GridHighlightSlot, bool> get, Action<GridHighlightSlot, bool> set) in slots)
            {
                Action<GridHighlightSlot, bool> capturedSet = set;
                MyraCheckButton cb = MyraCheckButton.CreateWithCallback(get(_data.EquipmentSlots), v =>
                {
                    capturedSet(_data.EquipmentSlots, v);
                    GridHighlightData.ConfigurationChanged();
                }, label);

                wrap.Widgets.Add(cb);
            }

            _content.Widgets.Add(wrap);
        }

        #endregion

        #region Negatives

        private void BuildNegatives()
        {
            _content.Widgets.Add(new MyraLabel(TazLang.Get("gridhighlight_disqualifying"), MyraLabel.TextStyle.P));

            var weightRow = new WrapPanel { Orientation = Orientation.Horizontal, HorizontalSpacing = 8, VerticalSpacing = 4 };
            weightRow.Widgets.Add(MyraCheckButton.CreateWithCallback(
                _data.Overweight,
                v => { _data.Overweight = v; GridHighlightData.ConfigurationChanged(); },
                TazLang.Get("gridhighlight_weightfilter"),
                TazLang.Get("gridhighlight_weight_tooltip")));

            weightRow.Widgets.Add(LabeledNumber(TazLang.Get("gridhighlight_min"), _data.MinimumWeight,
                v => { _data.MinimumWeight = v; GridHighlightData.ConfigurationChanged(); }, tooltip: TazLang.Get("gridhighlight_minweight_tooltip")));
            weightRow.Widgets.Add(LabeledNumber(TazLang.Get("gridhighlight_max"), _data.MaximumWeight,
                v => { _data.MaximumWeight = v; GridHighlightData.ConfigurationChanged(); }, tooltip: TazLang.Get("gridhighlight_maxweight_tooltip")));
            _content.Widgets.Add(weightRow);

            _content.Widgets.Add(new MyraLabel(TazLang.Get("gridhighlight_excludedesc"), MyraLabel.TextStyle.P));

            string[] values = GridHighlightRules.FlattenAndDistinctParameters(
                GridHighlightRules.NegativeProperties, GridHighlightRules.Properties,
                GridHighlightRules.SuperSlayerProperties, GridHighlightRules.SlayerProperties);

            for (int i = 0; i < _data.ExcludeNegatives.Count; i++)
                _content.Widgets.Add(BuildStringRow(_data.ExcludeNegatives, i, values));

            _content.Widgets.Add(new MyraButton(TazLang.Get("gridhighlight_adddisqualifying"), () =>
            {
                _data.ExcludeNegatives.Add("");
                Rebuild();
            }));
        }

        #endregion

        #region Rarities

        private void BuildRarities()
        {
            _content.Widgets.Add(new MyraLabel(TazLang.Get("gridhighlight_rarityfilters"), MyraLabel.TextStyle.P));
            _content.Widgets.Add(new MyraLabel(TazLang.Get("gridhighlight_raritydesc"), MyraLabel.TextStyle.P));

            string[] values = GridHighlightRules.FlattenAndDistinctParameters(GridHighlightRules.RarityProperties);

            for (int i = 0; i < _data.RequiredRarities.Count; i++)
                _content.Widgets.Add(BuildStringRow(_data.RequiredRarities, i, values));

            _content.Widgets.Add(new MyraButton(TazLang.Get("gridhighlight_addrarity"), () =>
            {
                _data.RequiredRarities.Add("");
                Rebuild();
            }));
        }

        #endregion

        #region Helpers

        /// <summary>Builds a row for a plain string list (item names / negatives / rarities) with an
        /// optional suggestion dropdown, an editable text box and a delete button.</summary>
        private Widget BuildStringRow(List<string> list, int index, string[] suggestions)
        {
            var row = new HorizontalStackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };

            var input = new MyraInputBox { Text = list[index] ?? "", Width = 230 };
            input.TextChangedByUser += (_, _) => list[index] = input.Text ?? "";
            input.LostFocus = () =>
            {
                if (index < list.Count && string.IsNullOrWhiteSpace(list[index]))
                {
                    list.RemoveAt(index);
                    Rebuild();
                }
                GridHighlightData.ConfigurationChanged();
            };

            // Setting Text programmatically doesn't raise TextChangedByUser, so commit the value to
            // the model here too or the picked value is shown but never saved.
            if (suggestions != null && suggestions.Length > 0)
                row.Widgets.Add(SuggestionCombo(suggestions, v =>
                {
                    if (index >= list.Count)
                        return;
                    list[index] = v;
                    input.Text = v;
                    GridHighlightData.ConfigurationChanged();
                }));

            row.Widgets.Add(input);

            row.Widgets.Add(MyraStyle.ApplyButtonDangerStyle(new MyraButton("X", () =>
            {
                list.RemoveAt(index);
                Rebuild();
                GridHighlightData.ConfigurationChanged();
            }) { Tooltip = TazLang.Get("gridhighlight_deleteproperty_tooltip") }));

            return row;
        }

        /// <summary>A numeric input followed by its label, committing valid integers as the user types.</summary>
        private static Widget LabeledNumber(string label, int value, Action<int> onChanged, string tooltip = null)
        {
            var box = new MyraInputBox { Text = Math.Max(0, value).ToString(), Width = 55, Tooltip = tooltip, InputFilter = NonNegativeIntInputFilter };
            box.LostFocus = () =>
            {
                int parsed = int.TryParse(box.Text, out int v) ? Math.Max(0, v) : 0;
                box.Text = parsed.ToString();
                onChanged(parsed);
            };

            var row = new HorizontalStackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            row.Widgets.Add(box);
            row.Widgets.Add(new MyraLabel(label, MyraLabel.TextStyle.P) { Tooltip = tooltip });
            return row;
        }

        private static Widget SuggestionCombo(string[] values, Action<string> onSelect)
        {
#pragma warning disable CS0612, CS0618
            var combo = new ComboBox { VerticalAlignment = VerticalAlignment.Center };

            foreach (string value in values)
                combo.Items.Add(new ListItem(value));

            combo.SelectedIndexChanged += (_, _) =>
            {
                if (combo.SelectedIndex.HasValue)
                    onSelect(values[combo.SelectedIndex.Value]);
            };
#pragma warning restore CS0612, CS0618
            return combo;
        }

        private static Widget Divider() =>
            new HorizontalSeparator { Thickness = 2, Color = new Color(0, 0, 0, 75) };

        #endregion
    }
}

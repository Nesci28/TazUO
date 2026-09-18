using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.MyraWindows;
using ClassicUO.Game.UI.MyraWindows.Widgets;
using ClassicUO.Utility.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.Styles;

namespace ClassicUO.Game.UI.Gumps.GridHighLight
{
    /// <summary>
    /// Myra-based replacement for the legacy grid highlight menu. Lists every highlight
    /// configuration of the current profile with per-row enable/rename/color/properties actions,
    /// ordering controls and delete, plus toolbar buttons to add, import, export and edit the
    /// shared property lists.
    /// </summary>
    internal class GridHighlightMenu : MyraControl
    {
        private readonly World _world;
        private readonly VerticalStackPanel _listPanel = new()
        {
            Spacing = MyraStyle.STANDARD_SPACING,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        public GridHighlightMenu(World world) : base(TazLang.Get("gridhighlight_settings_title"))
        {
            _world = world;
            Build();
            CenterInViewPort();
        }

        public static void Open(World world)
        {
            foreach (IGui gump in UIManager.Gumps)
            {
                if (gump is GridHighlightMenu w && !w.IsDisposed)
                {
                    w.BringOnTop();
                    return;
                }
            }

            UIManager.Add(new GridHighlightMenu(world));
        }

        private void Build()
        {
            var root = new VerticalStackPanel
            {
                Spacing = MyraStyle.STANDARD_SPACING,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            root.Widgets.Add(new MyraLabel(TazLang.Get("gridhighlight_settings_desc"), MyraLabel.TextStyle.P) { Width = 400 });

            root.Widgets.Add(BuildToolbar());

            RebuildList();
            root.Widgets.Add(new ScrollViewer
            {
                MaxHeight = 400,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Content = _listPanel
            });

            SetRootContent(root);
        }

        private Widget BuildToolbar()
        {
            var toolbar = new HorizontalStackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };

            toolbar.Widgets.Add(new MyraButton(TazLang.Get("gridhighlight_add"), () =>
            {
                // Passing the current count appends a fresh entry, then we redraw the list.
                GridHighlightData.GetGridHighlightData(GridHighlightsConfig.Current.Highlights.Count);
                GridHighlightData.ConfigurationChanged();
                RebuildList();
            }));

            toolbar.Widgets.Add(new MyraButton(TazLang.Get("gridhighlight_export"), () => ExportGridHighlightSettings(_world)));

            toolbar.Widgets.Add(new MyraButton(TazLang.Get("gridhighlight_import"), () => ImportGridHighlightSettings(_world)));

            toolbar.Widgets.Add(new MyraButton(TazLang.Get("gridhighlight_configs"), () => GridHighlightConfig.Show(_world)));

            return toolbar;
        }

        private void RebuildList()
        {
            _listPanel.Widgets.Clear();

            int count = GridHighlightsConfig.Current.Highlights.Count;
            if (count == 0)
            {
                _listPanel.Widgets.Add(new MyraLabel(TazLang.Get("gridhighlight_settings_desc"), MyraLabel.TextStyle.P));
                ForceSizeUpdate();
                return;
            }

            for (int i = 0; i < count; i++)
                _listPanel.Widgets.Add(BuildRow(i));

            ForceSizeUpdate();
        }

        private Widget BuildRow(int keyLoc)
        {
            GridHighlightData data = GridHighlightData.GetGridHighlightData(keyLoc);

            var row = new Grid
            {
                ColumnSpacing = 4,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center
            };

            row.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            row.ColumnsProportions.Add(new Proportion(ProportionType.Fill));
            row.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            row.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            row.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            row.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            row.ColumnsProportions.Add(new Proportion(ProportionType.Auto));

            AddRowWidget(row, MyraCheckButton.CreateWithCallback(data.Enabled, isChecked =>
            {
                data.Enabled = isChecked;
                GridHighlightData.ConfigurationChanged();
            }, tooltip: TazLang.Get("gridhighlight_enabled_tooltip")), 0);

            var nameBox = new MyraInputBox
            {
                Text = data.Name ?? "",
                MinWidth = 150,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            nameBox.TextChangedByUser += (_, _) => data.Name = nameBox.Text ?? "";
            nameBox.LostFocus = () => GridHighlightData.ConfigurationChanged();
            AddRowWidget(row, nameBox, 1);

            var colorButton = new GridHighlightColorButton(TazLang.Get("gridhighlight_color"))
            {
                Tooltip = TazLang.Get("gridhighlight_color_tooltip")
            };
            colorButton.SetColor(data.HighlightColor);
            colorButton.OnClick = () => RGBColorPickerGump.Open(data.HighlightColor, selectedColor =>
            {
                data.HighlightColor = selectedColor;
                colorButton.SetColor(selectedColor);
                GridHighlightData.ConfigurationChanged();
            });
            AddRowWidget(row, colorButton, 2);

            AddRowWidget(row, new MyraButton(TazLang.Get("gridhighlight_properties"), () => GridHighlightProperties.Show(_world, keyLoc)), 3);

            AddRowWidget(row, new MyraButton(TazLang.Get("gridhighlight_up"), () =>
            {
                data.Move(true);
                ClosePropertyEditors();
                GridHighlightData.ConfigurationChanged();
                RebuildList();
            }) { Tooltip = TazLang.Get("gridhighlight_up_tooltip") }, 4);

            AddRowWidget(row, new MyraButton(TazLang.Get("gridhighlight_down"), () =>
            {
                data.Move(false);
                ClosePropertyEditors();
                GridHighlightData.ConfigurationChanged();
                RebuildList();
            }) { Tooltip = TazLang.Get("gridhighlight_down_tooltip") }, 5);

            AddRowWidget(row, MyraStyle.ApplyButtonDangerStyle(new MyraButton("X", () =>
            {
                data.Delete();
                ClosePropertyEditors();
                RebuildList();
                GridHighlightData.ConfigurationChanged();
            }) { Tooltip = TazLang.Get("gridhighlight_delete_tooltip") }), 6);

            return row;
        }

        private static void AddRowWidget(Grid row, Widget widget, int column)
        {
            row.Widgets.Add(widget);
            Grid.SetColumn(widget, column);
        }

        private sealed class GridHighlightColorButton : MyraButton
        {
            private Color _color;
            private bool _hasColor;

            public GridHighlightColorButton(string text) : base(text)
            {
            }

            public void SetColor(Color color)
            {
                _color = color;
                _hasColor = true;
                ApplyColorButtonStyle(this, color);
            }

            protected override void InternalSetStyle(Stylesheet stylesheet, string name)
            {
                base.InternalSetStyle(stylesheet, name);

                if (_hasColor)
                    ApplyColorButtonStyle(this, _color);
            }
        }

        private static void ApplyColorButtonStyle(MyraButton button, Color color)
        {
            var brush = new SolidBrush(color);
            button.Background = brush;
            button.OverBackground = brush;
            button.PressedBackground = brush;
            button.DisabledBackground = brush;
        }

        private static void ClosePropertyEditors()
        {
            foreach (IGui gump in UIManager.Gumps.ToArray())
                if (gump is GridHighlightProperties properties && !properties.IsDisposed)
                    properties.Dispose();
        }

        private static void ExportGridHighlightSettings(World world)
        {
            List<GridHighlightSetupEntry> data = GridHighlightsConfig.Current.Highlights;

            RunFileDialog(world, true, TazLang.Get("gridhighlight_export_dialog"), file =>
            {
                try
                {
                    if (Directory.Exists(file))
                    {
                        // If the path is a directory, append default filename
                        file = Path.Combine(file, "highlights.json");
                    }
                    else if (!Path.HasExtension(file))
                    {
                        // If it's not a directory and has no extension, assume they meant a file name
                        file += ".json";
                    }

                    string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(file, json);
                    GameActions.Print(world, TazLang.Get("gridhighlight_export_success", [file]));
                }
                catch (Exception ex)
                {
                    GameActions.Print(world, TazLang.Get("gridhighlight_export_error"), Constants.HUE_ERROR);
                    Log.Error(ex.ToString());
                }
            });
        }

        private static void ImportGridHighlightSettings(World world) => RunFileDialog(world, false, TazLang.Get("gridhighlight_import_dialog"), file =>
        {
            try
            {
                if (!File.Exists(file))
                    return;

                string json = File.ReadAllText(file);
                using JsonDocument document = JsonDocument.Parse(json);
                List<GridHighlightSetupEntry> imported;

                if (document.RootElement.ValueKind == JsonValueKind.Array)
                {
                    imported = DeserializeEntries(document.RootElement);
                }
                else if (document.RootElement.ValueKind == JsonValueKind.Object &&
                         TryGetPropertyIgnoreCase(document.RootElement, "highlights", out JsonElement entries))
                {
                    imported = DeserializeEntries(entries);
                }
                else
                {
                    imported = null;
                }

                if (imported != null)
                {
                    MigrateImportedLegacyColors(imported, document.RootElement);
                    imported = imported.Where(entry => entry != null).ToList();
                    foreach (GridHighlightSetupEntry entry in imported)
                        entry.Normalize();

                    GridHighlightsConfig.Current.Highlights.AddRange(imported);
                    GridHighlightsConfig.Current.Normalize();
                    ClosePropertyEditors();
                    GridHighlightData.ConfigurationChanged();

                    foreach (IGui gump in UIManager.Gumps)
                    {
                        if (gump is GridHighlightMenu w && !w.IsDisposed)
                        {
                            w.RebuildList();
                            break;
                        }
                    }

                    GameActions.Print(world, TazLang.Get("gridhighlight_import_success", [file]));
                }
            }
            catch (Exception ex)
            {
                GameActions.Print(world, TazLang.Get("gridhighlight_import_error"), Constants.HUE_ERROR);
                Log.Error(ex.ToString());
            }
        });

        private static List<GridHighlightSetupEntry> DeserializeEntries(JsonElement entries)
        {
            string json = entries.GetRawText();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            if (json.Contains("\"highlight_color\"", StringComparison.OrdinalIgnoreCase))
                options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            return JsonSerializer.Deserialize<List<GridHighlightSetupEntry>>(json, options);
        }

        private static void MigrateImportedLegacyColors(List<GridHighlightSetupEntry> imported, JsonElement root)
        {
            JsonElement entries = root;
            if (root.ValueKind == JsonValueKind.Object &&
                !TryGetPropertyIgnoreCase(root, "highlights", out entries))
                return;

            if (entries.ValueKind != JsonValueKind.Array)
                return;

            int index = 0;
            foreach (JsonElement jsonEntry in entries.EnumerateArray())
            {
                if (index >= imported.Count)
                    break;

                if (jsonEntry.ValueKind == JsonValueKind.Object &&
                    !TryGetPropertyIgnoreCase(jsonEntry, "highlight_color", out _) &&
                    !TryGetPropertyIgnoreCase(jsonEntry, "highlightColor", out _))
                    GridHighLightProfile.TryMigrateLegacyHue(imported[index]);

                index++;
            }
        }

        private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static void RunFileDialog(World world, bool save, string title, Action<string> onResult) => FileSelector.ShowFileBrowser(world, save ? FileSelectorType.Directory : FileSelectorType.File, null, save ? null : ["*.json"], onResult, title);
    }
}

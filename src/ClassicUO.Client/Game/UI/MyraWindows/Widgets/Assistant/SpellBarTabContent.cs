#nullable enable
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Managers.Hotkeys;
using Myra.Graphics2D.UI;
using SDL3;

namespace ClassicUO.Game.UI.MyraWindows.Widgets.Assistant;

public static class SpellBarTabContent
{
    private static HotkeyCaptureWindow? _captureWindow;

    /// <summary>Close any open capture window; called from AssistantWindow.Dispose so a capture
    /// session doesn't outlive the window that spawned it.</summary>
    public static void Cleanup()
    {
        if (_captureWindow is { IsDisposed: false })
            _captureWindow.Dispose();

        _captureWindow = null;
    }

    public static Widget Build()
    {
        Profile profile = ProfileManager.CurrentProfile;

        var keyLabels = new MyraLabel[SpellBarManager.MaxVisibleRows, SpellBarManager.SlotCount];

        VerticalStackPanel? rightCol = null;

        string GetKeyDisplay(int visibleRow, int slot) =>
            SpellBarManager.GetKetNames(visibleRow, slot) is { Length: > 0 } s ? s : TazLang.Get("spellbar_none");

        void ApplyCapturedHotkey(int visibleRow, int slot, HotkeyBinding binding)
        {
            // The spell bar dispatches on either a key+modifier combo or a controller combo; mouse and
            // wheel bindings are not supported, so the window is opened with mouse capture disabled.
            if (binding.HasController)
                SpellBarManager.SetButtons(visibleRow, slot, SDL.SDL_Keymod.SDL_KMOD_NONE, SDL.SDL_Keycode.SDLK_UNKNOWN, binding.ControllerButtons);
            else if (binding.HasKey)
                SpellBarManager.SetButtons(visibleRow, slot, binding.Mod, binding.Key, []);
            else
                SpellBarManager.SetButtons(visibleRow, slot, SDL.SDL_Keymod.SDL_KMOD_NONE, SDL.SDL_Keycode.SDLK_UNKNOWN, []);

            keyLabels[visibleRow, slot].Text = GetKeyDisplay(visibleRow, slot);
            Game.UI.Gumps.SpellBar.SpellBar.Instance?.SetupHotkeyLabels();
        }

        void StartListening(int visibleRow, int slot)
        {
            if (_captureWindow is { IsDisposed: false })
            {
                _captureWindow.BringOnTop();
                return;
            }

            _captureWindow = new HotkeyCaptureWindow(
                prompt: TazLang.Get("spellbar_slot", new[] { slot.ToString() }),
                existing: SpellBarManager.GetSlotBinding(visibleRow, slot),
                onSaved: binding => ApplyCapturedHotkey(visibleRow, slot, binding),
                capturesMouseEvents: false);
        }

        Widget BuildVisibleRowsSelector()
        {
            var container = new MyraVerticalStackPanel { Spacing = 2 };
            var selectorRow = new MyraHorizontalStackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            string visibleRowsDescription = TazLang.Get(
                "spellbar_visiblerows_desc",
                "Choose how many spellbar rows are shown at once.");

#pragma warning disable CS0612, CS0618
            var combo = new MyraComboBox { MinWidth = 70, Tooltip = visibleRowsDescription };

            for (int i = 1; i <= SpellBarManager.MaxVisibleRows; i++)
                combo.Items.Add(new ListItem(i.ToString()) { Tag = i });

            combo.SelectedIndex = SpellBarManager.GetVisibleRowCount() - 1;
            combo.SelectedIndexChanged += (_, _) =>
            {
                if (combo.SelectedIndex == null)
                    return;

                SpellBarManager.SetVisibleRowCount(combo.SelectedIndex.Value + 1);
                Game.UI.Gumps.SpellBar.SpellBar.Instance?.Build();
                RefreshHotkeyConfig();
            };
#pragma warning restore CS0612, CS0618

            selectorRow.Widgets.Add(new MyraLabel(
                TazLang.Get("spellbar_visiblerows", "Visible spellbar rows:"),
                MyraLabel.TextStyle.P)
            { MinWidth = 130, Tooltip = visibleRowsDescription });
            selectorRow.Widgets.Add(combo);
            container.Widgets.Add(selectorRow);

            return container;
        }

        Widget BuildHotkeyGrid()
        {
            int visibleRows = SpellBarManager.GetVisibleRowCount();
            var hotkeyGrid = new MyraGrid();
            hotkeyGrid.AddColumn(new Proportion(ProportionType.Pixels, 60));

            for (int visibleRow = 0; visibleRow < visibleRows; visibleRow++)
                hotkeyGrid.AddColumn(new Proportion(ProportionType.Auto));

            hotkeyGrid.AddWidget(new MyraLabel(string.Empty, MyraLabel.TextStyle.TableHeader), 0, 0);

            for (int visibleRow = 0; visibleRow < visibleRows; visibleRow++)
            {
                hotkeyGrid.AddWidget(
                    new MyraLabel(TazLang.Get("spellbar_barhotkey", new[] { (visibleRow + 1).ToString() }), MyraLabel.TextStyle.TableHeader),
                    0,
                    visibleRow + 1
                );
            }

            for (int slot = 0; slot < SpellBarManager.SlotCount; slot++)
            {
                hotkeyGrid.AddWidget(new MyraLabel(TazLang.Get("spellbar_slot", new[] { slot.ToString() }), MyraLabel.TextStyle.P), slot + 1, 0);

                for (int visibleRow = 0; visibleRow < visibleRows; visibleRow++)
                {
                    int rowIndex = visibleRow;
                    int slotIndex = slot;

                    keyLabels[rowIndex, slotIndex] = new MyraLabel(GetKeyDisplay(rowIndex, slotIndex), MyraLabel.TextStyle.P)
                    {
                        MinWidth = 78
                    };

                    var actionsContainer = new MyraHorizontalStackPanel { Spacing = 4 };
                    actionsContainer.Widgets.Add(new MyraButton(TazLang.Get("spellbar_set"), () => StartListening(rowIndex, slotIndex)));
                    actionsContainer.Widgets.Add(new MyraButton(TazLang.Get("spellbar_clear"), () =>
                    {
                        SpellBarManager.SetButtons(rowIndex, slotIndex, SDL.SDL_Keymod.SDL_KMOD_NONE, SDL.SDL_Keycode.SDLK_UNKNOWN, []);
                        keyLabels[rowIndex, slotIndex].Text = GetKeyDisplay(rowIndex, slotIndex);
                        Game.UI.Gumps.SpellBar.SpellBar.Instance?.SetupHotkeyLabels();
                    }));

                    var cell = new MyraHorizontalStackPanel { Spacing = 4 };
                    cell.Widgets.Add(keyLabels[rowIndex, slotIndex]);
                    cell.Widgets.Add(actionsContainer);

                    hotkeyGrid.AddWidget(cell, slot + 1, visibleRow + 1);
                }
            }

            return hotkeyGrid;
        }

        void RefreshHotkeyConfig()
        {
            Cleanup();

            if (rightCol == null)
                return;

            rightCol.Widgets.Clear();
            rightCol.Widgets.Add(new MyraLabel(TazLang.Get("spellbar_hotkeyconfig"), MyraLabel.TextStyle.H2));
            rightCol.Widgets.Add(BuildHotkeyGrid());
        }

        var leftCol = new MyraVerticalStackPanel { Spacing = 6 };

        leftCol.Widgets.Add(MyraCheckButton.CreateWithCallback(
            SpellBarManager.IsEnabled(),
            _ =>
            {
                if (SpellBarManager.ToggleEnabled())
                    UIManager.Add(new Game.UI.Gumps.SpellBar.SpellBar(Client.Game.UO.World));
                else
                    Game.UI.Gumps.SpellBar.SpellBar.Instance?.Dispose();
            },
            TazLang.Get("spellbar_enable"), TazLang.Get("spellbar_enable_tooltip")));

        leftCol.Widgets.Add(MyraCheckButton.CreateWithCallback(
            profile.SpellBar_ShowHotkeys,
            b =>
            {
                profile.SpellBar_ShowHotkeys = b;
                Game.UI.Gumps.SpellBar.SpellBar.Instance?.SetupHotkeyLabels();
            },
            TazLang.Get("spellbar_showhotkeys"), TazLang.Get("spellbar_showhotkeys_tooltip")));

        leftCol.Widgets.Add(BuildVisibleRowsSelector());

        leftCol.Widgets.Add(new MyraSpacer(15, 5));
        leftCol.Widgets.Add(new MyraLabel(TazLang.Get("spellbar_rowmanagement"), MyraLabel.TextStyle.H2));
        var rowBtns = new MyraHorizontalStackPanel { Spacing = 4 };
        rowBtns.Widgets.Add(new MyraButton(TazLang.Get("spellbar_addrow_btn"), () =>
        {
            SpellBarManager.SpellBarRows.Add(new SpellBarRow());
            Game.UI.Gumps.SpellBar.SpellBar.Instance?.Build();
        }) { Tooltip = TazLang.Get("spellbar_addrow_tooltip") });
        rowBtns.Widgets.Add(new MyraButton(TazLang.Get("spellbar_removerow_btn"), () =>
        {
            if (SpellBarManager.SpellBarRows.Count > SpellBarManager.GetVisibleRowCount())
                SpellBarManager.SpellBarRows.RemoveAt(SpellBarManager.SpellBarRows.Count - 1);

            Game.UI.Gumps.SpellBar.SpellBar.Instance?.Build();
        }) { Tooltip = TazLang.Get("spellbar_removerow_tooltip") });
        leftCol.Widgets.Add(rowBtns);

        leftCol.Widgets.Add(new MyraSpacer(15, 5));
        leftCol.Widgets.Add(new MyraLabel(TazLang.Get("spellbar_presetmanagement"), MyraLabel.TextStyle.H2));

        var presetSavePanel = new MyraVerticalStackPanel { Spacing = 4, Visible = false };
        var presetNameBox = new MyraInputBox { MinWidth = 150, HintText = TazLang.Get("spellbar_savepreset_name") };
        var presetSaveRow = new MyraHorizontalStackPanel { Spacing = 4 };
        presetSaveRow.Widgets.Add(new MyraLabel(TazLang.Get("spellbar_name"), MyraLabel.TextStyle.P));
        presetSaveRow.Widgets.Add(presetNameBox);
        presetSaveRow.Widgets.Add(new MyraButton(TazLang.Get("spellbar_save"), () =>
        {
            if (!string.IsNullOrEmpty(presetNameBox.Text))
            {
                SpellBarManager.SaveCurrentRowPreset(presetNameBox.Text);
                presetNameBox.Text = "";
                presetSavePanel.Visible = false;
            }
        }));
        presetSaveRow.Widgets.Add(new MyraButton(TazLang.Get("spellbar_cancel"), () =>
        {
            presetNameBox.Text = "";
            presetSavePanel.Visible = false;
        }));
        presetSavePanel.Widgets.Add(presetSaveRow);

        var presetLoadPanel = new MyraVerticalStackPanel { Spacing = 4, Visible = false };
        var presetListPanel = new MyraVerticalStackPanel { Spacing = 2 };
        presetLoadPanel.Widgets.Add(presetListPanel);
        presetLoadPanel.Widgets.Add(new MyraButton(TazLang.Get("spellbar_cancel"), () => presetLoadPanel.Visible = false));

        var presetActionBtns = new MyraHorizontalStackPanel { Spacing = 4 };
        presetActionBtns.Widgets.Add(new MyraButton(TazLang.Get("spellbar_savepreset_btn"), () =>
        {
            presetLoadPanel.Visible = false;
            presetSavePanel.Visible = !presetSavePanel.Visible;
        }) { Tooltip = TazLang.Get("spellbar_savepreset_tooltip") });
        presetActionBtns.Widgets.Add(new MyraButton(TazLang.Get("spellbar_loadpreset_btn"), () =>
        {
            presetSavePanel.Visible = false;
            presetListPanel.Widgets.Clear();

            string[] presets = SpellBarManager.ListPresets();

            if (presets.Length == 0)
            {
                presetListPanel.Widgets.Add(new MyraLabel(TazLang.Get("spellbar_nopresets"), MyraLabel.TextStyle.P));
            }
            else
            {
                presetListPanel.Widgets.Add(new MyraLabel(TazLang.Get("spellbar_selectpreset"), MyraLabel.TextStyle.P));

                foreach (string preset in presets)
                {
                    string p = preset;
                    presetListPanel.Widgets.Add(new MyraButton(p, () =>
                    {
                        SpellBarManager.ImportPreset(p);
                        presetLoadPanel.Visible = false;
                    }));
                }
            }

            presetLoadPanel.Visible = !presetLoadPanel.Visible;
        }) { Tooltip = TazLang.Get("spellbar_loadpreset_tooltip") });

        leftCol.Widgets.Add(presetActionBtns);
        leftCol.Widgets.Add(presetSavePanel);
        leftCol.Widgets.Add(presetLoadPanel);

        rightCol = new MyraVerticalStackPanel { Spacing = 6 };
        RefreshHotkeyConfig();

        var root = new MyraHorizontalStackPanel { Spacing = 20 };
        root.Widgets.Add(leftCol);
        root.Widgets.Add(rightCol);

        return root;
    }
}

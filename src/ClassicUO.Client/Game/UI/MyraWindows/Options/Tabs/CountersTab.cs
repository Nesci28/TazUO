using ClassicUO.Common;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Game.UI.MyraWindows.Widgets;

namespace ClassicUO.Game.UI.MyraWindows.Options.Tabs;

/// <summary>Options tab source for the counter-bar feature settings</summary>
public static class CountersTab
{
    /// <summary>Returns the option fragment for counter-bar enable/disable and display configuration</summary>
    internal static IOptionSource GetContent()
    {
        Profile profile = ProfileManager.CurrentProfile;

        return OptionsUi.CheckBoxGroup(
            new PropertyBinder(
                new Accessor<bool>(
                    () => profile.CounterBarEnabled,
                    b =>
                    {
                        profile.CounterBarEnabled = b;

                        if (b && CounterBarGump.CurrentCounterBarGump == null)
                            CounterBarGump.AddNew(World.Instance);
                        else
                            CounterBarGump.SetAllVisible(b);
                    }
                ),
                TazLang.Get("mog_counters_enablecounters")
            ),
            Option.Checkbox(
                TazLang.Get("mog_counters_showhotkeys"),
                new Accessor<bool>(() => profile.CounterBarShowHotkeys, b =>
                {
                    profile.CounterBarShowHotkeys = b;
                    CounterBarGump.RefreshAllHotkeyLabels();
                }),
                search: new SearchMetadata(TazLang.Get("mog_counters_showhotkeys"), Keywords: [TazLang.Get("mog_kw_counter"), TazLang.Get("mog_kw_hotkey")])
            ),
            Option.Checkbox(
                TazLang.Get("mog_counters_disableitemscaling"),
                new Accessor<bool>(() => profile.CounterBarDisableItemScaling, b => profile.CounterBarDisableItemScaling = b),
                search: new SearchMetadata(TazLang.Get("mog_counters_disableitemscaling"), Keywords: [TazLang.Get("mog_kw_counter"), TazLang.Get("mog_kw_item"), TazLang.Get("mog_kw_scaling")])
            ),
            Option.Checkbox(
                TazLang.Get("mog_counters_disableiconscaling"),
                new Accessor<bool>(() => profile.CounterBarDisableIconScaling, b => profile.CounterBarDisableIconScaling = b),
                search: new SearchMetadata(TazLang.Get("mog_counters_disableiconscaling"), Keywords: [TazLang.Get("mog_kw_counter"), TazLang.Get("mog_kw_icon"), TazLang.Get("mog_kw_spell"), TazLang.Get("mog_kw_scaling")])
            ),
            Option.Button(
                TazLang.Get("mog_counters_addbar", "Add counter bar"),
                () => CounterBarGump.AddNew(World.Instance, SelectedBar),
                search: new SearchMetadata(TazLang.Get("mog_counters_addbar", "Add counter bar"), Keywords: [TazLang.Get("mog_kw_counter")])
            ),
            Option.Button(
                TazLang.Get("mog_counters_removebar", "Remove selected counter bar"),
                () => SelectedBar?.RemoveBar(),
                search: new SearchMetadata(TazLang.Get("mog_counters_removebar", "Remove selected counter bar"), Keywords: [TazLang.Get("mog_kw_counter")])
            ),
            GetAbbreviationGroup(),
            GetHighlightGroup(),
            GetLayoutGroup()
        ).WithSearch(new SearchMetadata(TazLang.Get("mog_counters_enablecounters"), Tags: [TazLang.Get("mog_kw_counter"), TazLang.Get("mog_kw_reagent")], Keywords: [TazLang.Get("mog_kw_counter")]));
    }

    private static OptionFragment GetAbbreviationGroup()
    {
        Profile profile = ProfileManager.CurrentProfile;

        return OptionsUi.CheckBoxGroup(
            new PropertyBinder(new Accessor<bool>(() => profile.CounterBarDisplayAbbreviatedAmount), TazLang.Get("mog_counters_abbreviatedvalues")),
            Option.IntegerInput(
                TazLang.Get("mog_counters_abbreviateifamountexceeds"),
                new Accessor<int>(() => profile.CounterBarAbbreviatedAmount),
                min: 999,
                max: 999999999,
                search: new SearchMetadata(TazLang.Get("mog_counters_abbreviateifamountexceeds"), Keywords: [TazLang.Get("mog_kw_abbreviate"), TazLang.Get("mog_kw_amount"), TazLang.Get("mog_kw_exceed")])
            )
        ).WithSearch(new SearchMetadata(TazLang.Get("mog_counters_enablecounters"), Tags: [TazLang.Get("mog_kw_counter")], Keywords: [TazLang.Get("mog_kw_abbreviate")]));
    }

    private static OptionFragment GetHighlightGroup()
    {
        Profile profile = ProfileManager.CurrentProfile;

        return OptionsUi.VisualContainer(
            new VisualContainerProps { LabelText = TazLang.Get("mog_counters_sectionhighlightinglabel") },
            Option.Checkbox(
                TazLang.Get("mog_counters_highlightitemsonuse"),
                new Accessor<bool>(() => profile.CounterBarHighlightOnUse),
                search: new SearchMetadata(TazLang.Get("mog_counters_highlightitemsonuse"), Keywords: [TazLang.Get("mog_kw_highlight"), TazLang.Get("mog_kw_item"), TazLang.Get("mog_kw_use")])
            ),
            OptionsUi.CheckBoxGroup(
                new PropertyBinder(new Accessor<bool>(() => profile.CounterBarHighlightOnAmount), TazLang.Get("mog_counters_highlightredwhenamountislow")),
                Option.IntegerInput(
                    TazLang.Get("mog_counters_highlightredifamountisbelow"),
                    new Accessor<int>(() => profile.CounterBarHighlightAmount),
                    min: 1,
                    max: 60000,
                    search: new SearchMetadata(TazLang.Get("mog_counters_highlightredifamountisbelow"), Keywords: [TazLang.Get("mog_kw_highlight"), TazLang.Get("mog_kw_amount"), TazLang.Get("mog_kw_below")])
                )
            ).WithSearch(new SearchMetadata(TazLang.Get("mog_counters_sectionhighlightinglabel"), Tags: [TazLang.Get("mog_kw_counter")], Keywords: [TazLang.Get("mog_kw_highlight")]))
        );
    }

    private static OptionFragment GetLayoutGroup()
    {
        Profile profile = ProfileManager.CurrentProfile;

        return OptionsUi.VisualContainer(
            new VisualContainerProps { LabelText = TazLang.Get("mog_counters_selectedlayout", "Selected counter bar layout") },
            Option.Slider(
                TazLang.Get("mog_counters_gridsize"),
                30,
                80,
                new Accessor<float>(() => SelectedBar?.CellSize ?? profile.CounterBarCellSize, v =>
                {
                    profile.CounterBarCellSize = (int)v;
                    CounterBarGump bar = SelectedBar;
                    bar?.SetLayout(profile.CounterBarCellSize, bar.Rows, bar.Columns);
                }),
                search: new SearchMetadata(TazLang.Get("mog_counters_gridsize"), Keywords: [TazLang.Get("mog_kw_grid"), TazLang.Get("mog_kw_size")])
            ),
            Option.IntegerInput(
                TazLang.Get("mog_counters_rows"),
                new Accessor<int>(() => SelectedBar?.Rows ?? profile.CounterBarRows, v =>
                {
                    profile.CounterBarRows = v;
                    CounterBarGump bar = SelectedBar;
                    bar?.SetLayout(bar.CellSize, profile.CounterBarRows, bar.Columns);
                }),
                min: 1,
                max: 30,
                search: new SearchMetadata(TazLang.Get("mog_counters_rows"), Keywords: [TazLang.Get("mog_kw_row")])
            ),
            Option.IntegerInput(
                TazLang.Get("mog_counters_columns"),
                new Accessor<int>(() => SelectedBar?.Columns ?? profile.CounterBarColumns, v =>
                {
                    profile.CounterBarColumns = v;
                    CounterBarGump bar = SelectedBar;
                    bar?.SetLayout(bar.CellSize, bar.Rows, profile.CounterBarColumns);
                }),
                min: 1,
                max: 30,
                search: new SearchMetadata(TazLang.Get("mog_counters_columns"), Keywords: [TazLang.Get("mog_kw_column")])
            )
        );
    }

    private static CounterBarGump SelectedBar =>
        CounterBarGump.SelectedCounterBarGump ?? CounterBarGump.CurrentCounterBarGump;
}

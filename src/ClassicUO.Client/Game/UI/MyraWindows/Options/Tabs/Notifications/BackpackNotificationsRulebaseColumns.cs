using System;
using ClassicUO.Common;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.MyraWindows.Options.Editors.Rulebase;
using ClassicUO.Game.UI.MyraWindows.Widgets;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace ClassicUO.Game.UI.MyraWindows.Options.Tabs.Notifications;

internal static partial class NotificationsTab
{
    private const int BackpackRuleCardWidth = 900;
    private const int BackpackRuleEditorWidth = BackpackRuleCardWidth + 24;

    private static RulebaseColumn<BackpackNotificationRule>[] GetBackpackRulebaseColumns()
    {
        return
        [
            new RulebaseColumn<BackpackNotificationRule>
            {
                CellContentAlignment = HorizontalAlignment.Center,
                Proportion = new Proportion(ProportionType.Fill),
                CellFactory = CreateRuleCard
            }
        ];
    }

    private static Widget CreateRuleCard(BackpackNotificationRule rule)
    {
        Widget name = CreateTextInput(
                TazLang.Get("backpacknotifications_rule", "Rule"),
                rule.Name,
                value => rule.Name = value,
                null,
                150
        );
        Widget enabled = MyraCheckButton.CreateWithCallback(
            rule.Enabled,
            value => rule.Enabled = value,
            TazLang.Get("backpacknotifications_enabled", "Enabled")
        );
        Widget destination = Labeled(
            TazLang.Get("backpacknotifications_destination", "Notify via"),
            CreateDestinationCell(rule)
        );
        Widget test = new MyraButton(
            TazLang.Get("mog_kw_test", "Test"),
            () => BackpackNotificationManager.Instance.TestNotification(rule.ToEntry())
        )
        {
            MinWidth = 60,
            Tooltip = TazLang.Get("backpacknotifications_testtooltip", "Show a sample notification using this rule's current destination and appearance.")
        };
        Widget graphic = Sized(
            OptionsFactory.PropBoundIntInput(
                TazLang.Get("backpacknotifications_itemkind", "Item kind"),
                new Accessor<int>(() => rule.Graphic),
                -1,
                ushort.MaxValue,
                TazLang.Get("backpacknotifications_itemkindtooltip", "Graphic ID. Use -1 for any item kind.")
            ),
            140
        );
        Widget hues = CreateTextInput(
                TazLang.Get("backpacknotifications_itemhues", "Item hues"),
                rule.Hues,
                value => rule.Hues = value,
                TazLang.Get("backpacknotifications_itemhuestooltip", "Item hues. Use -1 for any hue, or separate multiple hues with commas."),
                125
        );
        Widget regex = CreateTextInput(
                TazLang.Get("backpacknotifications_regex", "Regex"),
                rule.RegexSearch,
                value => rule.RegexSearch = value,
                TazLang.Get("backpacknotifications_regextooltip", "Regex matched against item name and properties."),
                300
        );
        Widget hue = Labeled(
            TazLang.Get("backpacknotifications_hue", "Hue"),
            CreateHueCell(rule)
        );
        Widget font = Labeled(
            TazLang.Get("mog_chattab_fonttab_fontlabel", "Font"),
            CreateOnScreenFontCell(rule)
        );
        Widget size = Labeled(
            TazLang.Get("mog_chattab_fonttab_size", "Size"),
            CreateOnScreenFontSizeCell(rule)
        );
        Widget announcement = CreateTextInput(
                TazLang.Get("backpacknotifications_announcement", "Announcement"),
                rule.Announcement,
                value => rule.Announcement = value,
                TazLang.Get("backpacknotifications_announcementtooltip", "Announcement text. Supports {rule} and {item}.")
        );

        StackPanel card = OptionTabCommons.StyledStackPanel(
            Orientation.Vertical,
            CreateCardSection(
                TazLang.Get("backpacknotifications_general", "General"),
                (name, 3f),
                (enabled, 1.25f),
                (destination, 2.25f),
                (test, 1f)
            ),
            CreateCardSection(
                TazLang.Get("backpacknotifications_match", "Match"),
                (graphic, 1.25f),
                (hues, 1.75f),
                (regex, 3.25f)
            ),
            CreateCardSection(
                TazLang.Get("mog_kw_appearance", "Appearance"),
                (hue, 1.25f),
                (font, 2.25f),
                (size, 1.25f)
            ),
            CreateMessageRow(TazLang.Get("backpacknotifications_message", "Message"), announcement)
        );
        card.Width = BackpackRuleCardWidth;
        card.HorizontalAlignment = HorizontalAlignment.Center;
        return card;
    }

    private static Widget CreateCardSection(string heading, params (Widget Widget, float Weight)[] fields)
    {
        StackPanel section = OptionTabCommons.StyledStackPanel(
            Orientation.Vertical,
            CreateSectionHeader(heading),
            CreateFieldsGrid(fields)
        );
        section.HorizontalAlignment = HorizontalAlignment.Stretch;
        return section;
    }

    private static MyraGrid CreateSectionHeader(string heading)
    {
        var header = new MyraGrid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(2)
        };
        header.AddColumn(new Proportion(ProportionType.Auto));
        header.AddColumn(new Proportion(ProportionType.Part, 1));
        header.AddRow(new Proportion(ProportionType.Auto));

        header.AddWidget(new MyraLabel(heading, MyraLabel.TextStyle.H5), 0, 0);
        Widget separator = OptionTabCommons.StyledHorizontalSeparator();
        separator.HorizontalAlignment = HorizontalAlignment.Stretch;
        separator.VerticalAlignment = VerticalAlignment.Center;
        separator.Margin = new Thickness(MyraStyle.STANDARD_SPACING, 0, 0, 0);
        header.AddWidget(separator, 0, 1);
        return header;
    }

    private static MyraGrid CreateFieldsGrid(params (Widget Widget, float Weight)[] fields)
    {
        var grid = new MyraGrid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ColumnSpacing = MyraStyle.STANDARD_SPACING * 2,
            Margin = new Thickness(42, 4, 8, 8)
        };
        grid.AddRow(new Proportion(ProportionType.Auto));

        for (int i = 0; i < fields.Length; i++)
        {
            grid.AddColumn(new Proportion(ProportionType.Part, fields[i].Weight));
            fields[i].Widget.HorizontalAlignment = HorizontalAlignment.Left;
            fields[i].Widget.VerticalAlignment = VerticalAlignment.Center;
            grid.AddWidget(fields[i].Widget, 0, i);
        }

        return grid;
    }

    private static MyraGrid CreateMessageRow(string heading, Widget announcement)
    {
        var row = new MyraGrid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ColumnSpacing = MyraStyle.STANDARD_SPACING,
            Margin = new Thickness(2, 4, 8, 8)
        };
        row.AddColumn(new Proportion(ProportionType.Pixels, 132));
        row.AddColumn(new Proportion(ProportionType.Part, 1));
        row.AddRow(new Proportion(ProportionType.Auto));
        row.AddWidget(new MyraLabel(heading, MyraLabel.TextStyle.H5), 0, 0);
        row.AddWidget(announcement, 0, 1);
        return row;
    }

    private static MyraGrid CreateTextInput(
        string label,
        string value,
        Action<string> onChange,
        string tooltip,
        int? inputWidth = null
    )
    {
        var input = new MyraInputBox
        {
            Text = value,
            Tooltip = tooltip,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        if (inputWidth.HasValue)
            input.Width = inputWidth.Value;

        input.TextChangedByUser += (_, _) => onChange(input.Text);

        var row = new MyraGrid { HorizontalAlignment = HorizontalAlignment.Stretch };
        row.AddColumn(new Proportion(ProportionType.Auto));
        row.AddColumn(
            inputWidth.HasValue
                ? new Proportion(ProportionType.Pixels, inputWidth.Value)
                : new Proportion(ProportionType.Part, 1)
        );
        row.AddRow(new Proportion(ProportionType.Auto));
        row.AddWidget(new MyraLabel(label, MyraLabel.TextStyle.P) { Tooltip = tooltip }, 0, 0);
        row.AddWidget(input, 0, 1);

        return row;
    }

    private static Widget CreateDestinationCell(BackpackNotificationRule rule)
    {
        DestinationChoice[] choices =
        [
            new(BackpackNotificationDestination.Journal, TazLang.Get("backpacknotifications_journal", "Journal")),
            new(BackpackNotificationDestination.Overhead, TazLang.Get("backpacknotifications_overhead", "Overhead")),
            new(BackpackNotificationDestination.OnScreen, TazLang.Get("backpacknotifications_onscreen", "On-screen"))
        ];

        int selectedIndex = Array.FindIndex(choices, choice => choice.Value == rule.Destination);
        DestinationChoice selected = choices[selectedIndex >= 0 ? selectedIndex : 0];
        Widget combo = OptionTabCommons.CreateOptionsComboBox(
            null,
            selected,
            choices,
            choice => rule.Destination = choice.Value,
            TazLang.Get("backpacknotifications_destinationtooltip", "Choose where this notification is displayed.")
        );
        combo.MinWidth = 100;
        combo.Width = 100;
        return combo;
    }

    private static Widget CreateHueCell(BackpackNotificationRule rule)
    {
        var hueLabel = new MyraLabel(rule.Hue.ToString(), MyraLabel.TextStyle.P);

        return OptionTabCommons.StyledStackPanel(
            Orientation.Horizontal,
            OptionsFactory.CreateHuePicker(
                null,
                rule.Hue,
                newHue =>
                {
                    rule.Hue = newHue;
                    hueLabel.Text = newHue.ToString();
                }
            ),
            hueLabel
        );
    }

    private static Widget CreateOnScreenFontCell(BackpackNotificationRule rule)
    {
        Widget selector = OptionTabCommons.StyledFontSelector(
            null,
            new Accessor<string>(() => rule.OnScreenFont)
        );
        selector.MinWidth = 125;
        selector.Width = 125;
        return EnableForOnScreen(rule, selector);
    }

    private static Widget CreateOnScreenFontSizeCell(BackpackNotificationRule rule) =>
        Sized(EnableForOnScreen(
            rule,
            OptionsFactory.PropBoundIntInput(
                null,
                new Accessor<int>(() => rule.OnScreenFontSize),
                5,
                50,
                TazLang.Get("backpacknotifications_fontsizetooltip", "Font size used for On-screen notifications.")
            )
        ), 72);

    private static Widget Labeled(string label, Widget widget) =>
        OptionTabCommons.StyledStackPanel(
            Orientation.Horizontal,
            new MyraLabel(label, MyraLabel.TextStyle.P),
            widget
        );

    private static Widget Sized(Widget widget, int width)
    {
        widget.Width = width;
        return widget;
    }

    private static Widget EnableForOnScreen(BackpackNotificationRule rule, Widget widget)
    {
        widget.Enabled = rule.Destination == BackpackNotificationDestination.OnScreen;
        rule.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BackpackNotificationRule.Destination))
                widget.Enabled = rule.Destination == BackpackNotificationDestination.OnScreen;
        };
        return widget;
    }

    private readonly record struct DestinationChoice(BackpackNotificationDestination Value, string Label)
    {
        public override string ToString() => Label;
    }
}

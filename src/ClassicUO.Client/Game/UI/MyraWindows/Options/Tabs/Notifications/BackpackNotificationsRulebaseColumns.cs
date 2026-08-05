using System;
using ClassicUO.Common;
using ClassicUO.Configuration;
using ClassicUO.Game.UI.MyraWindows.Options.Editors.Rulebase;
using ClassicUO.Game.UI.MyraWindows.Widgets;
using Myra.Graphics2D.UI;

namespace ClassicUO.Game.UI.MyraWindows.Options.Tabs.Notifications;

internal static partial class NotificationsTab
{
    private static RulebaseColumn<BackpackNotificationRule>[] GetBackpackRulebaseColumns()
    {
        return
        [
            new RulebaseColumn<BackpackNotificationRule>
            {
                Header = TazLang.Get("backpacknotifications_rule", "Rule"),
                Proportion = new Proportion(ProportionType.Auto),
                CellFactory = rule => OptionsFactory.PropBoundInputField(null, new Accessor<string>(() => rule.Name))
            },
            new RulebaseColumn<BackpackNotificationRule>
            {
                Header = TazLang.Get("backpacknotifications_enabled", "Enabled"),
                CellContentAlignment = HorizontalAlignment.Center,
                Proportion = new Proportion(ProportionType.Auto),
                CellFactory = rule => MyraCheckButton.CreateWithCallback(rule.Enabled, enabled => rule.Enabled = enabled)
            },
            new RulebaseColumn<BackpackNotificationRule>
            {
                Header = TazLang.Get("backpacknotifications_itemkind", "Item Kind"),
                HeaderTooltip = TazLang.Get("backpacknotifications_itemkindtooltip", "Graphic ID. Use -1 for any item kind."),
                Proportion = new Proportion(ProportionType.Auto),
                CellFactory = rule => OptionsFactory.PropBoundIntInput(
                    string.Empty,
                    new Accessor<int>(() => rule.Graphic),
                    -1,
                    ushort.MaxValue,
                    TazLang.Get("backpacknotifications_itemkindtooltip", "Graphic ID. Use -1 for any item kind.")
                )
            },
            new RulebaseColumn<BackpackNotificationRule>
            {
                Header = TazLang.Get("backpacknotifications_itemhues", "Item Hues"),
                HeaderTooltip = TazLang.Get("backpacknotifications_itemhuestooltip", "Item hues. Use -1 for any hue, or separate multiple hues with commas."),
                Proportion = new Proportion(ProportionType.Auto),
                CellFactory = rule => OptionsFactory.PropBoundInputField(
                    null,
                    new Accessor<string>(() => rule.Hues),
                    TazLang.Get("backpacknotifications_itemhuestooltip", "Item hues. Use -1 for any hue, or separate multiple hues with commas.")
                )
            },
            new RulebaseColumn<BackpackNotificationRule>
            {
                Header = TazLang.Get("backpacknotifications_regex", "Regex"),
                HeaderTooltip = TazLang.Get("backpacknotifications_regextooltip", "Regex matched against item name and properties."),
                Proportion = new Proportion(ProportionType.Fill),
                CellFactory = rule => OptionsFactory.PropBoundInputField(
                    null,
                    new Accessor<string>(() => rule.RegexSearch),
                    TazLang.Get("backpacknotifications_regextooltip", "Regex matched against item name and properties.")
                )
            },
            new RulebaseColumn<BackpackNotificationRule>
            {
                Header = TazLang.Get("backpacknotifications_announcement", "Announcement"),
                HeaderTooltip = TazLang.Get("backpacknotifications_announcementtooltip", "Announcement text. Supports {rule} and {item}."),
                Proportion = new Proportion(ProportionType.Fill),
                CellFactory = rule => OptionsFactory.PropBoundInputField(
                    null,
                    new Accessor<string>(() => rule.Announcement),
                    TazLang.Get("backpacknotifications_announcementtooltip", "Announcement text. Supports {rule} and {item}.")
                )
            },
            new RulebaseColumn<BackpackNotificationRule>
            {
                Header = TazLang.Get("backpacknotifications_journal", "Journal"),
                CellContentAlignment = HorizontalAlignment.Center,
                Proportion = new Proportion(ProportionType.Auto),
                CellFactory = rule => CreateOutputCell(
                    rule,
                    r => r.Journal,
                    (r, v) => r.Journal = v,
                    r => r.JournalHue,
                    (r, h) => r.JournalHue = h
                )
            },
            new RulebaseColumn<BackpackNotificationRule>
            {
                Header = TazLang.Get("backpacknotifications_overhead", "Overhead"),
                CellContentAlignment = HorizontalAlignment.Center,
                Proportion = new Proportion(ProportionType.Auto),
                CellFactory = rule => CreateOutputCell(
                    rule,
                    r => r.Overhead,
                    (r, v) => r.Overhead = v,
                    r => r.OverheadHue,
                    (r, h) => r.OverheadHue = h
                )
            },
            new RulebaseColumn<BackpackNotificationRule>
            {
                Header = TazLang.Get("backpacknotifications_onscreen", "On-screen"),
                CellContentAlignment = HorizontalAlignment.Center,
                Proportion = new Proportion(ProportionType.Auto),
                CellFactory = rule => CreateOutputCell(
                    rule,
                    r => r.OnScreen,
                    (r, v) => r.OnScreen = v,
                    r => r.OnScreenHue,
                    (r, h) => r.OnScreenHue = h
                )
            }
        ];
    }

    private static Widget CreateOutputCell(
        BackpackNotificationRule rule,
        Func<BackpackNotificationRule, bool> getEnabled,
        Action<BackpackNotificationRule, bool> setEnabled,
        Func<BackpackNotificationRule, ushort> getHue,
        Action<BackpackNotificationRule, ushort> setHue
    )
    {
        var hueLabel = new MyraLabel(getHue(rule).ToString(), MyraLabel.TextStyle.P);

        return OptionTabCommons.StyledStackPanel(
            Orientation.Horizontal,
            MyraCheckButton.CreateWithCallback(getEnabled(rule), isChecked => setEnabled(rule, isChecked)),
            OptionsFactory.CreateHuePicker(
                null,
                getHue(rule),
                newHue =>
                {
                    setHue(rule, newHue);
                    hueLabel.Text = newHue.ToString();
                }
            ),
            hueLabel
        );
    }
}

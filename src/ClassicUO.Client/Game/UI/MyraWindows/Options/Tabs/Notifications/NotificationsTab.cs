using System.Collections.Generic;
using System.ComponentModel;
using ClassicUO.Common;
using ClassicUO.Configuration;
using ClassicUO.Game.UI.MyraWindows.Options.Editors.Rulebase;
using ClassicUO.Game.UI.MyraWindows.Widgets;
using ClassicUO.Utility.Collections;
using Myra.Graphics2D.UI;

namespace ClassicUO.Game.UI.MyraWindows.Options.Tabs.Notifications;

internal static partial class NotificationsTab
{
    internal static IOptionSource GetContent()
    {
        return OptionsUi.Vertical(
            GetBackpackSection()
        ).WithSearch(
            new SearchMetadata(
                TazLang.Get("backpacknotifications_category", "Notifications"),
                Tags:
                [
                    TazLang.Get("backpacknotifications_backpack", "Backpack"),
                    TazLang.Get("mog_kw_journal"),
                    TazLang.Get("backpacknotifications_overhead", "Overhead")
                ]
            )
        );
    }

    private static OptionFragment GetBackpackSection()
    {
        return OptionsUi.VisualContainer(
            new VisualContainerProps
            {
                LabelText = TazLang.Get("backpacknotifications_backpack", "Backpack Notifications")
            },
            Option.Custom(
                GetRuleEditor,
                new SearchMetadata(
                    TazLang.Get("backpacknotifications_rules", "Backpack notification rules"),
                    Keywords:
                    [
                        TazLang.Get("backpacknotifications_artifact", "Artifact"),
                        TazLang.Get("backpacknotifications_regex", "Regex"),
                        TazLang.Get("backpacknotifications_itemhues", "Hues")
                    ]
                )
            )
        );
    }

    private static Rulebase<BackpackNotificationRule> GetRuleEditor()
    {
        var rb = new Rulebase<BackpackNotificationRule>
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            TitleLabel =
            {
                Text = TazLang.Get("backpacknotifications_rules", "Backpack notification rules"),
                HorizontalAlignment = HorizontalAlignment.Center
            }
        };

        rb.Columns.AddRange(GetBackpackRulebaseColumns());

        List<BackpackNotificationConfigEntry> rules = BackpackNotificationsConfig.Current.Rules;

        for (uint i = 0; i < rules.Count; i++)
        {
            BackpackNotificationRule rule = BackpackNotificationRule.FromEntry(i, rules[(int)i]);
            rb.Rules.Add(rule);
            rule.PropertyChanged += OnBackpackNotificationRuleChanged;
        }

        rb.RuleCrud += OnBackpackNotificationRuleCrud;

        return rb;
    }

    private static void OnBackpackNotificationRuleCrud(object sender, RuleCrudEventArgs<BackpackNotificationRule> e)
    {
        switch (e.Event)
        {
            case RuleCrudEventType.Create:
                UpsertBackpackNotificationRule(e.Rule, true);
                break;

            case RuleCrudEventType.Update:
                UpsertBackpackNotificationRule(e.Rule, false);
                break;

            case RuleCrudEventType.Delete:
                e.Rule.PropertyChanged -= OnBackpackNotificationRuleChanged;
                BackpackNotificationsConfig.Current.RemoveAt((int)e.Rule.Order);
                break;
        }
    }

    private static void OnBackpackNotificationRuleChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender is BackpackNotificationRule rule)
            UpsertBackpackNotificationRule(rule, false);
    }

    private static void UpsertBackpackNotificationRule(BackpackNotificationRule rule, bool isNew)
    {
        if (isNew)
            rule.PropertyChanged += OnBackpackNotificationRuleChanged;

        BackpackNotificationsConfig.Current.Upsert((int)rule.Order, rule.ToEntry(), isNew);
    }
}

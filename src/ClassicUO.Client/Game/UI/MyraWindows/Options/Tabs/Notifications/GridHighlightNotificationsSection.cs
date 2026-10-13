using System;
using ClassicUO.Common;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps.GridHighLight;
using ClassicUO.Game.UI.MyraWindows.Widgets;
using Myra.Graphics2D.UI;

namespace ClassicUO.Game.UI.MyraWindows.Options.Tabs.Notifications;

internal static partial class NotificationsTab
{
    private static OptionFragment GetGridHighlightSection() =>
        OptionsUi.VisualContainer(
            new VisualContainerProps { LabelText = TazLang.Get("gridhighlightnotifications_title") },
            Option.Custom(CreateGridHighlightNotifications,
                new SearchMetadata(TazLang.Get("gridhighlightnotifications_title"))));

    private static Widget CreateGridHighlightNotifications()
    {
        var panel = new VerticalStackPanel { Spacing = MyraStyle.STANDARD_SPACING };
        panel.Widgets.Add(new MyraLabel(TazLang.Get("gridhighlightnotifications_description"), MyraLabel.TextStyle.P)
        {
            Width = BackpackRuleCardWidth,
            Wrap = true
        });

        foreach (GridHighlightSetupEntry entry in GridHighlightsConfig.Current.Highlights)
        {
            if (entry.Notification == null && !entry.ShowNotification)
                continue;

            entry.Notification ??= new GridHighlightNotificationSettings();
            panel.Widgets.Add(CreateGridHighlightNotificationCard(entry));
        }

        if (panel.Widgets.Count == 1)
            panel.Widgets.Add(new MyraLabel(TazLang.Get("gridhighlightnotifications_empty"), MyraLabel.TextStyle.P));

        return panel;
    }

    private static Widget CreateGridHighlightNotificationCard(GridHighlightSetupEntry entry)
    {
        GridHighlightNotificationSettings settings = entry.Notification;
        var data = new GridHighlightData(entry);
        Widget enabled = MyraCheckButton.CreateWithCallback(entry.ShowNotification, value =>
        {
            data.ShowNotification = value;
            GridHighlightsConfig.Current.Save();
            GridHighlightData.RecheckMatchStatus();
            GridHighlightProperties.RefreshOpenWindows();
        }, TazLang.Get("gridhighlightnotifications_enabled"));

        Widget font = OptionTabCommons.StyledFontSelector(
            TazLang.Get("mog_chattab_fonttab_fontlabel"),
            new Accessor<string>(() => settings.OnScreenFont, value =>
            {
                settings.OnScreenFont = value;
                GridHighlightsConfig.Current.Save();
            }));
        font.Width = 230;
        Widget size = MyraInputBox.NumberWithLabel(
            TazLang.Get("mog_chattab_fonttab_size"), settings.OnScreenFontSize, 5, 50,
            value =>
            {
                settings.OnScreenFontSize = value;
                GridHighlightsConfig.Current.Save();
            }, width: 60);

        GridHighlightDestinationChoice[] choices =
        [
            new(BackpackNotificationDestination.Journal, TazLang.Get("gridhighlightnotifications_journal")),
            new(BackpackNotificationDestination.Overhead, TazLang.Get("gridhighlightnotifications_overhead")),
            new(BackpackNotificationDestination.OnScreen, TazLang.Get("gridhighlightnotifications_onscreen"))
        ];
        int selectedIndex = Array.FindIndex(choices, choice => choice.Value == settings.Destination);
        Widget destination = OptionTabCommons.CreateOptionsComboBox(
            null,
            choices[selectedIndex >= 0 ? selectedIndex : 0], choices, choice =>
            {
                settings.Destination = choice.Value;
                font.Enabled = size.Enabled = settings.Destination == BackpackNotificationDestination.OnScreen;
                GridHighlightsConfig.Current.Save();
            });
        font.Enabled = size.Enabled = settings.Destination == BackpackNotificationDestination.OnScreen;

        destination.Width = destination.MinWidth = 100;
        destination = Labeled(TazLang.Get("gridhighlightnotifications_destination"), destination);

        var hueLabel = new MyraLabel(settings.Hue.ToString(), MyraLabel.TextStyle.P);
        Widget hue = OptionTabCommons.StyledStackPanel(Orientation.Horizontal,
            new MyraLabel(TazLang.Get("gridhighlightnotifications_hue"), MyraLabel.TextStyle.P),
            OptionsFactory.CreateHuePicker(null, settings.Hue, value =>
            {
                settings.Hue = value;
                hueLabel.Text = value.ToString();
                GridHighlightsConfig.Current.Save();
            }), hueLabel);

        Widget announcement = CreateTextInput(
            TazLang.Get("backpacknotifications_announcement", "Announcement"), settings.Announcement, value =>
            {
                settings.Announcement = value;
                GridHighlightsConfig.Current.Save();
            }, TazLang.Get("gridhighlightnotifications_announcement_tooltip"));

        StackPanel card = OptionTabCommons.StyledStackPanel(Orientation.Vertical,
            CreateCardSection(TazLang.Get("gridhighlightnotifications_general"),
                (Labeled(TazLang.Get("gridhighlightnotifications_rule"),
                    new MyraLabel(GridHighlightNotificationManager.GetRuleName(entry), MyraLabel.TextStyle.P)
                    { Width = 150, Wrap = true }), 3f),
                (enabled, 1.25f), (destination, 2.25f),
                (new MyraButton(TazLang.Get("mog_kw_test"),
                    () => GridHighlightNotificationManager.TestNotification(World.Instance, entry)), 1f)),
            CreateCardSection(TazLang.Get("mog_kw_appearance"), (hue, 1.25f), (font, 2.25f), (size, 1.25f)),
            CreateMessageRow(TazLang.Get("gridhighlightnotifications_message"), announcement));
        card.Width = BackpackRuleCardWidth;
        card.HorizontalAlignment = HorizontalAlignment.Center;
        return new VisualContainer(new VisualContainerProps(), card);
    }

    private readonly record struct GridHighlightDestinationChoice(BackpackNotificationDestination Value, string Label)
    {
        public override string ToString() => Label;
    }
}

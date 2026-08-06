using System;
using ClassicUO.Common;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.MyraWindows.Widgets;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace ClassicUO.Game.UI.MyraWindows.Options.Tabs.Notifications;

internal static partial class NotificationsTab
{
    private static OptionFragment GetHealthSection()
    {
        return OptionsUi.VisualContainer(
            new VisualContainerProps
            {
                LabelText = TazLang.Get("healthnotifications_health", "Health Notifications")
            },
            Option.Custom(
                CreateHealthNotificationCard,
                new SearchMetadata(
                    TazLang.Get("healthnotifications_settings", "Health and debuff notification settings"),
                    Keywords:
                    [
                        TazLang.Get("healthnotifications_lowhealth", "Low health"),
                        TazLang.Get("healthnotifications_poison", "Poison"),
                        TazLang.Get("healthnotifications_mortalwound", "Mortal wound"),
                        TazLang.Get("healthnotifications_bloodoath", "Blood oath"),
                        TazLang.Get("healthnotifications_debuffs", "Debuffs")
                    ]
                )
            )
        );
    }

    private static Widget CreateHealthNotificationCard()
    {
        HealthNotificationsConfig config = HealthNotificationsConfig.Current;

        Widget lowHealthEnabled = MyraCheckButton.CreateWithCallback(
            config.LowHealthEnabled,
            value =>
            {
                config.LowHealthEnabled = value;
                config.Save();
            },
            TazLang.Get("healthnotifications_enabled", "Enabled")
        );
        Widget threshold = CreateHealthPercentageInput(config);
        Widget testLowHealth = CreateHealthTestButton(
            TazLang.Get("healthnotifications_testlowhealth", "Test low health"),
            () => HealthNotificationManager.Instance.TestLowHealthNotification()
        );

        Widget debuffsEnabled = MyraCheckButton.CreateWithCallback(
            config.DebuffsEnabled,
            value =>
            {
                config.DebuffsEnabled = value;
                config.Save();
            },
            TazLang.Get("healthnotifications_enabled", "Enabled")
        );
        Widget testDebuff = CreateHealthTestButton(
            TazLang.Get("healthnotifications_testdebuff", "Test debuff"),
            () => HealthNotificationManager.Instance.TestDebuffNotification()
        );

        Widget lowHealthHue = CreateHealthHueSelector(
            config,
            new Accessor<ushort>(() => config.LowHealthHue)
        );
        Widget debuffHue = CreateHealthHueSelector(
            config,
            new Accessor<ushort>(() => config.DebuffHue)
        );
        Widget font = CreateHealthFontSelector(config);
        Widget size = CreateHealthFontSizeInput(config);
        Widget destination = CreateHealthDestinationSelector(config, font, size);

        Widget lowHealthMessage = CreateTextInput(
            TazLang.Get("healthnotifications_lowhealth", "Low health"),
            config.LowHealthAnnouncement,
            value =>
            {
                config.LowHealthAnnouncement = value;
                config.Save();
            },
            TazLang.Get(
                "healthnotifications_lowhealthmessagetooltip",
                "Message shown at the health threshold. Supports {health}, {hits}, and {maxhits}."
            )
        );
        Widget debuffMessage = CreateTextInput(
            TazLang.Get("healthnotifications_debuff", "Debuff"),
            config.DebuffAnnouncement,
            value =>
            {
                config.DebuffAnnouncement = value;
                config.Save();
            },
            TazLang.Get("healthnotifications_debuffmessagetooltip", "Message shown when a selected debuff is applied. Supports {debuff}.")
        );

        StackPanel card = OptionTabCommons.StyledStackPanel(
            Orientation.Vertical,
            CreateCardSection(
                TazLang.Get("healthnotifications_lowhealth", "Low health"),
                (lowHealthEnabled, 1f),
                (threshold, 2f),
                (lowHealthHue, 1.25f),
                (testLowHealth, 1.4f)
            ),
            CreateDebuffSection(config, debuffsEnabled, debuffHue, testDebuff),
            CreateCardSection(
                TazLang.Get("healthnotifications_deliveryappearance", "Delivery & appearance"),
                (destination, 2f),
                (font, 2f),
                (size, 1.25f)
            ),
            CreateCardSection(
                TazLang.Get("healthnotifications_messages", "Messages"),
                (lowHealthMessage, 1f),
                (debuffMessage, 1f)
            )
        );
        card.Width = BackpackRuleCardWidth;
        card.HorizontalAlignment = HorizontalAlignment.Center;
        return card;
    }

    private static Widget CreateHealthPercentageInput(HealthNotificationsConfig config)
    {
        Widget input = OptionsFactory.PropBoundIntInput(
            null,
            new Accessor<int>(
                () => config.LowHealthPercentage,
                value =>
                {
                    config.LowHealthPercentage = value;
                    config.Save();
                }
            ),
            1,
            100,
            TazLang.Get("healthnotifications_percentagetooltip", "Notify when health reaches or falls below this percentage.")
        );
        input.Width = 72;

        return OptionTabCommons.StyledStackPanel(
            Orientation.Horizontal,
            new MyraLabel(TazLang.Get("healthnotifications_atbelow", "At or below"), MyraLabel.TextStyle.P),
            input,
            new MyraLabel("%", MyraLabel.TextStyle.P)
        );
    }

    private static Widget CreateDebuffSection(
        HealthNotificationsConfig config,
        Widget enabled,
        Widget hue,
        Widget test
    )
    {
        var controls = CreateFieldsGrid(
            (enabled, 1f),
            (new MyraLabel(TazLang.Get("healthnotifications_selectdebuffs", "Notify for these debuffs"), MyraLabel.TextStyle.P), 3f),
            (hue, 1.25f),
            (test, 1.4f)
        );

        const int columns = 4;
        var choices = new MyraGrid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ColumnSpacing = MyraStyle.STANDARD_SPACING * 2,
            RowSpacing = MyraStyle.STANDARD_SPACING,
            Margin = new Thickness(42, 2, 8, 10)
        };

        for (int column = 0; column < columns; column++)
            choices.AddColumn(new Proportion(ProportionType.Part, 1));

        int rows = (HealthDebuffCatalog.All.Length + columns - 1) / columns;

        for (int row = 0; row < rows; row++)
            choices.AddRow(new Proportion(ProportionType.Auto));

        for (int index = 0; index < HealthDebuffCatalog.All.Length; index++)
        {
            HealthDebuffDefinition definition = HealthDebuffCatalog.All[index];
            Widget checkbox = MyraCheckButton.CreateWithCallback(
                config.IsDebuffEnabled(definition.Type),
                value => config.SetDebuffEnabled(definition.Type, value),
                definition.Name
            );
            choices.AddWidget(checkbox, index / columns, index % columns);
        }

        StackPanel section = OptionTabCommons.StyledStackPanel(
            Orientation.Vertical,
            CreateSectionHeader(TazLang.Get("healthnotifications_debuffs", "Debuffs")),
            controls,
            choices
        );
        section.HorizontalAlignment = HorizontalAlignment.Stretch;
        return section;
    }

    private static Widget CreateHealthDestinationSelector(
        HealthNotificationsConfig config,
        params Widget[] onScreenOnly
    )
    {
        DestinationChoice[] choices =
        [
            new(BackpackNotificationDestination.Journal, TazLang.Get("backpacknotifications_journal", "Journal")),
            new(BackpackNotificationDestination.Overhead, TazLang.Get("backpacknotifications_overhead", "Overhead")),
            new(BackpackNotificationDestination.OnScreen, TazLang.Get("backpacknotifications_onscreen", "On-screen"))
        ];

        int selectedIndex = Array.FindIndex(choices, choice => choice.Value == config.Destination);
        DestinationChoice selected = choices[selectedIndex >= 0 ? selectedIndex : 0];
        Widget combo = OptionTabCommons.CreateOptionsComboBox(
            null,
            selected,
            choices,
            choice =>
            {
                config.Destination = choice.Value;
                config.Save();

                foreach (Widget widget in onScreenOnly)
                    widget.Enabled = choice.Value == BackpackNotificationDestination.OnScreen;
            },
            TazLang.Get("backpacknotifications_destinationtooltip", "Choose where this notification is displayed.")
        );
        combo.MinWidth = 110;
        combo.Width = 110;

        foreach (Widget widget in onScreenOnly)
            widget.Enabled = config.Destination == BackpackNotificationDestination.OnScreen;

        return Labeled(TazLang.Get("backpacknotifications_destination", "Notify via"), combo);
    }

    private static Widget CreateHealthHueSelector(
        HealthNotificationsConfig config,
        Accessor<ushort> hue
    )
    {
        var hueLabel = new MyraLabel(hue.Get().ToString(), MyraLabel.TextStyle.P);
        Widget picker = OptionsFactory.CreateHuePicker(
            null,
            hue.Get(),
            value =>
            {
                hue.Set(value);
                hueLabel.Text = value.ToString();
                config.Save();
            }
        );

        return Labeled(
            TazLang.Get("backpacknotifications_hue", "Hue"),
            OptionTabCommons.StyledStackPanel(Orientation.Horizontal, picker, hueLabel)
        );
    }

    private static Widget CreateHealthFontSelector(HealthNotificationsConfig config)
    {
        Widget selector = OptionTabCommons.StyledFontSelector(
            null,
            new Accessor<string>(
                () => config.OnScreenFont,
                value =>
                {
                    config.OnScreenFont = value;
                    config.Save();
                }
            )
        );
        selector.MinWidth = 125;
        selector.Width = 125;
        return Labeled(TazLang.Get("mog_chattab_fonttab_fontlabel", "Font"), selector);
    }

    private static Widget CreateHealthFontSizeInput(HealthNotificationsConfig config)
    {
        Widget input = OptionsFactory.PropBoundIntInput(
            null,
            new Accessor<int>(
                () => config.OnScreenFontSize,
                value =>
                {
                    config.OnScreenFontSize = value;
                    config.Save();
                }
            ),
            5,
            50,
            TazLang.Get("backpacknotifications_fontsizetooltip", "Font size used for On-screen notifications.")
        );
        input.Width = 72;
        return Labeled(TazLang.Get("mog_chattab_fonttab_size", "Size"), input);
    }

    private static Widget CreateHealthTestButton(string label, Action onClick) =>
        new MyraButton(label, onClick)
        {
            MinWidth = 110,
            Tooltip = TazLang.Get("healthnotifications_testtooltip", "Show a sample using the current delivery and appearance settings.")
        };
}

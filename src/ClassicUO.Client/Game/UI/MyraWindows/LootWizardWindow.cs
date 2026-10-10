#nullable enable

using System;
using System.Text.RegularExpressions;
using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.MyraWindows.Widgets;
using ClassicUO.Game.UI.MyraWindows.Widgets.ArtTexture;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace ClassicUO.Game.UI.MyraWindows;

/// <summary>
/// Shows a matching corpse item and its tooltip before a grid-highlight auto-loot action runs.
/// The result callback is also invoked as a cancellation when the window is closed by any other
/// route, so a pending item can never remain blocked indefinitely.
/// </summary>
public sealed class LootWizardWindow : MyraControl
{
    private readonly Action<bool> _onResult;
    private bool _resultSent;

    public LootWizardWindow(World world, Item item, Action<bool> onResult)
        : base(TazLang.Get("gridhighlight_lootwizard_title", "Loot wizard"))
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(onResult);

        _onResult = onResult;

        var root = new VerticalStackPanel
        {
            Spacing = 8,
            Padding = new Thickness(8)
        };

        root.Widgets.Add(new MyraLabel(
            TazLang.Get("gridhighlight_lootwizard_prompt", "Auto loot this matching item?"),
            MyraLabel.TextStyle.H3));

        var itemRow = new HorizontalStackPanel
        {
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center
        };

        var itemArt = new MyraArtTexture(item.DisplayedGraphic, item.Hue, 72)
        {
            Tooltip = BuildTooltipText(world, item)
        };
        itemRow.Widgets.Add(itemArt);

        string tooltipText = BuildTooltipText(world, item);
        itemRow.Widgets.Add(new ScrollViewer
        {
            MaxHeight = 320,
            Content = new MyraLabel(tooltipText, MyraLabel.TextStyle.P) { Width = 420 }
        });
        root.Widgets.Add(itemRow);

        var buttonRow = new HorizontalStackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttonRow.Widgets.Add(new MyraButton(
            TazLang.Get("gridhighlight_lootwizard_cancel", "Leave item"),
            () => Complete(false)));
        buttonRow.Widgets.Add(MyraStyle.ApplyButtonDangerStyle(new MyraButton(
            TazLang.Get("gridhighlight_lootwizard_confirm", "Loot item"),
            () => Complete(true))));
        root.Widgets.Add(buttonRow);

        SetRootContent(root);
        CenterInViewPort();
        UIManager.Add(this);
        BringOnTop();
    }

    public override void Dispose()
    {
        // Closing the title bar or right-clicking the window means the item stays in the corpse.
        if (!_resultSent)
        {
            _resultSent = true;
            _onResult(false);
        }

        base.Dispose();
    }

    private void Complete(bool confirmed)
    {
        if (_resultSent)
            return;

        _resultSent = true;
        _onResult(confirmed);
        base.Dispose();
    }

    private static string BuildTooltipText(World world, Item item)
    {
        string rawTooltip = $"{item.OPLName ?? item.GetNormalizedName(false)}\n{item.OPLData ?? string.Empty}";
        string resolvedTooltip = ToolTipOverrideData.ResolveTooltipText(world, item.Serial, rawTooltip);
        string plainTooltip = StripTooltipMarkup(resolvedTooltip);

        return string.IsNullOrWhiteSpace(plainTooltip)
            ? item.GetNormalizedName(false)
            : plainTooltip;
    }

    private static string StripTooltipMarkup(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        text = Regex.Replace(text, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", string.Empty);
        text = Regex.Replace(text, @"/c(?:d|\[[^\]]*\])", string.Empty, RegexOptions.IgnoreCase);
        return text.Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase).Trim();
    }
}

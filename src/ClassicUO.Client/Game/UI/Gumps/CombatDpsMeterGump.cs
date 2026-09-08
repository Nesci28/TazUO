using System;
using System.Collections.Generic;
using System.Globalization;
using ClassicUO.Game;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.MyraWindows;
using ClassicUO.Game.UI.MyraWindows.Widgets;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace ClassicUO.Game.UI.Gumps;

public sealed class CombatDpsMeterGump : MyraControl
{
    private const uint RefreshIntervalMs = 250;
    private const int MeterWidth = 330;
    private const int LabelWidth = 58;
    private const int BarWidth = 170;
    private const int BarHeight = 12;
    private const int ValueWidth = 54;

    private static readonly Color TrackColor = new(9, 12, 24, 185);
    private static readonly Color BorderColor = new(74, 82, 126, 180);
    private static readonly Color MineColor = new(33, 170, 227, 245);
    private static readonly Color OthersColor = new(178, 37, 61, 235);
    private static readonly Color UnknownColor = new(126, 126, 148, 235);
    private static readonly Color TotalColor = new(215, 149, 72, 240);

    private readonly MyraLabel _targetValue;
    private readonly MyraLabel _summaryValue;
    private readonly MyraTabControl _meterTabs;
    private readonly Dictionary<MeterTab, MeterTabRows> _rowsByTab = new();
    private MeterTab _activeTab;
    private CombatDamageSnapshot _snapshot;
    private uint _nextRefresh;

    public CombatDpsMeterGump() : base("DPS Meter")
    {
        CanBeSaved = true;
        AcceptKeyboardInput = false;

        var root = new VerticalStackPanel
        {
            Spacing = 5,
            Padding = new Thickness(8),
            Width = MeterWidth
        };

        root.Widgets.Add(BuildTargetRow(_targetValue = ValueLabel(240)));
        root.Widgets.Add(_summaryValue = new MyraLabel("No damage observed", MyraLabel.TextStyle.H6)
        {
            Width = MeterWidth - 16,
            Tooltip = "Mine/Others are whole hits attributed from matching combat events. Unknown is observed damage without a reliable source."
        });

        _meterTabs = new MyraTabControl();
        _meterTabs.SelectedIndexChanged += (_, _) =>
        {
            int? selectedIndex = _meterTabs.SelectedIndex;

            if (selectedIndex.HasValue)
            {
                SetActiveTab(selectedIndex.Value == 1 ? MeterTab.Damage : MeterTab.Dps);
            }
        };
        _meterTabs.AddTab("DPS", () => BuildMeterTab(MeterTab.Dps));
        _meterTabs.AddTab("DMG", () => BuildMeterTab(MeterTab.Damage));
        _meterTabs.SelectFirst();
        root.Widgets.Add(_meterTabs);

        SetRootContent(root);
        SetActiveTab(MeterTab.Dps, force: true);
        UpdateSnapshot(force: true);
        CenterInViewPort();
    }

    public static void Show()
    {
        foreach (IGui gump in UIManager.Gumps)
        {
            if (gump is CombatDpsMeterGump meter && !meter.IsDisposed)
            {
                meter.BringOnTop();
                return;
            }
        }

        UIManager.Add(new CombatDpsMeterGump());
    }

    public override void Update()
    {
        base.Update();

        if (IsDisposed)
        {
            return;
        }

        UpdateSnapshot();
    }

    private void UpdateSnapshot(bool force = false)
    {
        if (!force && Time.Ticks < _nextRefresh)
        {
            return;
        }

        _nextRefresh = Time.Ticks + RefreshIntervalMs;

        World world = World.Instance;
        _snapshot = world?.CombatDamageTracker.GetActiveSnapshot() ?? default;

        _targetValue.Text = FormatTarget(world, _snapshot.TargetSerial);
        _summaryValue.Text = _snapshot.HasData
            ? $"{_snapshot.HitCount} hits | {_snapshot.ElapsedSeconds:0.0} s | {_snapshot.AttributionCoverage:P0} attributed"
            : "No damage observed";

        UpdateBars();
    }

    private void SetActiveTab(MeterTab tab, bool force = false)
    {
        if (!force && _activeTab == tab)
        {
            return;
        }

        _activeTab = tab;
        UpdateBars();
    }

    private void UpdateBars()
    {
        foreach ((MeterTab tab, MeterTabRows rows) in _rowsByTab)
        {
            UpdateRows(tab, rows);
        }
    }

    private Widget BuildMeterTab(MeterTab tab)
    {
        var rows = new MeterTabRows(
            new MeterBarRow("Mine", MineColor),
            new MeterBarRow("Others", OthersColor),
            new MeterBarRow("Unknown", UnknownColor),
            new MeterBarRow("Total", TotalColor)
        );
        _rowsByTab[tab] = rows;

        var chartPanel = new VerticalStackPanel
        {
            Spacing = 4,
            Padding = new Thickness(2)
        };

        chartPanel.Widgets.Add(rows.Mine.Root);
        chartPanel.Widgets.Add(rows.Others.Root);
        chartPanel.Widgets.Add(rows.Unknown.Root);
        chartPanel.Widgets.Add(rows.Total.Root);
        UpdateRows(tab, rows);
        return chartPanel;
    }

    private void UpdateRows(MeterTab tab, MeterTabRows rows)
    {
        double mine = tab == MeterTab.Dps ? _snapshot.MineDps : _snapshot.MineDamage;
        double others = tab == MeterTab.Dps ? _snapshot.OthersDps : _snapshot.OthersDamage;
        double unknown = tab == MeterTab.Dps ? _snapshot.UnknownDps : _snapshot.UnknownDamage;
        double total = tab == MeterTab.Dps ? _snapshot.TotalDps : _snapshot.TotalDamage;
        double max = Math.Max(total, Math.Max(unknown, Math.Max(mine, others)));

        rows.Mine.SetValue(mine, max, FormatValue(mine, tab));
        rows.Others.SetValue(others, max, FormatValue(others, tab));
        rows.Unknown.SetValue(unknown, max, FormatValue(unknown, tab));
        rows.Total.SetValue(total, max, FormatValue(total, tab));
    }

    private static HorizontalStackPanel BuildTargetRow(MyraLabel value)
    {
        var row = new HorizontalStackPanel
        {
            Spacing = MyraStyle.STANDARD_SPACING,
            VerticalAlignment = VerticalAlignment.Center
        };

        row.Widgets.Add(new MyraLabel("Target", MyraLabel.TextStyle.H6) { Width = 52 });
        row.Widgets.Add(value);
        return row;
    }

    private static MyraLabel ValueLabel(int width)
    {
        return new MyraLabel("0.0", MyraLabel.TextStyle.H6, MyraLabel.AlignMode.Right)
        {
            Width = width
        };
    }

    private static string FormatTarget(World world, uint serial)
    {
        if (!SerialHelper.IsValid(serial))
        {
            return "None";
        }

        Entity entity = world?.Get(serial);
        return string.IsNullOrWhiteSpace(entity?.Name) ? $"0x{serial:X8}" : $"{entity.Name} (0x{serial:X8})";
    }

    private static string FormatValue(double value, MeterTab tab) =>
        value.ToString(tab == MeterTab.Dps ? "0.0" : "0", CultureInfo.InvariantCulture);

    private enum MeterTab
    {
        Dps,
        Damage
    }

    private sealed record MeterTabRows(
        MeterBarRow Mine,
        MeterBarRow Others,
        MeterBarRow Unknown,
        MeterBarRow Total
    );

    private sealed class MeterBarRow
    {
        private readonly Panel _fill;
        private readonly MyraLabel _value;

        public MeterBarRow(string label, Color color)
        {
            Root = new HorizontalStackPanel
            {
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };

            Root.Widgets.Add(new MyraLabel(label, MyraLabel.TextStyle.H6) { Width = LabelWidth });

            var track = new Panel
            {
                Width = BarWidth,
                Height = BarHeight,
                Background = new SolidBrush(TrackColor),
                Border = new SolidBrush(BorderColor),
                BorderThickness = new Thickness(1)
            };

            _fill = new Panel
            {
                Width = 0,
                Height = BarHeight,
                Background = new SolidBrush(color)
            };

            track.Widgets.Add(_fill);
            Root.Widgets.Add(track);

            Root.Widgets.Add(_value = new MyraLabel("0.0", MyraLabel.TextStyle.H6, MyraLabel.AlignMode.Right)
            {
                Width = ValueWidth
            });
        }

        public HorizontalStackPanel Root { get; }

        public void SetValue(double value, double max, string formatted)
        {
            double ratio = max <= 0 ? 0 : Math.Clamp(value / max, 0, 1);
            _fill.Width = ratio <= 0 ? 0 : Math.Max(1, (int)Math.Round(BarWidth * ratio));
            _value.Text = formatted;
        }
    }
}

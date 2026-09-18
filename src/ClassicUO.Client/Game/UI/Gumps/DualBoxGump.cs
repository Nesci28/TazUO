// SPDX-License-Identifier: BSD-2-Clause

using ClassicUO.Configuration;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;

namespace ClassicUO.Game.UI.Gumps;

public sealed class DualBoxGump : Gump
{
    private const int GumpWidth = 320;
    private readonly Label _modeLabel;
    private readonly Label _clientsLabel;
    private readonly Label _statusLabel;
    private readonly NiceButton _syncButton;
    private uint _nextRefresh;

    public DualBoxGump(World world, int x, int y) : base(world, 0, 0)
    {
        X = x;
        Y = y;
        Width = GumpWidth;
        Height = 150;
        CanMove = true;
        CanCloseWithEsc = true;
        CanCloseWithRightClick = true;
        AcceptMouseInput = true;

        Add(
            new AlphaBlendControl(0.8f)
            {
                Width = Width,
                Height = Height,
                AcceptMouseInput = true,
                CanMove = true
            }
        );

        Add(
            new Label(TazLang.Get("dualbox_title", "Dual Box"), true, 0x0481, font: 1)
            {
                X = 10,
                Y = 8
            }
        );

        Add(_modeLabel = new Label(string.Empty, true, 0xFFFF) { X = 10, Y = 30 });
        Add(_clientsLabel = new Label(string.Empty, true, 0xFFFF) { X = 105, Y = 30 });

        AddButton(10, 52, 90, TazLang.Get("dualbox_master", "Master"), 1);
        AddButton(110, 52, 90, TazLang.Get("dualbox_client", "Client"), 2);
        _syncButton = AddButton(210, 52, 100, TazLang.Get("dualbox_sync", "Sync (10 tiles)"), 3);
        AddButton(10, 80, 300, TazLang.Get("dualbox_stop", "Stop dual-box connection"), 4);

        Add(
            _statusLabel = new Label(string.Empty, true, 0xFFFF, GumpWidth - 20)
            {
                X = 10,
                Y = 112
            }
        );

        LayerOrder = UILayer.Over;
        WantUpdateSize = false;
        RefreshStatus();
    }

    public static void Show(World world)
    {
        DualBoxGump gump = UIManager.GetGump<DualBoxGump>();

        if (gump == null)
        {
            UIManager.Add(new DualBoxGump(world, 100, 100));
        }
        else
        {
            gump.IsVisible = true;
            gump.SetInScreen();
        }
    }

    public override void Update()
    {
        base.Update();

        if (Time.Ticks < _nextRefresh)
            return;

        _nextRefresh = Time.Ticks + 200;
        RefreshStatus();
    }

    public override void OnButtonClick(int buttonID)
    {
        DualBoxManager manager = DualBoxManager.Instance;

        switch (buttonID)
        {
            case 1:
                manager.StartMaster();
                break;
            case 2:
                manager.StartClient();
                break;
            case 3:
                manager.SyncClients();
                break;
            case 4:
                manager.Stop();
                break;
        }

        RefreshStatus();
    }

    private NiceButton AddButton(int x, int y, int width, string text, int id)
    {
        var button = new NiceButton(x, y, width, 22, ButtonAction.Activate, text)
        {
            ButtonParameter = id,
            IsSelectable = false,
            DisplayBorder = true
        };
        Add(button);
        return button;
    }

    private void RefreshStatus()
    {
        DualBoxManager manager = DualBoxManager.Instance;
        _modeLabel.Text = $"Mode: {manager.ModeText}";
        _clientsLabel.Text = manager.IsMaster
            ? $"Peers {manager.ConnectedClientCount} | Ready {manager.ReadyClientCount}/{manager.SelectedClientCount}"
            : $"Master: {(manager.ConnectedClientCount == 0 ? "disconnected" : "connected")}";
        _statusLabel.Text = manager.StatusText;
        _syncButton.IsEnabled = manager.IsMaster;
    }
}

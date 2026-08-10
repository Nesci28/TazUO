// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.UI;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Input;
using ClassicUO.Utility;
using ClassicUO.Utility.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace ClassicUO.Game.Managers.VendorSearch;

internal sealed class VendorSearchWebManager
{
    public const int DefaultPort = 8089;

    private const string LauncherTag = "tazuo-vendor-search-web-launcher";
    private static VendorSearchWebManager _instance;

    private readonly Dictionary<uint, byte[]> _artCache = new();
    private readonly object _sync = new();
    private readonly VendorSearchWebServer _server;
    private long _nextRevision;
    private long _nextVersion;
    private long _submittingVersion;
    private VendorSearchSnapshot _snapshot;

    public static VendorSearchWebManager Instance => _instance ??= new VendorSearchWebManager();

    private VendorSearchWebManager()
    {
        _server = new VendorSearchWebServer(this);
    }

    public bool IsRunning => _server.IsRunning;
    public int Port => _server.Port;

    public void ObserveGump(Gump gump, string layout, string[] lines)
    {
        if (gump == null || gump.IsDisposed || !gump.IsFromServer)
            return;

        var visibleTexts = new List<string>(lines ?? []);

        foreach (IGui child in gump.Children)
        {
            switch (child)
            {
                case HtmlControl html:
                    visibleTexts.Add(html.Text);
                    break;
                case Label label:
                    visibleTexts.Add(label.Text);
                    break;
            }
        }

        VendorSearchGumpKind kind = VendorSearchPacketAnalyzer.Classify(layout, visibleTexts);

        if (kind == VendorSearchGumpKind.None)
            return;

        VendorSearchSnapshot snapshot = Capture(gump, layout, kind);

        lock (_sync)
            _snapshot = snapshot;

        long observedVersion = snapshot.Version;
        gump.Disposed += (_, _) => OnObservedGumpDisposed(
            gump.LocalSerial,
            gump.ServerSerial,
            observedVersion
        );

        if (kind is VendorSearchGumpKind.Query or VendorSearchGumpKind.Results)
            AddWebLauncher(gump);
    }

    public void NotifyItemProperties(uint serial)
    {
        lock (_sync)
        {
            if (_snapshot?.Items.Any(item => item.Serial == serial) != true)
                return;

            _snapshot.Revision = ++_nextRevision;
        }
    }

    public VendorSearchStateDto GetState() => MainThreadQueue.BubblingInvokeOnMainThread(BuildStateOnMainThread);

    public VendorSearchResponseResult Submit(VendorSearchResponseRequest request)
    {
        if (request == null)
            return Rejected(400, "A response body is required.");

        return MainThreadQueue.BubblingInvokeOnMainThread(() => SubmitOnMainThread(request));
    }

    public bool IsArtAllowed(ushort graphic, ushort hue)
    {
        lock (_sync)
            return _snapshot?.Items.Any(item => item.Graphic == graphic && item.Hue == hue) == true;
    }

    public byte[] GetArtPng(ushort graphic, ushort hue)
    {
        uint key = graphic | ((uint)hue << 16);

        lock (_sync)
        {
            if (_artCache.TryGetValue(key, out byte[] cached))
                return cached;
        }

        byte[] png = MainThreadQueue.BubblingInvokeOnMainThread(
            () => RenderArtPngOnMainThread(graphic, hue)
        );

        if (png != null)
        {
            lock (_sync)
                _artCache[key] = png;
        }

        return png;
    }

    public void OpenBrowser()
    {
        if (!_server.Start(DefaultPort) && !_server.IsRunning)
        {
            GameActions.Print(
                World.Instance,
                $"Unable to start Vendor Search web server on localhost:{DefaultPort}.",
                0x21
            );
            return;
        }

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = $"http://localhost:{_server.Port}/",
                    UseShellExecute = true
                }
            );
        }
        catch (Exception ex)
        {
            Log.Error($"Unable to open Vendor Search web page: {ex.Message}");
            GameActions.Print(World.Instance, $"Unable to open browser: {ex.Message}", 0x21);
        }
    }

    public void Stop()
    {
        _server.Stop();

        lock (_sync)
        {
            _snapshot = null;
            _artCache.Clear();
        }
    }

    private VendorSearchSnapshot Capture(
        Gump gump,
        string layout,
        VendorSearchGumpKind kind
    )
    {
        double scale = ProfileManager.CurrentProfile?.ServerGumpScale ?? 1d;
        var snapshot = new VendorSearchSnapshot
        {
            Version = ++_nextVersion,
            Revision = ++_nextRevision,
            Kind = kind,
            LocalSerial = gump.LocalSerial,
            GumpID = gump.ServerSerial,
            Width = Math.Max(gump.Width, 320),
            Height = Math.Max(gump.Height, 180),
            ActivePage = gump.ActivePage == 0 ? 1 : gump.ActivePage,
            Scale = scale,
            Items = VendorSearchPacketAnalyzer.AnalyzeItems(layout).ToList()
        };

        foreach (IGui child in gump.Children)
        {
            if (child is not Control control || !control.IsFromServer || control.IsDisposed)
                continue;

            switch (control)
            {
                case HtmlControl html:
                    AddText(snapshot, control, html.Text);
                    break;
                case Label label:
                    AddText(snapshot, control, label.Text);
                    break;
                case StbTextBox entry:
                    snapshot.Entries.Add(
                        new VendorSearchEntryControl
                        {
                            ID = (int)entry.LocalSerial,
                            Text = entry.Text ?? string.Empty,
                            X = entry.X,
                            Y = entry.Y,
                            Width = Math.Max(entry.Width, 20),
                            Height = Math.Max(entry.Height, 18),
                            Page = entry.Page
                        }
                    );
                    break;
                case Checkbox checkbox:
                    snapshot.Switches.Add(
                        new VendorSearchSwitchControl
                        {
                            ID = checkbox.LocalSerial,
                            IsChecked = checkbox.IsChecked,
                            Text = VendorSearchPacketAnalyzer.NormalizeText(checkbox.Text),
                            X = checkbox.X,
                            Y = checkbox.Y,
                            Width = Math.Max(checkbox.Width, 20),
                            Height = Math.Max(checkbox.Height, 18),
                            Page = checkbox.Page
                        }
                    );
                    break;
                case ButtonTileArt:
                    // Item art and its itemproperty serial are reconstructed from the raw layout.
                    break;
                case Button button:
                    snapshot.Buttons.Add(
                        new VendorSearchButtonControl
                        {
                            ButtonID = button.ButtonID,
                            IsPageButton = button.ButtonAction == ButtonAction.SwitchPage,
                            ToPage = button.ToPage,
                            Tooltip = VendorSearchPacketAnalyzer.NormalizeText(
                                button.Tooltip as string
                            ),
                            X = button.X,
                            Y = button.Y,
                            Width = Math.Max(button.Width, 24),
                            Height = Math.Max(button.Height, 18),
                            Page = button.Page
                        }
                    );
                    break;
            }
        }

        return snapshot;
    }

    private static void AddText(
        VendorSearchSnapshot snapshot,
        Control control,
        string rawText
    )
    {
        string text = VendorSearchPacketAnalyzer.NormalizeText(rawText);

        if (string.IsNullOrWhiteSpace(text))
            return;

        snapshot.Texts.Add(
            new VendorSearchTextControl
            {
                Text = text,
                X = control.X,
                Y = control.Y,
                Width = Math.Max(control.Width, 20),
                Height = Math.Max(control.Height, 18),
                Page = control.Page
            }
        );
    }

    private void AddWebLauncher(Gump gump)
    {
        if (
            gump.Children.Any(
                child => child is Control control && Equals(control.Tag, LauncherTag)
            )
        )
            return;

        var launcher = new WebLauncherButton(
            Math.Max(4, gump.Width - 52),
            OpenBrowser
        )
        {
            Tag = LauncherTag
        };
        gump.Add(launcher);
    }

    private VendorSearchStateDto BuildStateOnMainThread()
    {
        VendorSearchSnapshot snapshot;

        lock (_sync)
            snapshot = _snapshot;

        if (snapshot == null)
        {
            return new VendorSearchStateDto
            {
                Available = false,
                Mode = "unavailable",
                Message = "Open Vendor Search from your character's context menu in TazUO."
            };
        }

        bool available = snapshot.Kind != VendorSearchGumpKind.Closed;
        var state = new VendorSearchStateDto
        {
            Available = available,
            Version = snapshot.Version,
            Revision = snapshot.Revision,
            Mode = snapshot.Kind.ToString().ToLowerInvariant(),
            Message = snapshot.Message,
            Width = snapshot.Width,
            Height = snapshot.Height,
            ActivePage = snapshot.ActivePage,
            Texts = snapshot.Texts,
            Entries = snapshot.Entries,
            Buttons = snapshot.Buttons,
            Switches = snapshot.Switches
        };

        World world = World.Instance;

        foreach (VendorSearchPacketItem item in snapshot.Items)
        {
            string name = string.Empty;
            string properties = string.Empty;

            if (item.Serial != 0 && world?.OPL != null)
                world.OPL.TryGetNameAndData(item.Serial, out name, out properties);

            state.Items.Add(
                new VendorSearchItemDto
                {
                    X = (int)((item.X + item.TileOffsetX) * snapshot.Scale),
                    Y = (int)((item.Y + item.TileOffsetY) * snapshot.Scale),
                    Page = item.Page,
                    Graphic = item.Graphic,
                    Hue = item.Hue,
                    Serial = item.Serial,
                    Scale = snapshot.Scale,
                    Name = VendorSearchPacketAnalyzer.NormalizeText(name),
                    Properties = VendorSearchPacketAnalyzer.NormalizeText(properties),
                    ArtUrl = $"/api/vendor-search/art?graphic={item.Graphic.ToString(CultureInfo.InvariantCulture)}&hue={item.Hue.ToString(CultureInfo.InvariantCulture)}"
                }
            );
        }

        return state;
    }

    private VendorSearchResponseResult SubmitOnMainThread(VendorSearchResponseRequest request)
    {
        VendorSearchSnapshot snapshot;

        lock (_sync)
            snapshot = _snapshot;

        if (
            !VendorSearchResponseValidator.TryValidate(
                snapshot,
                request,
                out int statusCode,
                out string validationMessage
            )
        )
            return Rejected(statusCode, validationMessage);

        var switchIDs = snapshot.Switches.Select(control => control.ID).ToHashSet();

        Gump gump = UIManager.GetGumpServer(snapshot.GumpID);

        if (
            gump == null
            || gump.IsDisposed
            || gump.LocalSerial != snapshot.LocalSerial
            || gump.ServerSerial != snapshot.GumpID
        )
            return Rejected(410, "The Vendor Search gump was closed in TazUO.");

        foreach (IGui child in gump.Children)
        {
            if (
                child is StbTextBox entry
                && request.Entries != null
                && request.Entries.TryGetValue((int)entry.LocalSerial, out string text)
            )
                entry.Text = text ?? string.Empty;

            if (child is Checkbox checkbox && switchIDs.Contains(checkbox.LocalSerial))
                checkbox.IsChecked = (request.Switches ?? []).Contains(checkbox.LocalSerial);
        }

        _submittingVersion = snapshot.Version;

        try
        {
            gump.OnButtonClick(request.ButtonID);
        }
        finally
        {
            _submittingVersion = 0;
        }

        VendorSearchSnapshot pending = CreateStatusSnapshot(
            VendorSearchGumpKind.Pending,
            "Waiting for the shard to update Vendor Search..."
        );

        lock (_sync)
            _snapshot = pending;

        return new VendorSearchResponseResult
        {
            Accepted = true,
            StatusCode = 200,
            Message = "Response sent.",
            Revision = pending.Revision
        };
    }

    private void OnObservedGumpDisposed(uint localSerial, uint gumpID, long observedVersion)
    {
        lock (_sync)
        {
            if (
                _submittingVersion == observedVersion
                || _snapshot == null
                || _snapshot.Version != observedVersion
                || _snapshot.LocalSerial != localSerial
                || _snapshot.GumpID != gumpID
            )
                return;

            _snapshot = CreateStatusSnapshot(
                VendorSearchGumpKind.Closed,
                "Vendor Search was closed in TazUO. Open it again to continue."
            );
        }
    }

    private VendorSearchSnapshot CreateStatusSnapshot(
        VendorSearchGumpKind kind,
        string message
    ) =>
        new()
        {
            Version = ++_nextVersion,
            Revision = ++_nextRevision,
            Kind = kind,
            Message = message
        };

    private VendorSearchResponseResult Rejected(int statusCode, string message) =>
        new()
        {
            Accepted = false,
            StatusCode = statusCode,
            Message = message,
            Revision = _snapshot?.Revision ?? 0
        };

    private static byte[] RenderArtPngOnMainThread(ushort graphic, ushort hue)
    {
        if (Client.Game?.UO?.Arts == null)
            return null;

        ArtInfo artInfo = Client.Game.UO.Arts.GetArtPixels(graphic);

        if (artInfo.Pixels.IsEmpty || artInfo.Width <= 0 || artInfo.Height <= 0)
            return null;

        ushort effectiveHue = (ushort)(hue & 0x7FFF);
        bool partialHue = (hue & 0x8000) != 0;

        if (graphic < Client.Game.UO.FileManager.TileData.StaticData.Length)
            partialHue |= Client.Game.UO.FileManager.TileData.StaticData[graphic].IsPartialHue;

        using var image = new Image<Rgba32>(artInfo.Width, artInfo.Height);

        for (int y = 0; y < artInfo.Height; y++)
        {
            for (int x = 0; x < artInfo.Width; x++)
            {
                uint pixel = artInfo.Pixels[(y * artInfo.Width) + x];

                if (pixel == 0)
                {
                    image[x, y] = new Rgba32(0, 0, 0, 0);
                    continue;
                }

                if (effectiveHue != 0 && (effectiveHue & 0x4000) == 0)
                {
                    byte red = (byte)pixel;
                    byte green = (byte)(pixel >> 8);
                    byte blue = (byte)(pixel >> 16);

                    if (!partialHue || (red == green && red == blue))
                    {
                        pixel =
                            Client.Game.UO.FileManager.Hues.ApplyHueRgba8888(
                                HuesHelper.Color32To16(pixel),
                                effectiveHue
                            ) | 0xFF000000;
                    }
                }

                image[x, y] = new Rgba32(
                    (byte)pixel,
                    (byte)(pixel >> 8),
                    (byte)(pixel >> 16),
                    (byte)(pixel >> 24)
                );
            }
        }

        using var stream = new MemoryStream();
        image.Save(
            stream,
            new PngEncoder
            {
                CompressionLevel = PngCompressionLevel.DefaultCompression,
                SkipMetadata = true
            }
        );
        return stream.ToArray();
    }

    private sealed class WebLauncherButton : NiceButton
    {
        private readonly Action _openBrowser;

        public WebLauncherButton(int x, Action openBrowser)
            : base(x, 5, 46, 20, ButtonAction.SwitchPage, "Web", hue: 0xFFFF)
        {
            _openBrowser = openBrowser;
            AlwaysShowBackground = true;
            BackgroundColor = new Microsoft.Xna.Framework.Color(32, 36, 44, 225);
            DisplayBorder = true;
            IsSelectable = false;
            SetTooltip("Open Vendor Search in browser");
        }

        public override void OnMouseUp(int x, int y, MouseButtonType button)
        {
            if (button == MouseButtonType.Left)
                _openBrowser();
        }
    }
}

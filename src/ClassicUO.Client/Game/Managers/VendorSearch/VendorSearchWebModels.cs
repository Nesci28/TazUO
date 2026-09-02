// SPDX-License-Identifier: BSD-2-Clause

using System.Collections.Generic;

namespace ClassicUO.Game.Managers.VendorSearch;

internal sealed class VendorSearchSnapshot
{
    public long Version { get; init; }
    public long Revision { get; set; }
    public VendorSearchGumpKind Kind { get; init; }
    public string Message { get; init; }
    public uint LocalSerial { get; init; }
    public uint GumpID { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int ActivePage { get; init; }
    public double Scale { get; init; } = 1d;
    public List<VendorSearchTextControl> Texts { get; init; } = new();
    public List<VendorSearchEntryControl> Entries { get; init; } = new();
    public List<VendorSearchButtonControl> Buttons { get; init; } = new();
    public List<VendorSearchSwitchControl> Switches { get; init; } = new();
    public List<VendorSearchPacketItem> Items { get; init; } = new();
}

internal abstract class VendorSearchWebControl
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int Page { get; init; }
}

internal sealed class VendorSearchTextControl : VendorSearchWebControl
{
    public string Text { get; init; }
}

internal sealed class VendorSearchEntryControl : VendorSearchWebControl
{
    public int ID { get; init; }
    public string Text { get; init; }
}

internal sealed class VendorSearchButtonControl : VendorSearchWebControl
{
    public int ButtonID { get; init; }
    public bool IsPageButton { get; init; }
    public int ToPage { get; init; }
    public string Tooltip { get; init; }
}

internal sealed class VendorSearchSwitchControl : VendorSearchWebControl
{
    public uint ID { get; init; }
    public bool IsChecked { get; init; }
    public string Text { get; init; }
}

internal sealed class VendorSearchStateDto
{
    public bool Available { get; init; }
    public long Version { get; init; }
    public long Revision { get; init; }
    public string Mode { get; init; }
    public string Message { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int ActivePage { get; init; }
    public List<VendorSearchTextControl> Texts { get; init; } = new();
    public List<VendorSearchEntryControl> Entries { get; init; } = new();
    public List<VendorSearchButtonControl> Buttons { get; init; } = new();
    public List<VendorSearchSwitchControl> Switches { get; init; } = new();
    public List<VendorSearchItemDto> Items { get; init; } = new();
}

internal sealed class VendorSearchItemDto
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Page { get; init; }
    public ushort Graphic { get; init; }
    public ushort Hue { get; init; }
    public uint Serial { get; init; }
    public double Scale { get; init; }
    public string Name { get; init; }
    public string Properties { get; init; }
    public string ArtUrl { get; init; }
}

internal sealed class VendorSearchResponseRequest
{
    public long Version { get; set; }
    public int ButtonID { get; set; }
    public Dictionary<int, string> Entries { get; set; } = new();
    public uint[] Switches { get; set; } = [];
}

internal sealed class VendorSearchResponseResult
{
    public bool Accepted { get; init; }
    public int StatusCode { get; init; }
    public string Message { get; init; }
    public long Revision { get; init; }
}

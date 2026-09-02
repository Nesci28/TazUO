// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ClassicUO.Utility;

namespace ClassicUO.Game.Managers.VendorSearch;

internal enum VendorSearchGumpKind
{
    None,
    Query,
    Waiting,
    Results,
    Pending,
    Closed
}

internal sealed class VendorSearchPacketItem
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Page { get; init; }
    public ushort Graphic { get; init; }
    public ushort Hue { get; init; }
    public int TileOffsetX { get; init; }
    public int TileOffsetY { get; init; }
    public uint Serial { get; set; }
    internal int CommandIndex { get; init; }
}

/// <summary>
/// Identifies OSI-compatible Vendor Search gumps and associates their itemproperty commands with
/// the item-art commands they describe. Vendor Search has no dedicated packet ID: it is carried by
/// the normal 0xB0/0xDD server-gump packets.
/// </summary>
internal static class VendorSearchPacketAnalyzer
{
    internal const int QueryTitleCliloc = 1154508;
    internal const int ResultsTitleCliloc = 1154509;
    internal const int WaitingCliloc = 1154678;

    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);

    public static VendorSearchGumpKind Classify(
        string layout,
        IEnumerable<string> visibleTexts = null
    )
    {
        if (ContainsNumber(layout, ResultsTitleCliloc))
            return VendorSearchGumpKind.Results;

        if (ContainsNumber(layout, QueryTitleCliloc))
            return VendorSearchGumpKind.Query;

        if (ContainsNumber(layout, WaitingCliloc))
            return VendorSearchGumpKind.Waiting;

        if (visibleTexts == null)
            return VendorSearchGumpKind.None;

        foreach (string rawText in visibleTexts)
        {
            string text = NormalizeText(rawText);

            if (text.Contains("Vendor Search Results", StringComparison.OrdinalIgnoreCase))
                return VendorSearchGumpKind.Results;

            if (text.Contains("Vendor Search Query", StringComparison.OrdinalIgnoreCase))
                return VendorSearchGumpKind.Query;

            if (
                text.Contains("wait for your search to complete", StringComparison.OrdinalIgnoreCase)
            )
                return VendorSearchGumpKind.Waiting;
        }

        return VendorSearchGumpKind.None;
    }

    public static IReadOnlyList<VendorSearchPacketItem> AnalyzeItems(string layout)
    {
        List<string[]> commands = ParseCommands(layout);
        var items = new List<VendorSearchPacketItem>();
        var properties = new List<(int CommandIndex, uint Serial)>();
        int page = 0;

        for (int i = 0; i < commands.Count; i++)
        {
            string[] command = commands[i];

            if (command.Length == 0)
                continue;

            if (
                command[0].Equals("page", StringComparison.OrdinalIgnoreCase)
                && command.Length > 1
                && int.TryParse(command[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedPage)
            )
            {
                page = parsedPage;
                continue;
            }

            if (
                command[0].Equals("itemproperty", StringComparison.OrdinalIgnoreCase)
                && command.Length > 1
                && TryParseSerial(command[1], out uint serial)
                && serial != 0
            )
            {
                properties.Add((i, serial));
                continue;
            }

            if (TryParseItem(command, page, i, out VendorSearchPacketItem item))
                items.Add(item);
        }

        // OSI has emitted both "itemproperty then art" and "art then itemproperty" layouts.
        // When the counts match, ordinal association is unambiguous and handles either order.
        if (items.Count == properties.Count)
        {
            for (int i = 0; i < items.Count; i++)
                items[i].Serial = properties[i].Serial;

            return items;
        }

        // For custom shards with decorative tile art, match each result to the closest unused
        // itemproperty command. This is deliberately one-to-one so adjacent rows cannot donate a
        // serial to one another.
        bool[] usedProperties = new bool[properties.Count];

        foreach (VendorSearchPacketItem item in items)
        {
            int bestIndex = -1;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < properties.Count; i++)
            {
                if (usedProperties[i])
                    continue;

                int distance = Math.Abs(properties[i].CommandIndex - item.CommandIndex);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                item.Serial = properties[bestIndex].Serial;
                usedProperties[bestIndex] = true;
            }
        }

        return items;
    }

    public static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string normalized = Regex.Replace(
            text,
            "<br\\s*/?>",
            "\n",
            RegexOptions.IgnoreCase
        );
        normalized = HtmlTagRegex.Replace(normalized, string.Empty);
        return WebUtility.HtmlDecode(normalized).Trim();
    }

    private static bool TryParseItem(
        string[] command,
        int page,
        int commandIndex,
        out VendorSearchPacketItem item
    )
    {
        item = null;

        try
        {
            if (
                command.Length > 11
                && command[0].Equals("buttontileart", StringComparison.OrdinalIgnoreCase)
            )
            {
                item = new VendorSearchPacketItem
                {
                    X = ParseInt(command[1]),
                    Y = ParseInt(command[2]),
                    Page = page,
                    Graphic = UInt16Converter.Parse(command[8]),
                    Hue = UInt16Converter.Parse(command[9]),
                    TileOffsetX = ParseInt(command[10]),
                    TileOffsetY = ParseInt(command[11]),
                    CommandIndex = commandIndex
                };
                return item.Graphic != 0;
            }

            if (
                command.Length > 3
                && (
                    command[0].Equals("tilepic", StringComparison.OrdinalIgnoreCase)
                    || command[0].Equals("tilepichue", StringComparison.OrdinalIgnoreCase)
                    || command[0].Equals(
                        "tilepicasgumppic",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            )
            {
                item = new VendorSearchPacketItem
                {
                    X = ParseInt(command[1]),
                    Y = ParseInt(command[2]),
                    Page = page,
                    Graphic = UInt16Converter.Parse(command[3]),
                    Hue = command.Length > 4 ? ParseUShort(command[4]) : (ushort)0,
                    CommandIndex = commandIndex
                };
                return item.Graphic != 0;
            }
        }
        catch (FormatException) { }
        catch (OverflowException) { }
        catch (IndexOutOfRangeException) { }

        item = null;
        return false;
    }

    private static List<string[]> ParseCommands(string layout)
    {
        var result = new List<string[]>();

        if (string.IsNullOrWhiteSpace(layout))
            return result;

        var layoutParser = new TextFileParser(
            layout,
            new[] { ' ' },
            Array.Empty<char>(),
            new[] { '{', '}' }
        );
        List<string> commandTexts = layoutParser.GetTokens(layout);

        foreach (string commandText in commandTexts)
        {
            var commandParser = new TextFileParser(
                commandText,
                new[] { ' ', ',' },
                Array.Empty<char>(),
                new[] { '@', '@' }
            );
            List<string> tokens = commandParser.GetTokens(commandText, false);
            result.Add(tokens.ToArray());
        }

        return result;
    }

    private static bool ContainsNumber(string text, int value)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        string marker = value.ToString(CultureInfo.InvariantCulture);
        int start = 0;

        while ((start = text.IndexOf(marker, start, StringComparison.Ordinal)) >= 0)
        {
            int before = start - 1;
            int after = start + marker.Length;

            if (
                (before < 0 || !char.IsDigit(text[before]))
                && (after >= text.Length || !char.IsDigit(text[after]))
            )
                return true;

            start = after;
        }

        return false;
    }

    private static int ParseInt(string value) =>
        int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static ushort ParseUShort(string value)
    {
        int equals = value.IndexOf('=');
        return UInt16Converter.Parse(equals >= 0 ? value[(equals + 1)..] : value);
    }

    private static bool TryParseSerial(string value, out uint serial)
    {
        try
        {
            serial = SerialHelper.Parse(value);
            return true;
        }
        catch (FormatException)
        {
            serial = 0;
            return false;
        }
        catch (OverflowException)
        {
            serial = 0;
            return false;
        }
    }
}

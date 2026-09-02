// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ClassicUO.Configuration;
using ClassicUO.Game.Managers;

namespace ClassicUO.Game.UI.Gumps;

/// <summary>
/// Builds the Diablo-style stat-change summary shown on the candidate item in an equipment
/// comparison. Numeric properties on only one item are treated as gains or losses from zero.
/// </summary>
internal static class ItemComparisonStatChanges
{
    private static readonly string[] NonStatPrefixes =
    [
        "artifact rarity",
        "charges",
        "contents",
        "dexterity requirement",
        "intelligence requirement",
        "price",
        "required level",
        "required skill",
        "requires",
        "sell value",
        "skill required",
        "strength requirement",
        "time left",
        "uses remaining",
        "vendor buy price",
        "vendor sell price",
        "weight"
    ];

    internal static string BuildSection(
        ItemPropertiesData candidate,
        ItemPropertiesData equipped
    )
    {
        if (candidate is not { HasData: true } || equipped is not { HasData: true })
            return string.Empty;

        List<StatChange> changes = Compare(candidate, equipped);
        var result = new StringBuilder();

        result.AppendLine();
        result.AppendLine("/c[gray]--------------------/cd");
        result.Append("/c[orange]")
            .Append(TazLang.Get("itemcomparison_statchanges", "Stat Changes if Equipped"))
            .AppendLine(":/cd");

        if (changes.Count == 0)
        {
            result.Append("/c[gray]")
                .Append(TazLang.Get("itemcomparison_nochanges", "No stat changes"))
                .AppendLine("/cd");
            return result.ToString();
        }

        foreach (StatChange change in changes)
        {
            AppendColoredDifference(result, change.FirstDifference, change.IsPercent);

            if (change.SecondDifference.HasValue)
            {
                result.Append(" / ");
                AppendColoredDifference(result, change.SecondDifference.Value, change.IsPercent);
            }

            result.Append(' ').AppendLine(change.Name);
        }

        return result.ToString();
    }

    private static List<StatChange> Compare(
        ItemPropertiesData candidate,
        ItemPropertiesData equipped
    )
    {
        var changes = new List<StatChange>();
        bool[] matchedEquippedProperties = new bool[equipped.singlePropertyData.Count];

        foreach (ItemPropertiesData.SinglePropertyData candidateProperty in candidate.singlePropertyData)
        {
            int equippedIndex = FindMatch(
                candidateProperty,
                equipped.singlePropertyData,
                matchedEquippedProperties
            );
            ItemPropertiesData.SinglePropertyData equippedProperty =
                equippedIndex >= 0 ? equipped.singlePropertyData[equippedIndex] : null;

            if (equippedIndex >= 0)
                matchedEquippedProperties[equippedIndex] = true;

            TryAddChange(changes, candidateProperty, equippedProperty);
        }

        for (int i = 0; i < equipped.singlePropertyData.Count; i++)
        {
            if (!matchedEquippedProperties[i])
                TryAddChange(changes, null, equipped.singlePropertyData[i]);
        }

        return changes;
    }

    private static int FindMatch(
        ItemPropertiesData.SinglePropertyData candidate,
        List<ItemPropertiesData.SinglePropertyData> equipped,
        bool[] matched
    )
    {
        for (int i = 0; i < equipped.Count; i++)
        {
            if (!matched[i] && NamesMatch(candidate, equipped[i]))
                return i;
        }

        return -1;
    }

    private static bool NamesMatch(
        ItemPropertiesData.SinglePropertyData first,
        ItemPropertiesData.SinglePropertyData second
    )
    {
        string[] firstNames = GetComparisonNames(first);
        string[] secondNames = GetComparisonNames(second);

        return firstNames.Any(firstName =>
            secondNames.Any(secondName =>
                firstName.Equals(secondName, StringComparison.OrdinalIgnoreCase)
            )
        );
    }

    private static string[] GetComparisonNames(ItemPropertiesData.SinglePropertyData property)
    {
        if (property == null)
            return [];

        string name = NormalizeName(property.Name);
        return [NormalizeName(property.Name)];
    }

    private static void TryAddChange(
        List<StatChange> changes,
        ItemPropertiesData.SinglePropertyData candidate,
        ItemPropertiesData.SinglePropertyData equipped
    )
    {
        ItemPropertiesData.SinglePropertyData displayProperty = candidate ?? equipped;
        if (displayProperty == null || IsNonStatProperty(candidate, equipped))
            return;

        bool candidateHasFirst = HasValue(candidate?.FirstValue);
        bool equippedHasFirst = HasValue(equipped?.FirstValue);
        if (!candidateHasFirst && !equippedHasFirst)
            return;

        double firstDifference = ValueOrZero(candidate?.FirstValue) - ValueOrZero(equipped?.FirstValue);
        bool candidateHasSecond = HasValue(candidate?.SecondValue);
        bool equippedHasSecond = HasValue(equipped?.SecondValue);
        double? secondDifference = candidateHasSecond || equippedHasSecond
            ? ValueOrZero(candidate?.SecondValue) - ValueOrZero(equipped?.SecondValue)
            : null;

        if (firstDifference == 0 && secondDifference.GetValueOrDefault() == 0)
            return;

        string displayName = displayProperty.Name?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
            return;

        changes.Add(
            new StatChange(
                displayName,
                firstDifference,
                secondDifference,
                ContainsPercent(candidate) || ContainsPercent(equipped)
            )
        );
    }

    private static bool IsNonStatProperty(
        ItemPropertiesData.SinglePropertyData candidate,
        ItemPropertiesData.SinglePropertyData equipped
    )
    {
        foreach (string name in GetComparisonNames(candidate).Concat(GetComparisonNames(equipped)))
        {
            // Durability Bonus is an equipment property; current/max Durability is item condition.
            if (name.StartsWith("durability bonus", StringComparison.OrdinalIgnoreCase))
                continue;

            if (name.Trim(' ', '/', ':', '-').Equals("durability", StringComparison.OrdinalIgnoreCase))
                return true;

            if (
                NonStatPrefixes.Any(prefix =>
                    name.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase)
                    || name.StartsWith(prefix + ":", StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var result = new StringBuilder(value.Length);
        bool insideClilocPlaceholder = false;
        bool pendingSpace = false;

        foreach (char character in value)
        {
            if (character == '~')
            {
                insideClilocPlaceholder = !insideClilocPlaceholder;
                continue;
            }

            if (insideClilocPlaceholder)
                continue;

            if (char.IsWhiteSpace(character))
            {
                pendingSpace = result.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                result.Append(' ');
                pendingSpace = false;
            }

            result.Append(char.ToLowerInvariant(character));
        }

        return result.ToString().Trim();
    }

    private static bool ContainsPercent(ItemPropertiesData.SinglePropertyData property) =>
        property?.OriginalString?.Contains('%') == true;

    private static bool HasValue(double? value) =>
        value.HasValue && value.Value != double.MinValue;

    private static double ValueOrZero(double? value) => HasValue(value) ? value.Value : 0;

    private static void AppendColoredDifference(StringBuilder result, double difference, bool percent)
    {
        string color = difference > 0 ? "green" : difference < 0 ? "red" : "gray";
        result.Append("/c[").Append(color).Append(']');

        if (difference > 0)
            result.Append('+');

        result.Append(difference.ToString("0.###", CultureInfo.InvariantCulture));
        if (percent)
            result.Append('%');

        result.Append("/cd");
    }

    private sealed record StatChange(
        string Name,
        double FirstDifference,
        double? SecondDifference,
        bool IsPercent
    );
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.Game.Managers;

public enum GridContainerFilterCurseMode
{
    Require,
    Exclude
}

public sealed class GridContainerFilterProperty
{
    public string Name { get; set; } = string.Empty;
    public int MinimumValue { get; set; } = -1;
}

public sealed class GridContainerFilterCurse
{
    public string Name { get; set; } = string.Empty;
    public GridContainerFilterCurseMode Mode { get; set; }
}

/// <summary>
/// Per-container item filter. Categories are combined with AND. Selected layers and item types
/// are alternatives within their own category; every needle, property, and required/excluded
/// curse is evaluated independently.
/// </summary>
public sealed class GridContainerFilter
{
    public bool Enabled { get; set; }
    public List<string> Needles { get; set; } = new();
    public List<GridContainerFilterProperty> Properties { get; set; } = new();
    public List<GridContainerFilterCurse> Curses { get; set; } = new();
    public List<byte> Layers { get; set; } = new();
    public List<string> ItemTypes { get; set; } = new();

    public bool HasCriteria =>
        Needles.Any(value => !string.IsNullOrWhiteSpace(value)) ||
        Properties.Any(value => value != null && !string.IsNullOrWhiteSpace(value.Name)) ||
        Curses.Any(value => value != null && !string.IsNullOrWhiteSpace(value.Name)) ||
        Layers.Count > 0 ||
        ItemTypes.Any(value => !string.IsNullOrWhiteSpace(value));

    public bool RequiresObjectProperties =>
        Properties.Any(value => value != null && !string.IsNullOrWhiteSpace(value.Name)) ||
        Curses.Any(value => value != null && !string.IsNullOrWhiteSpace(value.Name)) ||
        ItemTypes.Any(value => !string.IsNullOrWhiteSpace(value));

    public void Normalize()
    {
        Needles ??= new List<string>();
        Properties ??= new List<GridContainerFilterProperty>();
        Curses ??= new List<GridContainerFilterCurse>();
        Layers ??= new List<byte>();
        ItemTypes ??= new List<string>();

        Needles = Needles.Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        Properties = Properties.Where(value => value != null && !string.IsNullOrWhiteSpace(value.Name)).ToList();
        Curses = Curses.Where(value => value != null && !string.IsNullOrWhiteSpace(value.Name)).ToList();
        Layers = Layers.Distinct().ToList();
        ItemTypes = ItemTypes.Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        foreach (GridContainerFilterProperty property in Properties)
            property.MinimumValue = Math.Max(-1, property.MinimumValue);
    }

    public void Reset()
    {
        Enabled = false;
        Needles.Clear();
        Properties.Clear();
        Curses.Clear();
        Layers.Clear();
        ItemTypes.Clear();
    }

    public bool Matches(Item item, ItemPropertiesData itemData)
    {
        if (!Enabled || !HasCriteria)
            return true;

        if (item == null || Needles.Where(needle => !string.IsNullOrWhiteSpace(needle))
            .Any(needle => !ItemName(item, itemData).Contains(needle.Trim(), StringComparison.OrdinalIgnoreCase)))
            return false;

        if (Layers.Count > 0 && !Layers.Contains(item.ItemData.Layer))
            return false;

        if (!RequiresObjectProperties)
            return true;

        if (itemData == null || !itemData.HasData)
            return false;

        List<ItemPropertiesData.SinglePropertyData> lines = itemData.singlePropertyData
            .Where(line => line != null && !string.IsNullOrWhiteSpace(line.Name)).ToList();

        foreach (GridContainerFilterProperty property in Properties)
        {
            if (property == null || string.IsNullOrWhiteSpace(property.Name))
                continue;

            string rule = NormalizeText(property.Name);
            bool matched = lines.Any(line => PropertyNameMatches(line.Name, rule) &&
                                             (property.MinimumValue == -1 ||
                                              line.FirstValue != double.MinValue && line.FirstValue >= property.MinimumValue));
            if (!matched)
                return false;
        }

        foreach (GridContainerFilterCurse curse in Curses)
        {
            if (curse == null || string.IsNullOrWhiteSpace(curse.Name))
                continue;

            string rule = NormalizeText(curse.Name);
            bool present = lines.Any(line => PropertyNameMatches(line.Name, rule));
            if (curse.Mode == GridContainerFilterCurseMode.Require && !present ||
                curse.Mode == GridContainerFilterCurseMode.Exclude && present)
                return false;
        }

        List<string> selectedTypes = ItemTypes.Where(type => !string.IsNullOrWhiteSpace(type)).ToList();
        if (selectedTypes.Count > 0 && !lines.Any(line => selectedTypes.Any(type => PropertyNameMatches(line.Name, NormalizeText(type)))))
            return false;

        return true;
    }

    private static string ItemName(Item item, ItemPropertiesData itemData) =>
        itemData != null && itemData.HasData && !string.IsNullOrWhiteSpace(itemData.Name)
            ? itemData.Name
            : item.GetNormalizedName(false) ?? string.Empty;

    private static bool PropertyNameMatches(string value, string normalizedRule)
    {
        string normalizedValue = NormalizeText(value);
        return normalizedValue.Equals(normalizedRule, StringComparison.OrdinalIgnoreCase) ||
               normalizedValue.StartsWith(normalizedRule, StringComparison.OrdinalIgnoreCase) &&
               (normalizedValue.Length == normalizedRule.Length || !char.IsLetterOrDigit(normalizedValue[normalizedRule.Length]));
    }

    private static string NormalizeText(string value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormKC);
}

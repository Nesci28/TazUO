using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ClassicUO.Game.UI.Gumps.GridHighLight
{
    public class GridHighlightSetupEntry
    {
        public bool Enabled { get; set; } = true;
        public string Name { get; set; }
        public List<string> ItemNames { get; set; } = new();
        [Obsolete("Legacy UO hue retained only for importing older grid-highlight configurations.")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public ushort Hue { get; set; }
        public string HighlightColor { get; set; } = "#FF0000";
        public List<GridHighlightProperty> Properties { get; set; } = new();
        public bool AcceptExtraProperties { get; set; } = true;
        public bool Overweight { get; set; }
        public int MinimumWeight { get; set; } = 0;
        public int MaximumWeight { get; set; } = 0;
        public int MinimumProperty { get; set; } = 0;
        public int MaximumProperty { get; set; } = 0;
        public int MinimumMatchingProperty { get; set; } = 0;
        public int MaximumMatchingProperty { get; set; } = 0;
        public List<string> ExcludeNegatives { get; set; } = new();
        public List<string> RequiredRarities { get; set; } = new();
        public GridHighlightSlot GridHighlightSlot { get; set; } = new();
        public bool LootOnMatch { get; set; } = false;
        public uint DestinationContainer { get; set; } = 0;
        public Color GetHighlightColor() => HighlightColor.FromHtmlHex(Color.Red);

        // Keep alpha for grid highlights. The shared ToHtmlHex helper intentionally emits RGB only.
        public void SetHighlightColor(Color color) =>
            HighlightColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";

        /// <summary>
        /// Repairs values loaded from user-editable or legacy JSON without replacing the list instances
        /// currently used by the editor. Match caches ignore blank rows and duplicate values.
        /// </summary>
        public void Normalize()
        {
            Name ??= string.Empty;
            ItemNames = NormalizeStrings(ItemNames);
            ExcludeNegatives = NormalizeStrings(ExcludeNegatives);
            RequiredRarities = NormalizeStrings(RequiredRarities);
            Properties ??= new List<GridHighlightProperty>();
            foreach (GridHighlightProperty property in Properties)
            {
                if (property != null)
                {
                    property.Name = property.Name?.Trim() ?? string.Empty;
                    property.MinValue = Math.Max(-1, property.MinValue);
                }
            }
            GridHighlightSlot ??= new GridHighlightSlot();

            MinimumWeight = Math.Max(0, MinimumWeight);
            MaximumWeight = Math.Max(0, MaximumWeight);
            MinimumProperty = Math.Max(0, MinimumProperty);
            MaximumProperty = Math.Max(0, MaximumProperty);
            MinimumMatchingProperty = Math.Max(0, MinimumMatchingProperty);
            MaximumMatchingProperty = Math.Max(0, MaximumMatchingProperty);

            if (MaximumWeight > 0 && MinimumWeight > MaximumWeight)
                (MinimumWeight, MaximumWeight) = (MaximumWeight, MinimumWeight);
            if (MaximumProperty > 0 && MinimumProperty > MaximumProperty)
                (MinimumProperty, MaximumProperty) = (MaximumProperty, MinimumProperty);
            if (MaximumMatchingProperty > 0 && MinimumMatchingProperty > MaximumMatchingProperty)
                (MinimumMatchingProperty, MaximumMatchingProperty) = (MaximumMatchingProperty, MinimumMatchingProperty);

            // Accessing the color also validates it and supplies a safe fallback.
            SetHighlightColor(GetHighlightColor());
        }

        private static List<string> NormalizeStrings(List<string> values)
        {
            values ??= new List<string>();
            for (int i = 0; i < values.Count; i++)
                values[i] = values[i]?.Trim() ?? string.Empty;
            return values;
        }
    }

    public class GridHighlightSlot
    {
        public bool Talisman { get; set; } = true;
        public bool RightHand { get; set; } = true;
        public bool LeftHand { get; set; } = true;
        public bool Head { get; set; } = true;
        public bool Earring { get; set; } = true;
        public bool Neck { get; set; } = true;
        public bool Chest { get; set; } = true;
        public bool Shirt { get; set; } = true;
        public bool Back { get; set; } = true;
        public bool Robe { get; set; } = true;
        public bool Arms { get; set; } = true;
        public bool Hands { get; set; } = true;
        public bool Bracelet { get; set; } = true;
        public bool Ring { get; set; } = true;
        public bool Belt { get; set; } = true;
        public bool Skirt { get; set; } = true;
        public bool Legs { get; set; } = true;
        public bool Footwear { get; set; } = true;
        public bool Other { get; set; } = false;
    }

    public class GridHighlightProperty
    {
        public string Name { get; set; }
        public int MinValue { get; set; } = -1;
        public bool IsOptional { get; set; } = false;
    }
}

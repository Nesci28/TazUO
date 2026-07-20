using ClassicUO.Configuration;
using System.Collections.Generic;
using System.Linq;

namespace ClassicUO.Game.UI.Gumps.GridHighLight
{
    public class GridHighLightProfile
    {
#pragma warning disable CS0618 // Populating the obsolete GridHighlightSetup is a one-time migration step toward grid_highlights.json.
        public static void MigrateGridHighlightToSetup(Profile profile)
        {
            if (profile == null)
                return;

            profile.GridHighlightSetup ??= new();
            profile.GridHighlight_Name ??= new();
            profile.GridHighlight_Hue ??= new();
            profile.GridHighlight_PropNames ??= new();
            profile.GridHighlight_PropMinVal ??= new();
            profile.GridHighlight_AcceptExtraProperties ??= new();
            profile.GridHighlight_IsOptionalProperties ??= new();
            profile.GridHighlight_ExcludeNegatives ??= new();
            profile.GridHighlight_RequiredRarities ??= new();

            profile.GridHighlightSetup.Clear();
            int count = profile.GridHighlight_Name.Count;

            for (int i = 0; i < count; i++)
            {
                var entry = new GridHighlightSetupEntry
                {
                    Name = profile.GridHighlight_Name[i],
                    Hue = profile.GridHighlight_Hue.ElementAtOrDefault(i),
                    AcceptExtraProperties = i < profile.GridHighlight_AcceptExtraProperties.Count
                        ? profile.GridHighlight_AcceptExtraProperties[i]
                        : true,
                    ExcludeNegatives = profile.GridHighlight_ExcludeNegatives.ElementAtOrDefault(i) ?? new(),
                    RequiredRarities = profile.GridHighlight_RequiredRarities.ElementAtOrDefault(i) ?? new(),
                    Properties = new List<GridHighlightProperty>()
                };

                TryMigrateLegacyHue(entry);

                List<string> names = profile.GridHighlight_PropNames.ElementAtOrDefault(i) ?? new();
                List<int> mins = profile.GridHighlight_PropMinVal.ElementAtOrDefault(i) ?? new();
                List<bool> opts = profile.GridHighlight_IsOptionalProperties.ElementAtOrDefault(i);

                for (int j = 0; j < names.Count; j++)
                {
                    entry.Properties.Add(new GridHighlightProperty
                    {
                        Name = names[j],
                        MinValue = j < mins.Count ? mins[j] : -1,
                        IsOptional = opts != null && j < opts.Count ? opts[j] : false
                    });
                }

                profile.GridHighlightSetup.Add(entry);
            }

            // Clear legacy lists
            profile.GridHighlight_Name.Clear();
            profile.GridHighlight_Hue.Clear();
            profile.GridHighlight_PropNames.Clear();
            profile.GridHighlight_PropMinVal.Clear();
            profile.GridHighlight_AcceptExtraProperties.Clear();
            profile.GridHighlight_IsOptionalProperties.Clear();
            profile.GridHighlight_ExcludeNegatives.Clear();
            profile.GridHighlight_RequiredRarities.Clear();
        }

        internal static void TryMigrateLegacyHue(GridHighlightSetupEntry entry)
        {
            if (entry == null || entry.Hue == 0)
                return;

            try
            {
                uint packed = Client.Game.UO.FileManager.Hues.GetHueColorRgba8888(31, entry.Hue);
                entry.SetHighlightColor(new Microsoft.Xna.Framework.Color { PackedValue = packed });
                entry.Hue = 0;
            }
            catch
            {
                // Assets may not be available during headless migration; retain both the hue and safe default.
            }
        }
#pragma warning restore CS0618
    }
}

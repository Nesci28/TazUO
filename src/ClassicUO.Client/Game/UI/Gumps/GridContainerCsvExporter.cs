// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Network.PacketHandlers.Helpers;
using ClassicUO.Utility;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.UI.Gumps
{
    internal static class GridContainerCsvExporter
    {
        private const int MaxOpenPasses = 8;
        private const int OpenPassDelayMs = 650;

        private static readonly string[] Headers =
        {
            "Serial", "ItemID", "Hue", "Amount", "Layer", "ContainerSerial", "ParentContainerSerial", "Depth", "LocationPath", "Name", "ArtifactRarity", "Weight", "Insured", "Blessed", "Cursed", "Exceptional", "Crafter", "DurabilityCurrent", "DurabilityMax", "PhysicalResist", "FireResist", "ColdResist", "PoisonResist", "EnergyResist", "PhysicalDamage", "FireDamage", "ColdDamage", "PoisonDamage", "EnergyDamage", "ChaosDamage", "DirectDamage", "DamageIncrease", "HitChanceIncrease", "DefenseChanceIncrease", "SwingSpeedIncrease", "LowerAttackCost", "LowerDefenseCost", "Velocity", "UseBestWeaponSkill", "MageWeapon", "SplinteringWeapon", "BloodDrinker", "BattleLust", "HitLeechHits", "HitLeechMana", "HitLeechStamina", "HitManaDrain", "HitStaminaDrain", "HitFireball", "HitHarm", "HitLightning", "HitMagicArrow", "HitDispel", "HitLowerAttack", "HitLowerDefense", "HitFatigue", "HitPhysicalArea", "HitFireArea", "HitColdArea", "HitPoisonArea", "HitEnergyArea", "HitCurse", "HitSpellPlague", "HitMortalStrike", "HitAreaAttack", "StrengthBonus", "DexterityBonus", "IntelligenceBonus", "HitPointIncrease", "StaminaIncrease", "ManaIncrease", "HitPointRegen", "StaminaRegen", "ManaRegen", "LowerManaCost", "LowerReagentCost", "SpellDamageIncrease", "FasterCasting", "FasterCastRecovery", "EnhancePotions", "Luck", "NightSight", "ReflectPhysicalDamage", "CastingFocus", "SpellChanneling", "LowerRequirements", "MageArmor", "SelfRepair", "DurabilityBonus", "PhysicalResistBonus", "FireResistBonus", "ColdResistBonus", "PoisonResistBonus", "EnergyResistBonus", "SkillBonus1Name", "SkillBonus1Value", "SkillBonus2Name", "SkillBonus2Value", "SkillBonus3Name", "SkillBonus3Value", "SkillBonus4Name", "SkillBonus4Value", "SkillBonus5Name", "SkillBonus5Value", "Slayer", "SuperSlayer", "ChargesCurrent", "ChargesMax", "ChargeType", "SetName", "SetPieces", "SetBonusActive", "GargoyleOnly", "ElvesOnly", "HumanOnly", "RaceRestriction", "FactionItem", "Imbued", "Antique", "Brittle", "CannotBeRepaired", "Prized", "NoTrade", "QuestItem", "Replica", "MinorArtifact", "MajorArtifact", "LegendaryArtifact", "DamageEater", "FireEater", "ColdEater", "PoisonEater", "EnergyEater", "KineticEater", "ResonanceFire", "ResonanceCold", "ResonancePoison", "ResonanceEnergy", "ResonanceKinetic", "ResonanceChaos", "PropertiesRaw"
        };

        private static readonly Dictionary<string, string> PropertyColumns = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Artifact Rarity"] = "ArtifactRarity",
            ["Weight"] = "Weight",
            ["Durability"] = "DurabilityCurrent",
            ["Physical Resist"] = "PhysicalResist",
            ["Fire Resist"] = "FireResist",
            ["Cold Resist"] = "ColdResist",
            ["Poison Resist"] = "PoisonResist",
            ["Energy Resist"] = "EnergyResist",
            ["Physical Damage"] = "PhysicalDamage",
            ["Fire Damage"] = "FireDamage",
            ["Cold Damage"] = "ColdDamage",
            ["Poison Damage"] = "PoisonDamage",
            ["Energy Damage"] = "EnergyDamage",
            ["Chaos Damage"] = "ChaosDamage",
            ["Direct Damage"] = "DirectDamage",
            ["Damage Increase"] = "DamageIncrease",
            ["Weapon Damage"] = "DamageIncrease",
            ["Hit Chance Increase"] = "HitChanceIncrease",
            ["Defense Chance Increase"] = "DefenseChanceIncrease",
            ["Swing Speed Increase"] = "SwingSpeedIncrease",
            ["Lower Attack Cost"] = "LowerAttackCost",
            ["Lower Defense Cost"] = "LowerDefenseCost",
            ["Velocity"] = "Velocity",
            ["Mage Weapon"] = "MageWeapon",
            ["Splintering Weapon"] = "SplinteringWeapon",
            ["Hit Life Leech"] = "HitLeechHits",
            ["Hit Mana Leech"] = "HitLeechMana",
            ["Hit Stamina Leech"] = "HitLeechStamina",
            ["Hit Mana Drain"] = "HitManaDrain",
            ["Hit Stamina Drain"] = "HitStaminaDrain",
            ["Hit Fireball"] = "HitFireball",
            ["Hit Harm"] = "HitHarm",
            ["Hit Lightning"] = "HitLightning",
            ["Hit Magic Arrow"] = "HitMagicArrow",
            ["Hit Dispel"] = "HitDispel",
            ["Hit Lower Attack"] = "HitLowerAttack",
            ["Hit Lower Defense"] = "HitLowerDefense",
            ["Hit Fatigue"] = "HitFatigue",
            ["Hit Physical Area"] = "HitPhysicalArea",
            ["Hit Fire Area"] = "HitFireArea",
            ["Hit Cold Area"] = "HitColdArea",
            ["Hit Poison Area"] = "HitPoisonArea",
            ["Hit Energy Area"] = "HitEnergyArea",
            ["Hit Curse"] = "HitCurse",
            ["Hit Spell Plague"] = "HitSpellPlague",
            ["Hit Mortal Strike"] = "HitMortalStrike",
            ["Strength Bonus"] = "StrengthBonus",
            ["Dexterity Bonus"] = "DexterityBonus",
            ["Intelligence Bonus"] = "IntelligenceBonus",
            ["Hit Point Increase"] = "HitPointIncrease",
            ["Stamina Increase"] = "StaminaIncrease",
            ["Mana Increase"] = "ManaIncrease",
            ["Hit Point Regeneration"] = "HitPointRegen",
            ["Stamina Regeneration"] = "StaminaRegen",
            ["Mana Regeneration"] = "ManaRegen",
            ["Lower Mana Cost"] = "LowerManaCost",
            ["Lower Reagent Cost"] = "LowerReagentCost",
            ["Spell Damage Increase"] = "SpellDamageIncrease",
            ["Faster Casting"] = "FasterCasting",
            ["Faster Cast Recovery"] = "FasterCastRecovery",
            ["Enhance Potions"] = "EnhancePotions",
            ["Luck"] = "Luck",
            ["Reflect Physical Damage"] = "ReflectPhysicalDamage",
            ["Casting Focus"] = "CastingFocus",
            ["Lower Requirements"] = "LowerRequirements",
            ["Self Repair"] = "SelfRepair",
            ["Durability Bonus"] = "DurabilityBonus",
            ["Physical Resist Bonus"] = "PhysicalResistBonus",
            ["Fire Resist Bonus"] = "FireResistBonus",
            ["Cold Resist Bonus"] = "ColdResistBonus",
            ["Poison Resist Bonus"] = "PoisonResistBonus",
            ["Energy Resist Bonus"] = "EnergyResistBonus",
            ["Damage Eater"] = "DamageEater",
            ["Fire Eater"] = "FireEater",
            ["Cold Eater"] = "ColdEater",
            ["Poison Eater"] = "PoisonEater",
            ["Energy Eater"] = "EnergyEater",
            ["Kinetic Eater"] = "KineticEater"
        };

        private static readonly Dictionary<string, string> BooleanColumns = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Insured"] = "Insured",
            ["Blessed"] = "Blessed",
            ["Cursed"] = "Cursed",
            ["Exceptional"] = "Exceptional",
            ["Use Best Weapon Skill"] = "UseBestWeaponSkill",
            ["Blood Drinker"] = "BloodDrinker",
            ["Battle Lust"] = "BattleLust",
            ["Night Sight"] = "NightSight",
            ["Spell Channeling"] = "SpellChanneling",
            ["Mage Armor"] = "MageArmor",
            ["Set Bonus Active"] = "SetBonusActive",
            ["Gargoyle Only"] = "GargoyleOnly",
            ["Elves Only"] = "ElvesOnly",
            ["Elf Only"] = "ElvesOnly",
            ["Human Only"] = "HumanOnly",
            ["Faction Item"] = "FactionItem",
            ["Imbued"] = "Imbued",
            ["Antique"] = "Antique",
            ["Brittle"] = "Brittle",
            ["Cannot Be Repaired"] = "CannotBeRepaired",
            ["Prized"] = "Prized",
            ["No Trade"] = "NoTrade",
            ["Quest Item"] = "QuestItem",
            ["Replica"] = "Replica",
            ["Minor Artifact"] = "MinorArtifact",
            ["Major Artifact"] = "MajorArtifact",
            ["Legendary Artifact"] = "LegendaryArtifact"
        };

        public static async void Export(World world, Item container)
        {
            if (world == null || container == null)
            {
                return;
            }

            try
            {
                string path = await ExportAsync(world, container);
                UIManager.Add(new MessageBoxGump(world, 420, 150, TazLang.Get("gridcontainer_exportcsv_done", new[] { path }), null));
            }
            catch (Exception ex)
            {
                Log.Error($"Grid container CSV export failed: {ex}");
                UIManager.Add(new MessageBoxGump(world, 320, 120, TazLang.Get("gridcontainer_exportcsv_failed", "Container CSV export failed."), null));
            }
        }

        private static async Task<string> ExportAsync(World world, Item rootContainer)
        {
            await OpenNestedContainers(rootContainer);

            var rows = new List<Dictionary<string, string>>();
            CollectRows(world, rootContainer, rootContainer.Serial, 0, GetItemName(world, rootContainer), rows, new HashSet<uint>());

            string directory = Path.Combine(ProfileManager.ProfilePath, "Exports");
            Directory.CreateDirectory(directory);

            string safeName = SanitizeFileName(GetItemName(world, rootContainer));
            string path = Path.Combine(directory, $"container_{rootContainer.Serial:X8}_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            using (var writer = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                writer.WriteLine(string.Join(",", Headers.Select(EscapeCsv)));

                foreach (Dictionary<string, string> row in rows)
                {
                    writer.WriteLine(string.Join(",", Headers.Select(header => EscapeCsv(row.TryGetValue(header, out string value) ? value : string.Empty))));
                }
            }

            return path;
        }

        private static async Task OpenNestedContainers(Item rootContainer)
        {
            var opened = new HashSet<uint>();

            for (int pass = 0; pass < MaxOpenPasses; pass++)
            {
                bool requestedAny = RequestClosedNestedContainers(rootContainer, opened, new HashSet<uint>());

                if (!requestedAny)
                {
                    return;
                }

                await Task.Delay(OpenPassDelayMs);
            }
        }

        private static bool RequestClosedNestedContainers(Item container, HashSet<uint> opened, HashSet<uint> visited)
        {
            if (container == null || !visited.Add(container.Serial))
            {
                return false;
            }

            bool requestedAny = false;

            for (LinkedObject node = container.Items; node != null; node = node.Next)
            {
                if (node is not Item item || item.IsDestroyed || !item.ItemData.IsContainer)
                {
                    continue;
                }

                if (item.IsEmpty && opened.Add(item.Serial))
                {
                    GameActions.DoubleClickQueued(item.Serial);
                    requestedAny = true;
                }

                requestedAny |= RequestClosedNestedContainers(item, opened, visited);
            }

            return requestedAny;
        }

        private static void CollectRows(World world, Item container, uint rootSerial, int depth, string locationPath, List<Dictionary<string, string>> rows, HashSet<uint> visited)
        {
            if (container == null || !visited.Add(container.Serial))
            {
                return;
            }

            for (LinkedObject node = container.Items; node != null; node = node.Next)
            {
                if (node is not Item item || item.IsDestroyed)
                {
                    continue;
                }

                string name = GetItemName(world, item);
                string itemPath = string.IsNullOrWhiteSpace(locationPath) ? name : $"{locationPath}/{name}";
                Dictionary<string, string> row = CreateBaseRow(world, item, depth, itemPath, name);
                ParseProperties(world, item, row);
                rows.Add(row);

                if (item.ItemData.IsContainer && !item.IsEmpty)
                {
                    CollectRows(world, item, rootSerial, depth + 1, itemPath, rows, visited);
                }
            }
        }

        private static Dictionary<string, string> CreateBaseRow(World world, Item item, int depth, string locationPath, string name)
        {
            var row = Headers.ToDictionary(header => header, _ => string.Empty, StringComparer.OrdinalIgnoreCase);

            row["Serial"] = item.Serial.ToString(CultureInfo.InvariantCulture);
            row["ItemID"] = item.Graphic.ToString(CultureInfo.InvariantCulture);
            row["Hue"] = item.Hue.ToString(CultureInfo.InvariantCulture);
            row["Amount"] = item.Amount.ToString(CultureInfo.InvariantCulture);
            row["Layer"] = item.Layer.ToString();
            row["ContainerSerial"] = item.Container.ToString(CultureInfo.InvariantCulture);
            row["ParentContainerSerial"] = GetParentContainerSerial(world, item.Container);
            row["Depth"] = depth.ToString(CultureInfo.InvariantCulture);
            row["LocationPath"] = locationPath;
            row["Name"] = name;

            if (item.ItemData.Weight > 0)
            {
                row["Weight"] = item.ItemData.Weight.ToString(CultureInfo.InvariantCulture);
            }

            if (!world.OPL.Contains(item.Serial))
            {
                SharedStore.AddMegaCliLocRequest(item.Serial);
            }

            return row;
        }

        private static void ParseProperties(World world, Item item, Dictionary<string, string> row)
        {
            if (!world.OPL.TryGetNameAndData(item.Serial, out _, out string data) || string.IsNullOrWhiteSpace(data))
            {
                return;
            }

            string[] rawLines = data.Split(new[] { "\n", "<br>" }, StringSplitOptions.RemoveEmptyEntries);
            row["PropertiesRaw"] = string.Join(" | ", rawLines.Select(CleanLine).Where(line => !string.IsNullOrWhiteSpace(line)));

            int skillIndex = 1;

            foreach (string rawLine in rawLines)
            {
                string line = CleanLine(rawLine);

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                ApplyKnownLine(row, line, ref skillIndex);
            }
        }

        private static void ApplyKnownLine(Dictionary<string, string> row, string line, ref int skillIndex)
        {
            string normalized = NormalizeName(line);

            if (BooleanColumns.TryGetValue(normalized, out string boolColumn))
            {
                row[boolColumn] = "true";
                return;
            }

            if (TrySetDurability(row, line))
            {
                return;
            }

            if (TrySetCharges(row, line))
            {
                return;
            }

            if (TrySetSkillBonus(row, line, ref skillIndex))
            {
                return;
            }

            if (TrySetCrafter(row, line) || TrySetSlayer(row, line) || TrySetRaceRestriction(row, line) || TrySetSetInfo(row, line) || TrySetResonance(row, line))
            {
                return;
            }

            string propertyName = ExtractPropertyName(line);

            if (PropertyColumns.TryGetValue(propertyName, out string column) && TryGetFirstNumber(line, out string value))
            {
                row[column] = value;
            }
        }

        private static bool TrySetDurability(Dictionary<string, string> row, string line)
        {
            Match match = Regex.Match(line, @"Durability\s+(\d+)\s*/\s*(\d+)", RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                return false;
            }

            row["DurabilityCurrent"] = match.Groups[1].Value;
            row["DurabilityMax"] = match.Groups[2].Value;
            return true;
        }

        private static bool TrySetCharges(Dictionary<string, string> row, string line)
        {
            Match match = Regex.Match(line, @"(?:(?<type>.+?)\s+)?Charges?:\s*(?<current>\d+)(?:\s*/\s*(?<max>\d+))?", RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                return false;
            }

            row["ChargesCurrent"] = match.Groups["current"].Value;
            row["ChargesMax"] = match.Groups["max"].Value;
            row["ChargeType"] = NormalizeName(match.Groups["type"].Value);
            return true;
        }

        private static bool TrySetSkillBonus(Dictionary<string, string> row, string line, ref int skillIndex)
        {
            if (skillIndex > 5 || !TryGetFirstNumber(line, out string value))
            {
                return false;
            }

            string name = ExtractPropertyName(line);

            if (!name.EndsWith("Skill Bonus", StringComparison.OrdinalIgnoreCase) && !line.Contains("skill", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            row[$"SkillBonus{skillIndex}Name"] = name.Replace(" Skill Bonus", string.Empty, StringComparison.OrdinalIgnoreCase);
            row[$"SkillBonus{skillIndex}Value"] = value;
            skillIndex++;

            return true;
        }

        private static bool TrySetCrafter(Dictionary<string, string> row, string line)
        {
            Match match = Regex.Match(line, @"^(?:crafted by|crafter)\s*:?\s*(.+)$", RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                return false;
            }

            row["Crafter"] = match.Groups[1].Value.Trim();
            row["Exceptional"] = "true";
            return true;
        }

        private static bool TrySetSlayer(Dictionary<string, string> row, string line)
        {
            if (!line.Contains("slayer", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (line.Contains("super", StringComparison.OrdinalIgnoreCase))
            {
                row["SuperSlayer"] = line;
            }
            else
            {
                row["Slayer"] = line;
            }

            return true;
        }

        private static bool TrySetRaceRestriction(Dictionary<string, string> row, string line)
        {
            if (!line.Contains(" only", StringComparison.OrdinalIgnoreCase) && !line.Contains("Race", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string normalized = NormalizeName(line);

            if (BooleanColumns.TryGetValue(normalized, out string column))
            {
                row[column] = "true";
            }

            row["RaceRestriction"] = line;
            return true;
        }

        private static bool TrySetSetInfo(Dictionary<string, string> row, string line)
        {
            if (!line.Contains("set", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Match pieces = Regex.Match(line, @"\((\d+)\s*/\s*(\d+)\)", RegexOptions.IgnoreCase);

            if (pieces.Success)
            {
                row["SetPieces"] = $"{pieces.Groups[1].Value}/{pieces.Groups[2].Value}";
            }

            if (string.IsNullOrEmpty(row["SetName"]))
            {
                row["SetName"] = line;
            }

            return true;
        }

        private static bool TrySetResonance(Dictionary<string, string> row, string line)
        {
            if (!line.StartsWith("Resonance", StringComparison.OrdinalIgnoreCase) || !TryGetFirstNumber(line, out string value))
            {
                return false;
            }

            foreach (string type in new[] { "Fire", "Cold", "Poison", "Energy", "Kinetic", "Chaos" })
            {
                if (line.Contains(type, StringComparison.OrdinalIgnoreCase))
                {
                    row[$"Resonance{type}"] = value;
                    return true;
                }
            }

            return false;
        }

        private static string GetItemName(World world, Item item)
        {
            if (world.OPL.TryGetNameAndData(item.Serial, out string name, out _) && !string.IsNullOrWhiteSpace(name))
            {
                return CleanLine(name);
            }

            return !string.IsNullOrWhiteSpace(item.Name) ? item.Name : item.ItemData.Name;
        }

        private static string GetParentContainerSerial(World world, uint containerSerial)
        {
            Item container = world.Items.Get(containerSerial);

            if (container == null || !SerialHelper.IsValid(container.Container))
            {
                return string.Empty;
            }

            return container.Container.ToString(CultureInfo.InvariantCulture);
        }

        private static string ExtractPropertyName(string line) => NormalizeName(Regex.Replace(line, @"[-+]?\d+(\.\d+)?\s*%?(?:\s*/\s*\d+)?", string.Empty).Trim(':', ' '));

        private static string NormalizeName(string value) => Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();

        private static bool TryGetFirstNumber(string line, out string value)
        {
            Match match = Regex.Match(line, @"[-+]?\d+(?:\.\d+)?");
            value = match.Success ? match.Value : string.Empty;
            return match.Success;
        }

        private static string CleanLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return string.Empty;
            }

            string cleaned = Regex.Replace(line, @"<[^>]+>", string.Empty);
            cleaned = Regex.Replace(cleaned, @"/c\[[#a-zA-Z0-9]+\]", string.Empty, RegexOptions.IgnoreCase).Replace("/cd", string.Empty);
            return NormalizeName(cleaned);
        }

        private static string EscapeCsv(string value)
        {
            value ??= string.Empty;

            if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }

        private static string SanitizeFileName(string value)
        {
            string sanitized = string.Join("_", (value ?? "container").Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "container" : sanitized;
        }
    }
}

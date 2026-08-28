using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Game.GameObjects;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClassicUO.Game.UI.Gumps.GridHighLight
{
    public class GridHighlightData
    {
        private static GridHighlightData[] allConfigs;
        private readonly GridHighlightSetupEntry _entry;

        private static readonly Queue<uint> _queue = new();
        private static readonly HashSet<uint> _queuedItems = new();
        private static readonly HashSet<uint> _waitingForOpl = new();
        private const int MaxWaitingForOpl = 8192;
        private static readonly string[] WeightPropertyNames =
        [
            "weight", "poids", "gewicht", "peso", "waga", "вес", "vikt", "vekt",
            "vægt", "paino", "hmotnost", "súly", "ağırlık", "重量", "重さ", "무게"
        ];
        private static bool _subscribed;

        private readonly Dictionary<string, string> _normalizeCache = new();

        public static GridHighlightData[] AllConfigs
        {
            get
            {
                if (allConfigs != null)
                    return allConfigs;

                GridHighlightsConfig.Current.Normalize();
                List<GridHighlightSetupEntry> setup = GridHighlightsConfig.Current.Highlights;
                allConfigs = setup.Select(entry => new GridHighlightData(entry)).ToArray();
                return allConfigs;
            }
            set => allConfigs = value;
        }

        public bool Enabled
        {
            get => _entry.Enabled;
            set => _entry.Enabled = value;
        }

        public string Name
        {
            get => _entry.Name;
            set => _entry.Name = value;
        }

        public List<string> ItemNames
        {
            get => _entry.ItemNames;
            set => _entry.ItemNames = value;
        }

        public Color HighlightColor
        {
            get => _entry.GetHighlightColor();
            set => _entry.SetHighlightColor(value);
        }

        public List<GridHighlightProperty> Properties
        {
            get => _entry.Properties;
            set
            {
                _entry.Properties = value;
                InvalidateCache();
            }
        }

        public bool AcceptExtraProperties
        {
            get => _entry.AcceptExtraProperties;
            set => _entry.AcceptExtraProperties = value;
        }

        public int MinimumProperty
        {
            get => _entry.MinimumProperty;
            set => _entry.MinimumProperty = value;
        }

        public int MaximumProperty
        {
            get => _entry.MaximumProperty;
            set => _entry.MaximumProperty = value;
        }

        public int MinimumMatchingProperty
        {
            get => _entry.MinimumMatchingProperty;
            set => _entry.MinimumMatchingProperty = value;
        }

        public int MaximumMatchingProperty
        {
            get => _entry.MaximumMatchingProperty;
            set => _entry.MaximumMatchingProperty = value;
        }

        public List<string> ExcludeNegatives
        {
            get => _entry.ExcludeNegatives;
            set
            {
                _entry.ExcludeNegatives = value;
                InvalidateCache();
            }
        }

        public bool Overweight
        {
            get => _entry.Overweight;
            set => _entry.Overweight = value;
        }

        public int MinimumWeight
        {
            get => _entry.MinimumWeight;
            set => _entry.MinimumWeight = value;
        }

        public int MaximumWeight
        {
            get => _entry.MaximumWeight;
            set => _entry.MaximumWeight = value;
        }

        public List<string> RequiredRarities
        {
            get => _entry.RequiredRarities;
            set
            {
                _entry.RequiredRarities = value;
                InvalidateCache();
            }
        }

        public GridHighlightSlot EquipmentSlots
        {
            get => _entry.GridHighlightSlot;
            set => _entry.GridHighlightSlot = value;
        }

        public bool LootOnMatch
        {
            get => _entry.LootOnMatch;
            set => _entry.LootOnMatch = value;
        }

        public uint DestinationContainer
        {
            get => _entry.DestinationContainer;
            set
            {
                _entry.DestinationContainer = value;
                _cachedLootEntry = null; // Invalidate cache when container changes
            }
        }

        private AutoLootManager.AutoLootConfigEntry _cachedLootEntry;

        private AutoLootManager.AutoLootConfigEntry GetLootEntry()
        {
            if (DestinationContainer == 0)
                return null;

            if (_cachedLootEntry == null || _cachedLootEntry.DestinationContainer != DestinationContainer)
            {
                _cachedLootEntry = new AutoLootManager.AutoLootConfigEntry
                {
                    DestinationContainer = DestinationContainer
                };
            }

            return _cachedLootEntry;
        }

        private List<string> _cachedNormalizedItemNames;
        private List<string> _cachedNormalizedRulesExcludeNegatives;
        private HashSet<string> _cachedNormalizedRulesRequiredRarities;
        private HashSet<string> _cachedNormalizedKnownRarities;
        private Dictionary<string, (int MinValue, bool IsOptional)> _cachedNormalizedRulesProperties;
        private static readonly List<ItemPropertiesData> _reusableItemData = new(3);
        private bool _cacheValid = false;

        internal GridHighlightData(GridHighlightSetupEntry entry)
        {
            _entry = entry ?? throw new ArgumentNullException(nameof(entry));
            _entry.Normalize();
        }

        public void Delete()
        {
            GridHighlightsConfig.Current.Highlights.Remove(_entry);
            allConfigs = null;
        }

        public void Move(bool up)
        {
            List<GridHighlightSetupEntry> list = GridHighlightsConfig.Current.Highlights;
            int index = list.IndexOf(_entry);
            if (index == -1) return; // Not found

            // Prevent moving out of bounds
            if (up && index == 0) return;
            if (!up && index == list.Count - 1) return;

            list.RemoveAt(index);
            list.Insert(up ? index - 1 : index + 1, _entry);
            allConfigs = null;
        }

        public static void Unload()
        {
            if (_subscribed)
            {
                EventSink.OPLOnReceive -= OnOplReceived;
                _subscribed = false;
            }

            allConfigs = null;
            _queue.Clear();
            _queuedItems.Clear();
            _waitingForOpl.Clear();
        }

        public static void OnSceneLoad()
        {
            if (_subscribed)
                return;

            EventSink.OPLOnReceive += OnOplReceived;
            _subscribed = true;
        }

        private static void OnOplReceived(object sender, OPLEventArgs e)
        {
            World world = World.Instance;
            if (world == null || !world.Items.TryGetValue(e.Serial, out Item item))
                return;

            _waitingForOpl.Remove(e.Serial);
            ResetItemHighlight(item);

            if (IsEligibleItem(world, item) && HasEnabledConfigs())
                Enqueue(e.Serial);
        }

        public static void ProcessItemOpl(World world, Item item)
        {
            if (world == null || item == null)
                return;

            if (!IsEligibleItem(world, item))
            {
                ResetItemHighlight(item);
                return;
            }

            if (!HasEnabledConfigs())
            {
                ResetItemHighlight(item);
                return;
            }

            if (item.HighlightChecked &&
                item.HighlightCheckedContainer == item.Container &&
                item.HighlightCheckedGraphic == item.Graphic)
                return;

            if (item.HighlightChecked)
                ResetItemHighlight(item);

            ProcessItemOpl(world, item.Serial);
        }


        public static void ProcessItemOpl(World world, uint serial)
        {
            if (world == null || !HasEnabledConfigs())
                return;

            // Only queue items if the server supports tooltips
            if (!world.ClientFeatures.TooltipsEnabled)
                return;

            if (world.OPL.TryGetNameAndData(serial, out _, out _))
            {
                _waitingForOpl.Remove(serial);
                Enqueue(serial);
            }
            else
            {
                if (AddWaitingForOpl(world, serial))
                    world.OPL.Contains(serial);
            }
        }

        private static void Enqueue(uint serial)
        {
            if (_queuedItems.Add(serial))
                _queue.Enqueue(serial);
        }

        private static bool AddWaitingForOpl(World world, uint serial)
        {
            if (_waitingForOpl.Contains(serial))
                return false;

            if (_waitingForOpl.Count >= MaxWaitingForOpl)
            {
                _waitingForOpl.RemoveWhere(waitingSerial => !world.Items.ContainsKey(waitingSerial));
                if (_waitingForOpl.Count >= MaxWaitingForOpl)
                    _waitingForOpl.Clear();
            }

            return _waitingForOpl.Add(serial);
        }

        private static bool HasEnabledConfigs() => AllConfigs.Any(config => config.Enabled);

        public static void ProcessQueue(World World)
        {
            if (World == null || _queue.Count == 0)
                return;

            _reusableItemData.Clear();

            for (int i = 0; i < 12 && _queue.Count > 0; i++)
            {
                uint ser = _queue.Dequeue();
                _queuedItems.Remove(ser);

                // Check if item still exists
                if (!World.Items.TryGetValue(ser, out Item item))
                {
                    _waitingForOpl.Remove(ser);
                    continue;
                }

                // Check if item is still valid for highlighting
                bool isEligible = IsEligibleItem(World, item);
                if (!isEligible || item.HighlightChecked)
                {
                    if (!isEligible)
                        ResetItemHighlight(item);
                    continue;
                }

                // Check if OPL data exists
                if (!World.OPL.TryGetNameAndData(ser, out _, out _))
                {
                    if (AddWaitingForOpl(World, ser))
                        World.OPL.Contains(ser);
                    continue;
                }

                _waitingForOpl.Remove(ser);
                _reusableItemData.Add(new ItemPropertiesData(World, item));
            }

            // Process items with OPL data
            foreach (ItemPropertiesData data in _reusableItemData)
            {
                data.item.HighlightChecked = true;
                data.item.HighlightCheckedContainer = data.item.Container;
                data.item.HighlightCheckedGraphic = data.item.Graphic;

                GridHighlightData[] matches = GetMatches(data);
                GridHighlightData bestMatch = matches.FirstOrDefault();
                if (bestMatch != null)
                {
                    data.item.MatchesHighlightData = true;
                    data.item.HighlightColor = bestMatch.HighlightColor;
                    data.item.HighlightColors = matches.Select(match => match.HighlightColor).ToArray();
                    data.item.HighlightName = bestMatch.Name;

                    GridHighlightData lootMatch = GetAutoLootMatch(matches);
                    if (lootMatch != null && AutoLootManager.GetContainingCorpse(World, data.item) != null)
                    {
                        data.item.ShouldAutoLoot = AutoLootManager.Instance.LootGridHighlightItem(
                            data.item,
                            lootMatch.GetLootEntry()
                        );
                    }
                }
                else
                {
                    data.item.MatchesHighlightData = false;
                    data.item.HighlightColor = Color.Transparent;
                    data.item.HighlightColors = Array.Empty<Color>();
                    data.item.HighlightName = string.Empty;
                    data.item.ShouldAutoLoot = false;
                }
            }
        }

        public static GridHighlightData GetGridHighlightData(int index)
        {
            List<GridHighlightSetupEntry> list = GridHighlightsConfig.Current.Highlights;
            GridHighlightData data = index >= 0 && index < list.Count ? new GridHighlightData(list[index]) : null;

            if (data == null)
            {
                var newEntry = new GridHighlightSetupEntry();
                newEntry.Normalize();
                list.Add(newEntry);
                allConfigs = null;
                data = new GridHighlightData(newEntry);
            }

            return data;
        }

        public static void RecheckMatchStatus()
        {
            AllConfigs = null; // Reset configs

            World world = World.Instance;
            if (world == null)
                return;

            AutoLootManager.Instance.CancelGridHighlightLoot();
            _queue.Clear();
            _queuedItems.Clear();
            _waitingForOpl.Clear();

            bool hasEnabledConfigs = HasEnabledConfigs();

            // Then re-queue all valid items for OPL processing
            foreach (KeyValuePair<uint, Item> kvp in world.Items)
            {
                Item item = kvp.Value;
                ResetItemHighlight(item);

                // Grid highlights are rendered for items inside item containers, not equipped/mobile items.
                if (hasEnabledConfigs && IsEligibleItem(world, item))
                    ProcessItemOpl(world, item);
            }
        }

        public static void ConfigurationChanged()
        {
            allConfigs = null;
            GridHighlightsConfig.Current.Save();
            RecheckMatchStatus();
        }

        private static void ResetItemHighlight(Item item)
        {
            if (item == null)
                return;

            item.MatchesHighlightData = false;
            item.HighlightName = string.Empty;
            item.HighlightColor = Color.Transparent;
            item.HighlightColors = Array.Empty<Color>();
            item.ShouldAutoLoot = false;
            item.HighlightChecked = false;
            item.HighlightCheckedContainer = 0;
            item.HighlightCheckedGraphic = 0;
        }

        internal static bool IsEligibleItem(World world, Item item)
        {
            if (world == null || item == null || item.IsMulti)
                return false;

            return SerialHelper.IsItem(item.Container) ||
                   AutoLootManager.GetContainingCorpse(world, item) != null;
        }

        public bool IsMatch(ItemPropertiesData itemData)
        {
            if (itemData == null)
                return false;

            EnsureCache();

            if (!HasSelectionCriteria() || !IsItemNameMatch(itemData.Name))
                return false;

            if (itemData.item != null && !MatchesSlot(itemData.item.ItemData.Layer))
                return false;

            List<ItemPropertiesData.SinglePropertyData> lines = itemData.singlePropertyData
                .Where(line => line != null && !string.IsNullOrWhiteSpace(Normalize(line.Name)))
                .ToList();

            if (Overweight)
            {
                if (!TryGetWeight(lines, out double weight))
                    return false;

                if ((MinimumWeight > 0 && weight < MinimumWeight) ||
                    (MaximumWeight > 0 && weight > MaximumWeight))
                    return false;
            }

            foreach (string exclusion in _cachedNormalizedRulesExcludeNegatives)
            {
                if (lines.Any(line => ContainsPhrase(Normalize(line.Name), exclusion) ||
                                      ContainsPhrase(Normalize(line.OriginalString), exclusion) ||
                                      ContainsPhrase(Normalize(line.EnglishName), exclusion) ||
                                      ContainsPhrase(Normalize(line.EnglishOriginalString), exclusion)))
                    return false;
            }

            if (_cachedNormalizedRulesRequiredRarities.Count > 0 &&
                !lines.Any(line => _cachedNormalizedRulesRequiredRarities.Any(rarity => PropertyNameMatches(line, rarity))))
                return false;

            int matchingPropertiesCount = 0;
            foreach (KeyValuePair<string, (int MinValue, bool IsOptional)> rule in _cachedNormalizedRulesProperties)
            {
                bool matched = lines.Any(line => PropertyMatches(line, rule.Key, rule.Value.MinValue));
                if (matched)
                    matchingPropertiesCount++;
                else if (!rule.Value.IsOptional)
                    return false;
            }

            if (!IsMatchingCount(matchingPropertiesCount, MinimumMatchingProperty, MaximumMatchingProperty))
                return false;

            List<ItemPropertiesData.SinglePropertyData> propertyLines = lines
                .Where(line => !IsWeightLine(line) &&
                               !_cachedNormalizedKnownRarities.Any(rarity => PropertyNameMatches(line, rarity)))
                .GroupBy(line => Normalize(line.Name), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            if (!IsMatchingCount(propertyLines.Count, MinimumProperty, MaximumProperty))
                return false;

            if (!AcceptExtraProperties)
            {
                foreach (ItemPropertiesData.SinglePropertyData line in propertyLines)
                {
                    if (!_cachedNormalizedRulesProperties.Any(rule => PropertyNameMatches(line, rule.Key)))
                        return false;
                }
            }

            return true;
        }

        public bool DoesPropertyMatch(ItemPropertiesData.SinglePropertyData property)
        {
            if (property == null)
                return false;

            EnsureCache();
            return _cachedNormalizedRulesProperties.Any(rule => PropertyMatches(property, rule.Key, rule.Value.MinValue)) ||
                   _cachedNormalizedRulesRequiredRarities.Any(rarity => PropertyNameMatches(property, rarity));
        }

        public void InvalidateCache()
        {
            _cacheValid = false;
            ConfigurationChanged();
        }

        private void EnsureCache()
        {
            if (_cacheValid) return;

            _entry.Normalize();
            _cachedNormalizedItemNames = ItemNames.Select(Normalize).Where(name => name.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            _cachedNormalizedRulesExcludeNegatives = ExcludeNegatives.Select(Normalize).Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            _cachedNormalizedRulesRequiredRarities = new HashSet<string>(
                RequiredRarities.Select(Normalize).Where(value => value.Length > 0), StringComparer.OrdinalIgnoreCase);
            _cachedNormalizedKnownRarities = new HashSet<string>(
                GridHighlightRules.RarityProperties.Select(Normalize).Concat(_cachedNormalizedRulesRequiredRarities),
                StringComparer.OrdinalIgnoreCase);
            _cachedNormalizedRulesProperties = Properties
                .Where(p => p != null && !string.IsNullOrWhiteSpace(p.Name))
                .GroupBy(p => Normalize(p.Name), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key,
                              g =>
                              {
                                  // if duplicates exist, keep the strictest (highest MinValue) and required if any non-optional
                                  int minValue = g.Max(x => x.MinValue);
                                  bool isOptional = g.All(x => x.IsOptional); // any required makes it required
                                  return (minValue, isOptional);
                              },
                              StringComparer.OrdinalIgnoreCase);

            _cacheValid = true;
        }

        public static GridHighlightData[] GetMatches(ItemPropertiesData itemData) =>
            AllConfigs.Where(config => config.Enabled && config.IsMatch(itemData)).ToArray();

        /// <summary>The first matching rule wins, making the visible Up/Down order the priority order.</summary>
        public static GridHighlightData GetBestMatch(ItemPropertiesData itemData) => GetMatches(itemData).FirstOrDefault();

        /// <summary>The first auto-loot rule in configured match order supplies the loot settings.</summary>
        internal static GridHighlightData GetAutoLootMatch(IEnumerable<GridHighlightData> matches) =>
            matches?.FirstOrDefault(match => match.LootOnMatch);

        internal bool HasSelectionCriteria()
        {
            EnsureCache();
            return _cachedNormalizedItemNames.Count > 0 ||
                   _cachedNormalizedRulesProperties.Count > 0 ||
                   _cachedNormalizedRulesExcludeNegatives.Count > 0 ||
                   _cachedNormalizedRulesRequiredRarities.Count > 0 ||
                   Overweight || MinimumProperty > 0 || MaximumProperty > 0 ||
                   MinimumMatchingProperty > 0 || MaximumMatchingProperty > 0 ||
                   HasNonDefaultSlotSelection();
        }

        private bool HasNonDefaultSlotSelection()
        {
            GridHighlightSlot slots = EquipmentSlots;
            return slots.Other || !slots.Talisman || !slots.RightHand || !slots.LeftHand ||
                   !slots.Head || !slots.Earring || !slots.Neck || !slots.Chest || !slots.Shirt ||
                   !slots.Back || !slots.Robe || !slots.Arms || !slots.Hands || !slots.Bracelet ||
                   !slots.Ring || !slots.Belt || !slots.Skirt || !slots.Legs || !slots.Footwear;
        }

        private bool PropertyMatches(ItemPropertiesData.SinglePropertyData property, string normalizedRule, int minValue) =>
            PropertyNameMatches(property, normalizedRule) &&
            (minValue == -1 || property.FirstValue.HasValue && property.FirstValue.Value >= minValue);

        private bool PropertyNameMatches(ItemPropertiesData.SinglePropertyData property, string normalizedRule)
        {
            if (property == null || string.IsNullOrEmpty(normalizedRule))
                return false;

            string name = Normalize(property.Name);
            string original = Normalize(property.OriginalString);
            string englishName = Normalize(property.EnglishName);
            string englishOriginal = Normalize(property.EnglishOriginalString);
            return name.Equals(normalizedRule, StringComparison.OrdinalIgnoreCase) ||
                   StartsWithPhrase(name, normalizedRule) ||
                   StartsWithPhrase(original, normalizedRule) ||
                   englishName.Equals(normalizedRule, StringComparison.OrdinalIgnoreCase) ||
                   StartsWithPhrase(englishName, normalizedRule) ||
                   StartsWithPhrase(englishOriginal, normalizedRule);
        }

        private static bool StartsWithPhrase(string value, string phrase) =>
            value.StartsWith(phrase, StringComparison.OrdinalIgnoreCase) &&
            (value.Length == phrase.Length || !char.IsLetterOrDigit(value[phrase.Length]));

        private static bool ContainsPhrase(string value, string phrase)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(phrase))
                return false;

            int index = value.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
            while (index >= 0)
            {
                int end = index + phrase.Length;
                bool leftBoundary = index == 0 || !char.IsLetterOrDigit(value[index - 1]);
                bool rightBoundary = end == value.Length || !char.IsLetterOrDigit(value[end]);
                if (leftBoundary && rightBoundary)
                    return true;
                index = value.IndexOf(phrase, index + 1, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private bool TryGetWeight(IEnumerable<ItemPropertiesData.SinglePropertyData> properties, out double weight)
        {
            foreach (ItemPropertiesData.SinglePropertyData property in properties)
            {
                if (IsWeightLine(property) && property.FirstValue.HasValue)
                {
                    weight = property.FirstValue.Value;
                    return true;
                }
            }

            weight = 0;
            return false;
        }

        private bool IsWeightLine(ItemPropertiesData.SinglePropertyData property)
        {
            string name = Normalize(property?.Name);
            string englishName = Normalize(property?.EnglishName);
            return WeightPropertyNames.Any(weightName =>
                StartsWithPhrase(name, weightName) || StartsWithPhrase(englishName, weightName));
        }

        private bool IsMatchingCount(int count, int minPropertyCount, int maxPropertyCount)
        {
            if (minPropertyCount > 0 && count < minPropertyCount)
            {
                return false;
            }
            if (maxPropertyCount > 0 && count > maxPropertyCount)
            {
                return false;
            }

            return true;
        }

        private string Normalize(string input)
        {
            input ??= string.Empty;

            if (_normalizeCache.TryGetValue(input, out string cached))
                return cached;

            string result = StripHtmlTags(input);
            if (_normalizeCache.Count >= 4096)
                _normalizeCache.Clear();
            _normalizeCache[input] = result;
            return result;
        }

        private string StripLeadingStackAmount(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;

            string trimmed = name.Trim();
            int index = 0;

            while (index < trimmed.Length && (char.IsDigit(trimmed[index]) ||
                   ((trimmed[index] == ',' || trimmed[index] == '.') &&
                    index > 0 && index + 1 < trimmed.Length && char.IsDigit(trimmed[index + 1]))))
                index++;

            // A leading number is a stack amount only when it is followed by whitespace.
            // This preserves real names such as "10th Anniversary Sculpture".
            if (index > 0 && index < trimmed.Length && char.IsWhiteSpace(trimmed[index]))
            {
                while (index < trimmed.Length && char.IsWhiteSpace(trimmed[index]))
                    index++;
                trimmed = trimmed.Substring(index);
            }

            return Normalize(trimmed);
        }

        private string StripHtmlTags(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            char[] output = new char[input.Length];
            int outputIndex = 0;

            for (int i = 0; i < input.Length; i++)
            {
                if (input[i] == '<')
                {
                    int end = input.IndexOf('>', i + 1);
                    if (end > i + 1 && IsLikelyHtmlTag(input, i + 1, end))
                    {
                        i = end;
                        continue;
                    }
                }

                output[outputIndex++] = input[i];
            }

            string result = CollapseWhitespace(RemoveColorCommands(new string(output, 0, outputIndex)));
            return result.ToLowerInvariant().Normalize(NormalizationForm.FormKC);
        }

        private static bool IsLikelyHtmlTag(string value, int start, int end)
        {
            while (start < end && char.IsWhiteSpace(value[start]))
                start++;
            if (start < end && value[start] == '/')
                start++;
            return start < end && (char.IsLetter(value[start]) || value[start] is '!' or '?');
        }

        private static string RemoveColorCommands(string value)
        {
            int start;
            while ((start = value.IndexOf("/c[", StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                int end = value.IndexOf(']', start + 3);
                if (end < 0)
                    break;
                value = value.Remove(start, end - start + 1);
            }

            return value.Replace("/cd", string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static string CollapseWhitespace(string value)
        {
            char[] output = new char[value.Length];
            int count = 0;
            bool pendingSpace = false;

            foreach (char character in value)
            {
                if (char.IsWhiteSpace(character))
                {
                    pendingSpace = count > 0;
                    continue;
                }

                if (pendingSpace)
                    output[count++] = ' ';
                output[count++] = character;
                pendingSpace = false;
            }

            return new string(output, 0, count);
        }

        private bool IsItemNameMatch(string itemName)
        {
            EnsureCache();
            if (_cachedNormalizedItemNames.Count == 0)
                return true;

            string normalizedItemName = Normalize(itemName);
            if (_cachedNormalizedItemNames.Contains(normalizedItemName, StringComparer.OrdinalIgnoreCase))
                return true;

            string withoutStackAmount = StripLeadingStackAmount(itemName);
            return !withoutStackAmount.Equals(normalizedItemName, StringComparison.OrdinalIgnoreCase) &&
                   _cachedNormalizedItemNames.Contains(withoutStackAmount, StringComparer.OrdinalIgnoreCase);
        }

        internal bool MatchesSlot(byte layer)
        {
            return layer switch
            {
                (byte)Layer.Talisman => EquipmentSlots.Talisman,
                (byte)Layer.OneHanded => EquipmentSlots.RightHand,
                (byte)Layer.TwoHanded => EquipmentSlots.LeftHand,
                (byte)Layer.Helmet => EquipmentSlots.Head,
                (byte)Layer.Earrings => EquipmentSlots.Earring,
                (byte)Layer.Neck => EquipmentSlots.Neck,
                (byte)Layer.Torso or (byte)Layer.Tunic => EquipmentSlots.Chest,
                (byte)Layer.Shirt => EquipmentSlots.Shirt,
                (byte)Layer.Cloak => EquipmentSlots.Back,
                (byte)Layer.Robe => EquipmentSlots.Robe,
                (byte)Layer.Arms => EquipmentSlots.Arms,
                (byte)Layer.Gloves => EquipmentSlots.Hands,
                (byte)Layer.Bracelet => EquipmentSlots.Bracelet,
                (byte)Layer.Ring => EquipmentSlots.Ring,
                (byte)Layer.Waist => EquipmentSlots.Belt,
                (byte)Layer.Skirt => EquipmentSlots.Skirt,
                (byte)Layer.Legs => EquipmentSlots.Legs,
                (byte)Layer.Pants => EquipmentSlots.Legs,
                (byte)Layer.Shoes => EquipmentSlots.Footwear,

                (byte)Layer.Hair or
                (byte)Layer.Beard or
                (byte)Layer.Face or
                (byte)Layer.Mount or
                (byte)Layer.Backpack or
                (byte)Layer.ShopBuy or
                (byte)Layer.ShopBuyRestock or
                (byte)Layer.ShopSell or
                (byte)Layer.Bank => false,

                (byte)Layer.Invalid => EquipmentSlots.Other,
                _ => EquipmentSlots.Other
            };
        }
    }
}

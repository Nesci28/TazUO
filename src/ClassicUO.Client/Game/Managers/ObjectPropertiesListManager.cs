// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps.GridHighLight;
using ClassicUO.Network;
using ClassicUO.Network.PacketHandlers.Helpers;
using ClassicUO.Utility;

namespace ClassicUO.Game.Managers
{
    public sealed class ObjectPropertiesListManager
    {
        private readonly Dictionary<uint, ItemProperty> _itemsProperties = new Dictionary<uint, ItemProperty>();
        private World _world;

        public ObjectPropertiesListManager(World world)
        {
            _world = world;
        }

        public void Add(uint serial, uint revision, string name, string data, int namecliloc, int[] clilocs = null)
        {
            if (!_itemsProperties.TryGetValue(serial, out ItemProperty prop))
            {
                prop = new ItemProperty();
                _itemsProperties[serial] = prop;
            }

            prop.Serial = serial;
            prop.Revision = revision;
            prop.Name = name;
            prop.Data = data;
            prop.NameCliloc = namecliloc;
            prop.Clilocs = clilocs;

            EventSink.InvokeOPLOnReceive(null, new OPLEventArgs(serial, name, data));

            Entity ent = _world.Get(serial);
            ent?.OPLUpdated(prop);

            if (ent is Item item)
            {
                item.OPLName = name;
                item.OPLData = data;
                ItemDatabaseManager.Instance.AddOrUpdateItem(item, _world);
            }
        }

        public bool Contains(uint serial)
        {
            if (ProfileManager.CurrentProfile != null && ProfileManager.CurrentProfile.ForceTooltipsOnOldClients)
                ForcedTooltipManager.RequestName(_world, serial);

            if (_itemsProperties.TryGetValue(serial, out ItemProperty p))
                return true; //p.Revision != 0;  <-- revision == 0 can contain the name.

            // if we don't have the OPL of this item, let's request it to the server.
            // Original client seems asking for OPL when character is not running.
            // We'll ask OPL when mouse is over an object.
            SharedStore.AddMegaCliLocRequest(serial);

            return false;
        }

        public bool IsRevisionEquals(uint serial, uint revision)
        {
            if (_itemsProperties.TryGetValue(serial, out ItemProperty prop))
            {
                return (revision & ~0x40000000) == prop.Revision || // remove the mask
                       revision == prop.Revision;                   // if mask removing didn't work, try a simple compare.
            }

            return false;
        }

        public bool TryGetRevision(uint serial, out uint revision)
        {
            if (_itemsProperties.TryGetValue(serial, out ItemProperty p))
            {
                revision = p.Revision;

                return true;
            }

            revision = 0;

            return false;
        }

        public bool TryGetNameAndData(uint serial, out string name, out string data)
        {
            if (_itemsProperties.TryGetValue(serial, out ItemProperty p))
            {
                name = p.Name;
                data = p.Data;

                return true;
            }

            name = data = null;

            return false;
        }
        public int[] GetClilocs(uint serial)
        {
            if (_itemsProperties.TryGetValue(serial, out ItemProperty p) && p.Clilocs != null)
            {
                return p.Clilocs;
            }

            return Array.Empty<int>();
        }

        public int GetNameCliloc(uint serial)
        {
            if (_itemsProperties.TryGetValue(serial, out ItemProperty p))
            {
                return p.NameCliloc;
            }

            return 0;
        }

        public ItemPropertiesData TryGetItemPropertiesData(World world, uint serial)
        {
            if (Contains(serial))
                if (world.Items.TryGetValue(serial, out Item item))
                    return new ItemPropertiesData(world, item);
            return null;
        }

        public void Remove(uint serial) => _itemsProperties.Remove(serial);

        public void Clear() => _itemsProperties.Clear();
    }

    public class ItemProperty
    {
        public bool IsEmpty => string.IsNullOrEmpty(Name) && string.IsNullOrEmpty(Data);
        public string Data;
        public string Name;
        public uint Revision;
        public uint Serial;
        public int NameCliloc;
        public int[] Clilocs;

        public string CreateData(bool extended) => string.Empty;
    }

    public class ItemPropertiesData
    {
        public readonly bool HasData = false;
        public string Name = "";
        public readonly string RawData = "";
        public readonly uint serial;
        public readonly byte ItemLayer;
        public string[] RawLines;
        public readonly Item item, itemComparedTo;
        public List<SinglePropertyData> singlePropertyData = new List<SinglePropertyData>();

        private World world;

        public ItemPropertiesData(World world, Item item, Item compareTo = null)
        {
            if (item == null)
                return;
            this.world = world;
            this.item = item;
            itemComparedTo = compareTo;

            serial = item.Serial;
            ItemLayer = item.ItemData.Layer;
            if (world.OPL.TryGetNameAndData(item.Serial, out Name, out RawData))
            {
                Name = Name.Trim();
                HasData = true;
                processData();
            }
        }

        /// <summary>
        /// Builds property data from an OPL serial and explicit equipment layer. Server-sent gumps
        /// such as Vendor Search use this without a <see cref="World.Items"/> entry; paperdoll
        /// comparisons use it to preserve the item's network equipment layer.
        /// </summary>
        public ItemPropertiesData(World world, uint serial, byte itemLayer, Item compareTo = null)
        {
            if (world == null)
                return;

            this.world = world;
            this.serial = serial;
            ItemLayer = itemLayer;
            itemComparedTo = compareTo;

            if (world.OPL.TryGetNameAndData(serial, out Name, out RawData))
            {
                Name = Name.Trim();
                HasData = true;
                processData();
            }
        }

        public ItemPropertiesData(string tooltip)
            : this(null, tooltip, 0)
        {
        }

        /// <summary>
        /// Builds comparable property data from tooltip text embedded directly in a server gump.
        /// </summary>
        public ItemPropertiesData(
            World world,
            string tooltip,
            byte itemLayer,
            Item compareTo = null
        )
        {
            if (string.IsNullOrEmpty(tooltip))
                return;
            this.world = world;
            ItemLayer = itemLayer;
            itemComparedTo = compareTo;
            if (tooltip.Contains("\n"))
            {
                Name = tooltip.Substring(0, tooltip.IndexOf("\n"));
                RawData = tooltip.Substring(tooltip.IndexOf("\n") + 1);
            }
            else
            {
                Name = tooltip;
            }
            HasData = true;
            processData();
        }

        private void processData()
        {
            string formattedData = TextBox.ConvertHtmlToFontStashSharpCommand(RawData);

            RawLines = formattedData.Split(new string[] { "\n", "<br>" }, StringSplitOptions.None);

            int[] clilocs = item != null ? world.OPL.GetClilocs(serial) : Array.Empty<int>();
            for (int i = 0; i < RawLines.Length; i++)
            {
                var property = new SinglePropertyData(RawLines[i]);

                // The first OPL cliloc is the item name; RawLines starts at the following entry.
                int clilocIndex = i + 1;
                if (clilocIndex < clilocs.Length)
                {
                    string english = Client.Game.UO.FileManager.Clilocs.GetEnglishString(clilocs[clilocIndex]);
                    if (!string.IsNullOrWhiteSpace(english))
                    {
                        var englishProperty = new SinglePropertyData(english);
                        property.EnglishName = englishProperty.Name;
                        property.EnglishOriginalString = englishProperty.OriginalString;
                    }
                }

                singlePropertyData.Add(property);
            }

            if (itemComparedTo != null)
            {
                GenComparisonData();
            }
        }

        private void GenComparisonData()
        {
            if (itemComparedTo == null) return;

            // Property diffs only need the equipped item's OPL. Avoid resolving its tile data here
            // so OPL-only candidates remain independent from the world-item lookup path.
            var itemPropertiesData = new ItemPropertiesData(world, itemComparedTo.Serial, 0);
            if (itemPropertiesData.HasData)
            {
                foreach (SinglePropertyData thisItem in singlePropertyData)
                {
                    foreach (SinglePropertyData secondItem in itemPropertiesData.singlePropertyData)
                    {
                        if (String.Equals(thisItem.Name, secondItem.Name, StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (thisItem.FirstValue.HasValue && secondItem.FirstValue.HasValue)
                            {
                                thisItem.FirstDiff = thisItem.FirstValue.Value - secondItem.FirstValue.Value;
                            }

                            if (thisItem.SecondValue.HasValue && secondItem.SecondValue.HasValue)
                            {
                                thisItem.SecondDiff = thisItem.SecondValue.Value - secondItem.SecondValue.Value;
                            }
                            break;
                        }
                    }
                }
            }
        }

        public bool GenerateComparisonTooltip(ItemPropertiesData comparedTo, out string compiledToolTip)
        {
            if (!HasData)
            {
                compiledToolTip = null;
                return false;
            }

            string finalTooltip = Name + "\n";

            foreach (SinglePropertyData thisItem in singlePropertyData)
            {
                bool foundMatch = false;
                foreach (SinglePropertyData secondItem in comparedTo.singlePropertyData)
                {
                    if (string.Equals(thisItem.Name, secondItem.Name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        foundMatch = true;
                        finalTooltip += thisItem.Name;

                        if (thisItem.FirstValue.HasValue && secondItem.FirstValue.HasValue)
                        {
                            double diff = thisItem.FirstValue.Value - secondItem.FirstValue.Value;
                            finalTooltip += $" {thisItem.FirstValue.Value}";
                            if (diff != 0)
                            {
                                finalTooltip += $"({(diff >= 0 ? "/c[green]+" : "/c[red]")} {diff}/cd)";
                            }
                        }

                        if (thisItem.SecondValue.HasValue && secondItem.SecondValue.HasValue)
                        {
                            double diff = thisItem.SecondValue.Value - secondItem.SecondValue.Value;
                            finalTooltip += $" {thisItem.SecondValue.Value}";
                            if (diff != 0)
                            {
                                finalTooltip += $"({(diff >= 0 ? "/c[green]+" : "/c[red]")}{diff}/cd)";
                            }
                        }

                        finalTooltip += "\n";
                        break;
                    }
                }
                if (!foundMatch)
                    finalTooltip += thisItem.ToString() + "\n";
            }

            compiledToolTip = finalTooltip;
            return true;
        }

        public string CompileTooltip()
        {
            string result = "";

            result += Name + "\n";
            foreach (SinglePropertyData data in singlePropertyData)
                result += $"{data.Name} [{data.FirstValue}] [{data.SecondValue}]\n";

            return result;
        }

        public class SinglePropertyData
        {
            public string OriginalString;
            public string Name = "";
            public string EnglishOriginalString = "";
            public string EnglishName = "";
            public double? FirstValue = null;
            public double? SecondValue = null;
            public double FirstDiff = 0;
            public double SecondDiff = 0;

            public SinglePropertyData(string line)
            {
                OriginalString = line;

                // Remove any color tags like /c[#...]
                string cleaned = RegexHelper.GetRegex(@"/c\[[#a-zA-Z0-9]+\]", RegexOptions.IgnoreCase).Replace(line, "").Replace("/cd", "").Trim();

                // Extract numbers
                MatchCollection matches = RegexHelper.GetRegex(@"-?\d+(?:[\.,]\d+)*").Matches(cleaned);

                if (matches.Count > 0)
                {
                    if (TryParseNumber(matches[0].Value, out double firstValue))
                        FirstValue = firstValue;

                    if (matches.Count > 1 && TryParseNumber(matches[1].Value, out double secondValue))
                        SecondValue = secondValue;
                }

                // Remove all numbers and symbols from the cleaned string to isolate the name
                Name = RegexHelper.GetRegex(@"[-+]?\d+(?:[\.,]\d+)*[%]?([- ]*\d+)?", RegexOptions.IgnoreCase).Replace(cleaned, "").Trim();

                // Fallback if something went wrong
                if (string.IsNullOrWhiteSpace(Name))
                    Name = line;
            }

            private static bool TryParseNumber(string value, out double result)
            {
                int lastComma = value.LastIndexOf(',');
                int lastDot = value.LastIndexOf('.');

                if (lastComma >= 0 && lastDot >= 0)
                {
                    char decimalSeparator = lastComma > lastDot ? ',' : '.';
                    char groupSeparator = decimalSeparator == ',' ? '.' : ',';
                    value = value.Replace(groupSeparator.ToString(), string.Empty)
                                 .Replace(decimalSeparator, '.');
                }
                else
                {
                    char separator = lastComma >= 0 ? ',' : lastDot >= 0 ? '.' : '\0';
                    if (separator != '\0')
                    {
                        string[] parts = value.Split(separator);
                        bool isGroupedInteger = parts.Length > 1 &&
                                                parts.Skip(1).All(part => part.Length == 3);
                        value = isGroupedInteger
                            ? string.Concat(parts)
                            : string.Concat(parts.Take(parts.Length - 1)) + "." + parts[^1];
                    }
                }

                return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
            }

            public override string ToString()
            {
                string output = "";

                if (Name != null)
                    output += Name;

                if (FirstValue.HasValue)
                    output += $" {FirstValue.Value}";

                if (SecondValue.HasValue)
                    output += $" {SecondValue.Value}";

                return output;
            }
        }
    }
}

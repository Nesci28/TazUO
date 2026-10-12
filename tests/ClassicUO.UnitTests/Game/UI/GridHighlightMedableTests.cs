using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Gumps.GridHighLight;
using ClassicUO.UnitTests.Fixtures;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Game.UI;

[Collection(CurrentProfileCollection.Name)]
public class GridHighlightMedableTests : IDisposable
{
    private const ushort LeatherChest = 0x13CC;
    private const ushort PlateChest = 0x1415;
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    private readonly Profile _previousProfile = ProfileManager.CurrentProfile;
    private readonly GameController _previousGame = Client.Game;
    private readonly World _previousWorld = World.Instance;
    private readonly object _previousTiles = typeof(TileDataLoader).GetField("_staticData", PrivateStatic).GetValue(null);
    private readonly object _previousRulesLoaded = typeof(GridHighlightRules).GetField("_loaded", PrivateStatic).GetValue(null);
    private readonly StaticTiles[] _tiles = new StaticTiles[ushort.MaxValue + 1];
    private readonly World _world = new();
    private uint _nextSerial = 0x40000001;

    public GridHighlightMedableTests()
    {
        SetProfile(new Profile());
        typeof(GridHighlightRules).GetField("_loaded", PrivateStatic).SetValue(null, true);

        // Supply tile layers without opening a native game window or loading client data files.
        var game = (GameController)RuntimeHelpers.GetUninitializedObject(typeof(GameController));
        GC.SuppressFinalize(game);
        var files = (UOFileManager)RuntimeHelpers.GetUninitializedObject(typeof(UOFileManager));
        typeof(UOFileManager).GetField("<TileData>k__BackingField", PrivateInstance)
            .SetValue(files, new TileDataLoader(files));
        var uo = new UltimaOnline();
        typeof(UltimaOnline).GetProperty(nameof(UltimaOnline.FileManager)).SetValue(uo, files);
        typeof(GameController).GetField("<UO>k__BackingField", PrivateInstance).SetValue(game, uo);
        typeof(Client).GetProperty(nameof(Client.Game)).SetValue(null, game);
        typeof(TileDataLoader).GetField("_staticData", PrivateStatic).SetValue(null, _tiles);
    }

    public void Dispose()
    {
        ProfileManager.CurrentProfileChanged -= typeof(World).GetMethod("OnCurrentProfileChanged", PrivateInstance)
            .CreateDelegate<EventHandler>(_world);
        typeof(World).GetProperty(nameof(World.Instance)).SetValue(null, _previousWorld);
        typeof(Client).GetProperty(nameof(Client.Game)).SetValue(null, _previousGame);
        typeof(TileDataLoader).GetField("_staticData", PrivateStatic).SetValue(null, _previousTiles);
        typeof(GridHighlightRules).GetField("_loaded", PrivateStatic).SetValue(null, _previousRulesLoaded);
        SetProfile(_previousProfile);
    }

    [Theory]
    [InlineData(true, 14, false)]
    [InlineData(true, 15, true)]
    [InlineData(true, 20, true)]
    [InlineData(false, 14, false)]
    [InlineData(false, 15, true)]
    [InlineData(false, 20, true)]
    public void MedableLrc15_RequiresBothMedabilityAndMinimumProperty(bool acceptExtra, int lrc, bool expected)
    {
        var rule = Rule(true, acceptExtra);

        rule.IsMatch(ItemData(LeatherChest, Layer.Torso, lrc)).Should().Be(expected);
        rule.IsMatch(ItemData(PlateChest, Layer.Torso, lrc)).Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NonMedableFilter_SelectsPlateAndRejectsLeather(bool acceptExtra)
    {
        var rule = Rule(false, acceptExtra);

        rule.IsMatch(ItemData(PlateChest, Layer.Torso)).Should().BeTrue();
        rule.IsMatch(ItemData(LeatherChest, Layer.Torso)).Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnyMedability_KeepsExistingPropertyMatching(bool acceptExtra)
    {
        var rule = Rule(null, acceptExtra);

        rule.IsMatch(ItemData(LeatherChest, Layer.Torso)).Should().BeTrue();
        rule.IsMatch(ItemData(PlateChest, Layer.Torso)).Should().BeTrue();
        rule.IsMatch(ItemData(LeatherChest, Layer.Torso, 14)).Should().BeFalse();
    }

    [Fact]
    public void MageArmor_MakesPlateMatchMedableInsteadOfNonMedable()
    {
        var data = ItemData(PlateChest, Layer.Torso, extraProperties: "Mage Armor");

        Rule(true).IsMatch(data).Should().BeTrue();
        Rule(false).IsMatch(data).Should().BeFalse();
    }

    [Theory]
    [InlineData(1060437)]
    [InlineData(1060482)]
    public void LocalizedMeditationProperties_AreDetectedByCliloc(int cliloc)
    {
        var data = ItemData(PlateChest, Layer.Torso, extraProperties: "Propriété localisée", clilocs: new[] { cliloc });

        Rule(true).IsMatch(data).Should().BeTrue();
        Rule(false).IsMatch(data).Should().BeFalse();
    }

    [Theory]
    [InlineData(0x2FC5, Layer.Torso, true)] // Leaf armor
    [InlineData(0x13DB, Layer.Torso, false)] // Studded leather
    [InlineData(0x1718, Layer.Helmet, true)] // Wizard hat
    [InlineData(0x1088, Layer.Neck, true)] // Beaded necklace
    [InlineData(0x0EFA, Layer.OneHanded, true)] // Spellbook
    public void StandardEquipment_UsesItsMeditationEligibility(int graphic, Layer layer, bool medable)
    {
        var data = ItemData((ushort)graphic, layer);

        Rule(medable).IsMatch(data).Should().BeTrue();
        Rule(!medable).IsMatch(data).Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UnknownArmor_MatchesOnlyWithFilterDisabled(bool medable)
    {
        var data = ItemData(0x7FFE, Layer.Torso);

        Rule(medable).IsMatch(data).Should().BeFalse();
        Rule(null).IsMatch(data).Should().BeTrue();
    }

    [Fact]
    public void GraphicsReplacement_DoesNotChangeMedability()
    {
        var data = ItemData(LeatherChest, Layer.Torso);
        _tiles[PlateChest] = new StaticTiles { Layer = (byte)Layer.Torso, Flags = TileFlag.Wearable };
        typeof(GameObject).GetField("originalGraphic", PrivateInstance).SetValue(data.item, PlateChest);

        Rule(true).IsMatch(data).Should().BeFalse();
        Rule(false).IsMatch(data).Should().BeTrue();
    }

    [Theory]
    [InlineData(Layer.OneHanded)]
    [InlineData(Layer.TwoHanded)]
    public void HeldEquipment_RequiresSpellChannelingToBeMedable(Layer layer)
    {
        var ordinary = ItemData(0x0F5E, layer);
        var channeling = ItemData(0x0F5E, layer, extraProperties: "Spell Channeling");

        Rule(true).IsMatch(ordinary).Should().BeFalse();
        Rule(false).IsMatch(ordinary).Should().BeTrue();
        Rule(true).IsMatch(channeling).Should().BeTrue();
        Rule(false).IsMatch(channeling).Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MedabilityFilter_StillRespectsEquipmentSlots(bool acceptExtra)
    {
        var rule = Rule(true, acceptExtra);
        rule.EquipmentSlots.Chest = false;

        rule.IsMatch(ItemData(LeatherChest, Layer.Torso)).Should().BeFalse();
        rule.EquipmentSlots.Chest = true;
        rule.IsMatch(ItemData(LeatherChest, Layer.Torso)).Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExplicitMedability_DoesNotMatchNonEquipmentOrUnidentifiedTooltip(bool medable)
    {
        var rule = Rule(medable);
        rule.EquipmentSlots.Other = true;

        rule.IsMatch(ItemData(0x0EED, Layer.Invalid)).Should().BeFalse();
        rule.IsMatch(new ItemPropertiesData("Unknown item\nLower Reagent Cost 15%")).Should().BeFalse();
    }

    [Fact]
    public void AnyMedability_StillMatchesRawTooltips()
    {
        Rule(null).IsMatch(new ItemPropertiesData("Unknown item\nLower Reagent Cost 15%"))
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void ConfigRoundTrip_PreservesMedabilityAndLrcRule(bool? medable)
    {
        var config = new GridHighlightsConfig
        {
            Highlights = new List<GridHighlightSetupEntry> { Entry(medable) }
        };

        string json = JsonSerializer.Serialize(config, GridHighlightsJsonContext.DefaultToUse.GridHighlightsConfig);
        var restored = JsonSerializer.Deserialize(json, GridHighlightsJsonContext.DefaultToUse.GridHighlightsConfig);

        json.Should().Contain("\"is_medable\"");
        restored.Highlights[0].IsMedable.Should().Be(medable);
        restored.Highlights[0].Properties[0].MinValue.Should().Be(15);
        new GridHighlightData(restored.Highlights[0]).IsMedable.Should().Be(medable);
    }

    [Fact]
    public void LegacyConfigWithoutMedability_DefaultsToAny()
    {
        const string json = "{\"highlights\":[{\"name\":\"Legacy LRC\",\"properties\":[{\"name\":\"Lower Reagent Cost\",\"min_value\":15}]}]}";
        var config = JsonSerializer.Deserialize(json, GridHighlightsJsonContext.DefaultToUse.GridHighlightsConfig);

        config.Highlights[0].IsMedable.Should().BeNull();
        var rule = new GridHighlightData(config.Highlights[0]);
        rule.IsMatch(ItemData(LeatherChest, Layer.Torso)).Should().BeTrue();
        rule.IsMatch(ItemData(PlateChest, Layer.Torso)).Should().BeTrue();
    }

    private ItemPropertiesData ItemData(ushort graphic, Layer layer, int lrc = 15, string extraProperties = null, int[] clilocs = null)
    {
        _tiles[graphic] = new StaticTiles { Layer = (byte)layer, Flags = TileFlag.Wearable };
        var item = new Item(_world);
        typeof(GameObject).GetField("graphic", PrivateInstance).SetValue(item, graphic);
        item.Serial = _nextSerial++;
        string properties = $"Lower Reagent Cost {lrc}%";
        if (extraProperties != null)
            properties += "\n" + extraProperties;
        _world.OPL.Add(item.Serial, 1, "Equipment", properties, 0, clilocs);
        return new ItemPropertiesData(_world, item);
    }

    private static GridHighlightData Rule(bool? medable, bool acceptExtra = true) =>
        new(Entry(medable, acceptExtra));

    private static GridHighlightSetupEntry Entry(bool? medable, bool acceptExtra = true) => new()
    {
        IsMedable = medable,
        AcceptExtraProperties = acceptExtra,
        Properties = new List<GridHighlightProperty>
        {
            new() { Name = "Lower Reagent Cost", MinValue = 15 }
        }
    };

    private static void SetProfile(Profile profile) =>
        typeof(ProfileManager).GetProperty(nameof(ProfileManager.CurrentProfile)).SetValue(null, profile);
}

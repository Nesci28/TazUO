using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ClassicUO.Common;
using ClassicUO.Game.Data;

namespace ClassicUO.Configuration;

public readonly record struct HealthDebuffDefinition(BuffIconType Type, string Name);

public static class HealthDebuffCatalog
{
    public static readonly HealthDebuffDefinition[] All =
    [
        new(BuffIconType.Poison, "Poison"),
        new(BuffIconType.MortalStrike, "Mortal wound"),
        new(BuffIconType.BloodOathCurse, "Blood oath"),
        new(BuffIconType.Bleed, "Bleed"),
        new(BuffIconType.Paralyze, "Paralyze"),
        new(BuffIconType.Curse, "Curse"),
        new(BuffIconType.MassCurse, "Mass curse"),
        new(BuffIconType.CorpseSkin, "Corpse skin"),
        new(BuffIconType.EvilOmen, "Evil omen"),
        new(BuffIconType.Mindrot, "Mind rot"),
        new(BuffIconType.PainSpike, "Pain spike"),
        new(BuffIconType.Strangle, "Strangle"),
        new(BuffIconType.SpellPlague, "Spell plague"),
        new(BuffIconType.Sleep, "Sleep"),
        new(BuffIconType.HitLowerAttack, "Hit lower attack"),
        new(BuffIconType.HitLowerDefense, "Hit lower defense"),
        new(BuffIconType.Disarm, "Disarm"),
        new(BuffIconType.Stagger, "Stagger"),
        new(BuffIconType.Onslaught, "Onslaught"),
        new(BuffIconType.SwingSpeedDebuff, "Swing speed debuff")
    ];

    public static string GetName(BuffIconType type)
    {
        foreach (HealthDebuffDefinition definition in All)
        {
            if (definition.Type == type)
                return definition.Name;
        }

        return type.ToString();
    }

    internal static List<BuffIconType> CreateDefaults() => All.Select(definition => definition.Type).ToList();
}

public sealed class HealthNotificationsConfig : JsonSave<HealthNotificationsConfig>
{
    private const string HealthNotificationsFileName = "health_notifications.json";
    private const string DefaultLowHealthAnnouncement = "Low health: {health}% ({hits}/{maxhits})";
    private const string DefaultDebuffAnnouncement = "Debuff: {debuff}";

    public bool LowHealthEnabled { get; set; } = true;

    public int LowHealthPercentage { get; set; } = 30;

    public string LowHealthAnnouncement { get; set; } = DefaultLowHealthAnnouncement;

    public bool DebuffsEnabled { get; set; } = true;

    public List<BuffIconType> Debuffs { get; set; } = HealthDebuffCatalog.CreateDefaults();

    public string DebuffAnnouncement { get; set; } = DefaultDebuffAnnouncement;

    [JsonConverter(typeof(JsonStringEnumConverter<BackpackNotificationDestination>))]
    public BackpackNotificationDestination Destination { get; set; } = BackpackNotificationDestination.OnScreen;

    public ushort Hue { get; set; } = 32;

    public string OnScreenFont { get; set; } = "avadonian";

    public int OnScreenFontSize { get; set; } = 20;

    protected override SettingsScope Scope => SettingsScope.Char;

    protected override string FileName => HealthNotificationsFileName;

    protected override JsonTypeInfo<HealthNotificationsConfig> TypeInfo =>
        HealthNotificationsJsonContext.DefaultToUse.HealthNotificationsConfig;

    private static HealthNotificationsConfig _current;

    public static HealthNotificationsConfig Current => _current ??= LoadForCurrentProfile();

    public static void LoadForProfile(string profilePath)
    {
        string file = string.IsNullOrEmpty(profilePath) ? null : Path.Combine(profilePath, HealthNotificationsFileName);
        bool shouldSave = false;

        if (file != null && File.Exists(file))
        {
            _current = Load();
        }
        else
        {
            _current = new HealthNotificationsConfig();
            shouldSave = true;
        }

        shouldSave |= _current.Normalize();

        if (shouldSave)
            _current.Save();
    }

    public static void Unload()
    {
        if (_current == null)
            return;

        _current.Normalize();
        _current.Save();
        _current = null;
    }

    public bool IsDebuffEnabled(BuffIconType type) => Debuffs?.Contains(type) == true;

    public void SetDebuffEnabled(BuffIconType type, bool enabled)
    {
        Debuffs ??= [];

        if (enabled)
        {
            if (!Debuffs.Contains(type))
                Debuffs.Add(type);
        }
        else
        {
            Debuffs.RemoveAll(value => value == type);
        }

        Save();
    }

    private static HealthNotificationsConfig LoadForCurrentProfile()
    {
        LoadForProfile(ProfileManager.ProfilePath);
        return _current;
    }

    private bool Normalize()
    {
        bool changed = false;
        int percentage = Math.Clamp(LowHealthPercentage, 1, 100);
        int fontSize = Math.Clamp(OnScreenFontSize, 5, 50);

        if (LowHealthPercentage != percentage)
        {
            LowHealthPercentage = percentage;
            changed = true;
        }

        if (OnScreenFontSize != fontSize)
        {
            OnScreenFontSize = fontSize;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(OnScreenFont))
        {
            OnScreenFont = "avadonian";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(LowHealthAnnouncement))
        {
            LowHealthAnnouncement = DefaultLowHealthAnnouncement;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(DebuffAnnouncement))
        {
            DebuffAnnouncement = DefaultDebuffAnnouncement;
            changed = true;
        }

        if (Debuffs == null)
        {
            Debuffs = HealthDebuffCatalog.CreateDefaults();
            changed = true;
        }
        else
        {
            List<BuffIconType> distinct = Debuffs.Distinct().ToList();

            if (distinct.Count != Debuffs.Count)
            {
                Debuffs = distinct;
                changed = true;
            }
        }

        return changed;
    }
}

[JsonSerializable(typeof(HealthNotificationsConfig), GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(List<BuffIconType>), GenerationMode = JsonSourceGenerationMode.Metadata)]
sealed partial class HealthNotificationsJsonContext : JsonSerializerContext
{
    sealed class SnakeCaseNamingPolicy : JsonNamingPolicy
    {
        public static SnakeCaseNamingPolicy Instance { get; } = new();

        public override string ConvertName(string name) =>
            string.Concat(name.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString())).ToLower();
    }

    private static readonly Lazy<JsonSerializerOptions> _jsonOptions = new(() =>
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = SnakeCaseNamingPolicy.Instance
        };
        options.Converters.Add(new JsonStringEnumConverter<BuffIconType>());
        return options;
    });

    public static HealthNotificationsJsonContext DefaultToUse { get; } = new(_jsonOptions.Value);
}

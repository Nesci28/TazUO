using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ClassicUO.Common;

namespace ClassicUO.Configuration;

public enum BackpackNotificationDestination
{
    Journal,
    Overhead,
    OnScreen
}

public sealed class BackpackNotificationConfigEntry
{
    public bool Enabled { get; set; } = true;

    public string Name { get; set; } = "Artifact";

    /// <summary>Item graphic to match. -1 matches any graphic.</summary>
    public int Graphic { get; set; } = -1;

    /// <summary>Item hues to match. -1 matches any hue. Multiple hues can be comma, semicolon, or space separated.</summary>
    public string Hues { get; set; } = "-1";

    [JsonConverter(typeof(RawStringConverter))]
    public string RegexSearch { get; set; } = @"(?i)\bArtifact Rarity\b";

    public string Announcement { get; set; } = "{rule} received: {item}";

    [JsonConverter(typeof(JsonStringEnumConverter<BackpackNotificationDestination>))]
    public BackpackNotificationDestination Destination { get; set; } = BackpackNotificationDestination.Journal;

    public ushort Hue { get; set; } = 63;

    public string OnScreenFont { get; set; } = "avadonian";

    public int OnScreenFontSize { get; set; } = 20;

    [JsonPropertyName("journal")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LegacyJournal { get; set; }

    [JsonPropertyName("journal_hue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ushort? LegacyJournalHue { get; set; }

    [JsonPropertyName("overhead")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LegacyOverhead { get; set; }

    [JsonPropertyName("overhead_hue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ushort? LegacyOverheadHue { get; set; }

    [JsonPropertyName("on_screen")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? LegacyOnScreen { get; set; }

    [JsonPropertyName("on_screen_hue")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ushort? LegacyOnScreenHue { get; set; }

    internal bool MigrateLegacyDestination()
    {
        if (
            LegacyJournal == null
            && LegacyJournalHue == null
            && LegacyOverhead == null
            && LegacyOverheadHue == null
            && LegacyOnScreen == null
            && LegacyOnScreenHue == null
        )
            return false;

        if (LegacyOnScreen == true)
        {
            Destination = BackpackNotificationDestination.OnScreen;
            Hue = LegacyOnScreenHue ?? Hue;
        }
        else if (LegacyOverhead == true)
        {
            Destination = BackpackNotificationDestination.Overhead;
            Hue = LegacyOverheadHue ?? Hue;
        }
        else
        {
            Destination = BackpackNotificationDestination.Journal;
            Hue = LegacyJournalHue ?? Hue;
        }

        LegacyJournal = null;
        LegacyJournalHue = null;
        LegacyOverhead = null;
        LegacyOverheadHue = null;
        LegacyOnScreen = null;
        LegacyOnScreenHue = null;
        return true;
    }
}

public sealed class BackpackNotificationsConfig : JsonSave<BackpackNotificationsConfig>, INotifyPropertyChanged
{
    private const string BackpackNotificationsFileName = "backpack_notifications.json";

    public List<BackpackNotificationConfigEntry> Rules { get; set; } = CreateDefaultRules();

    protected override SettingsScope Scope => SettingsScope.Char;

    protected override string FileName => BackpackNotificationsFileName;

    protected override JsonTypeInfo<BackpackNotificationsConfig> TypeInfo =>
        BackpackNotificationsJsonContext.DefaultToUse.BackpackNotificationsConfig;

    private static BackpackNotificationsConfig _current;

    public static BackpackNotificationsConfig Current => _current ??= LoadForCurrentProfile();

    public static void LoadForProfile(string profilePath)
    {
        string file = string.IsNullOrEmpty(profilePath) ? null : Path.Combine(profilePath, BackpackNotificationsFileName);
        bool shouldSave = false;

        if (file != null && File.Exists(file))
        {
            _current = Load();
        }
        else
        {
            _current = new BackpackNotificationsConfig();
            shouldSave = true;
        }

        if (_current.Rules == null || _current.Rules.Count == 0)
        {
            _current.Rules = CreateDefaultRules();
            shouldSave = true;
        }

        foreach (BackpackNotificationConfigEntry rule in _current.Rules)
            shouldSave |= rule.MigrateLegacyDestination();

        if (shouldSave)
            _current.Save();
    }

    public static void Unload()
    {
        if (_current == null)
            return;

        _current.Save();
        _current = null;
    }

    public void Upsert(int index, BackpackNotificationConfigEntry entry, bool createIfMissing)
    {
        if (index >= 0 && index < Rules.Count)
            Rules[index] = entry;
        else if (createIfMissing)
            Rules.Add(entry);
        else
            return;

        Save();
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= Rules.Count)
            return;

        Rules.RemoveAt(index);
        Save();
    }

    private static BackpackNotificationsConfig LoadForCurrentProfile()
    {
        LoadForProfile(ProfileManager.ProfilePath);
        return _current;
    }

    private static List<BackpackNotificationConfigEntry> CreateDefaultRules()
    {
        return
        [
            new BackpackNotificationConfigEntry
            {
                Name = "Artifact",
                Graphic = -1,
                Hues = "-1",
                RegexSearch = @"(?i)\bArtifact Rarity\b",
                Announcement = "{rule} received: {item}",
                Destination = BackpackNotificationDestination.OnScreen,
                Hue = 63,
                OnScreenFont = "avadonian",
                OnScreenFontSize = 20
            }
        ];
    }
}

[JsonSerializable(typeof(BackpackNotificationsConfig), GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(BackpackNotificationConfigEntry), GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(List<BackpackNotificationConfigEntry>), GenerationMode = JsonSourceGenerationMode.Metadata)]
sealed partial class BackpackNotificationsJsonContext : JsonSerializerContext
{
    sealed class SnakeCaseNamingPolicy : JsonNamingPolicy
    {
        public static SnakeCaseNamingPolicy Instance { get; } = new();

        public override string ConvertName(string name) =>
            string.Concat(name.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString())).ToLower();
    }

    private static readonly System.Lazy<JsonSerializerOptions> _jsonOptions = new(() =>
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = SnakeCaseNamingPolicy.Instance
        };

        return options;
    });

    public static BackpackNotificationsJsonContext DefaultToUse { get; } = new(_jsonOptions.Value);
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ClassicUO.Common;

namespace ClassicUO.Configuration;

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

    public bool Journal { get; set; } = true;

    public ushort JournalHue { get; set; } = 63;

    public bool Overhead { get; set; }

    public ushort OverheadHue { get; set; } = 63;

    public bool OnScreen { get; set; } = true;

    public ushort OnScreenHue { get; set; } = 63;
}

public sealed class BackpackNotificationsConfig : JsonSave<BackpackNotificationsConfig>
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
                Journal = true,
                JournalHue = 63,
                Overhead = false,
                OverheadHue = 63,
                OnScreen = true,
                OnScreenHue = 63
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

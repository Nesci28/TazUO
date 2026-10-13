using System.Text.Json.Serialization;

namespace ClassicUO.Configuration;

/// <summary>Notification appearance saved with its grid highlight rule.</summary>
public sealed class GridHighlightNotificationSettings
{
    [JsonConverter(typeof(JsonStringEnumConverter<BackpackNotificationDestination>))]
    public BackpackNotificationDestination Destination { get; set; } = BackpackNotificationDestination.OnScreen;

    public ushort Hue { get; set; } = 63;

    public string OnScreenFont { get; set; } = "avadonian";

    public int OnScreenFontSize { get; set; } = 20;

    public string Announcement { get; set; } = "{rule} matched: {item}";
}

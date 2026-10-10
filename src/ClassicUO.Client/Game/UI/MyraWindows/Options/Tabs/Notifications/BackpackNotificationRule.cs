using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ClassicUO.Configuration;
using ClassicUO.Game.UI.MyraWindows.Options.Editors.Rulebase;

namespace ClassicUO.Game.UI.MyraWindows.Options.Tabs.Notifications;

public sealed class BackpackNotificationRule : IRule, INotifyPropertyChanged
{
    public uint Order { get; set => SetField(ref field, value); }

    public bool Enabled { get; set => SetField(ref field, value); } = true;

    public bool CanEdit { get; set => SetField(ref field, value); } = true;

    public bool CanDelete { get; set => SetField(ref field, value); } = true;

    public string Name { get; set => SetField(ref field, value); } = "Artifact";

    public int Graphic { get; set => SetField(ref field, value); } = -1;

    public string Hues { get; set => SetField(ref field, value); } = "-1";

    public string RegexSearch { get; set => SetField(ref field, value); } = string.Empty;

    public string Announcement { get; set => SetField(ref field, value); } = "{rule} received: {item}";

    public BackpackNotificationDestination Destination { get; set => SetField(ref field, value); } = BackpackNotificationDestination.OnScreen;

    public ushort Hue { get; set => SetField(ref field, value); } = 63;

    public string OnScreenFont { get; set => SetField(ref field, value); } = "avadonian";

    public int OnScreenFontSize { get; set => SetField(ref field, value); } = 20;

    public static BackpackNotificationRule FromEntry(uint order, BackpackNotificationConfigEntry entry)
    {
        return new BackpackNotificationRule
        {
            Order = order,
            Enabled = entry.Enabled,
            Name = entry.Name,
            Graphic = entry.Graphic,
            Hues = entry.Hues,
            RegexSearch = entry.RegexSearch,
            Announcement = entry.Announcement,
            Destination = entry.Destination,
            Hue = entry.Hue,
            OnScreenFont = entry.OnScreenFont,
            OnScreenFontSize = entry.OnScreenFontSize
        };
    }

    public BackpackNotificationConfigEntry ToEntry()
    {
        return new BackpackNotificationConfigEntry
        {
            Enabled = Enabled,
            Name = Name,
            Graphic = Graphic,
            Hues = Hues,
            RegexSearch = RegexSearch,
            Announcement = Announcement,
            Destination = Destination,
            Hue = Hue,
            OnScreenFont = OnScreenFont,
            OnScreenFontSize = OnScreenFontSize
        };
    }

    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        OnPropertyChanged(propertyName);
    }
}

using System.IO;
using System.Net;
using System.Net.Sockets;
using DaikinDesktopController.App.Persistence;
namespace DaikinDesktopController.App.Preferences;
public sealed class JsonPreferencesStore(string path) : IPreferencesStore
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DaikinDesktopController", "preferences.json");
    public static bool IsIpv4(string? text) => text is not null && text.Split('.') is var parts && parts.Length == 4
        && parts.All(part => part.Length is >= 1 and <= 3 && part.All(char.IsAsciiDigit))
        && IPAddress.TryParse(text, out var address) && address.AddressFamily == AddressFamily.InterNetwork;
    public AppPreferences Load()
    {
        var preferences = JsonFile.Read<AppPreferences>(path);
        if (preferences is null || !Enum.IsDefined(preferences.Theme)) return new();
        return preferences with { ManualIpAddress = IsIpv4(preferences.ManualIpAddress) ? preferences.ManualIpAddress : null,
            StatusRefreshIntervalSeconds = AppPreferences.StatusRefreshIntervals.Contains(preferences.StatusRefreshIntervalSeconds) ? preferences.StatusRefreshIntervalSeconds : 10 };
    }
    public void Save(AppPreferences preferences) => JsonFile.Write(path, preferences);
}

namespace DaikinDesktopController.App.Preferences;
public sealed record AppPreferences(string? Language = null, ThemePreference Theme = ThemePreference.System, bool LaunchAtSignIn = true, string? ManualIpAddress = null, int StatusRefreshIntervalSeconds = 10, string? LocationName = null)
{
    public static string? NormalizeLocationName(string? value)
    {
        var name = value?.Trim();
        return string.IsNullOrEmpty(name) ? null : name[..Math.Min(name.Length, 40)];
    }
    public static IReadOnlyList<int> StatusRefreshIntervals { get; } = Array.AsReadOnly(new[] { 5, 10, 30, 60 });
}

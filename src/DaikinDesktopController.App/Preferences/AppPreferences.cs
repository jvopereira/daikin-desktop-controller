namespace DaikinDesktopController.App.Preferences;
public sealed record AppPreferences(string? Language = null, ThemePreference Theme = ThemePreference.System, bool LaunchAtSignIn = true);

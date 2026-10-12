using System.Windows;
namespace DaikinDesktopController.App.Preferences;
public static class ThemeApplier
{
    public static void Apply(ThemePreference preference) => Application.Current.ThemeMode = preference switch
    {
        ThemePreference.Light => ThemeMode.Light,
        ThemePreference.Dark => ThemeMode.Dark,
        _ => ThemeMode.System
    };
}

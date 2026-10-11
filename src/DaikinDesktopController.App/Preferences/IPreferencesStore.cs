namespace DaikinDesktopController.App.Preferences;
public interface IPreferencesStore
{
    AppPreferences Load();
    void Save(AppPreferences preferences);
}

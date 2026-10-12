using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace DaikinDesktopController.App.Preferences;
public sealed class JsonPreferencesStore(string path) : IPreferencesStore
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DaikinDesktopController", "preferences.json");
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    public AppPreferences Load()
    {
        try
        {
            var preferences = JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(path), Options);
            return preferences is not null && Enum.IsDefined(preferences.Theme) ? preferences : new AppPreferences();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException or JsonException)
        { return new AppPreferences(); }
    }
    public void Save(AppPreferences preferences)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(preferences, Options));
        File.Move(temporaryPath, path, overwrite: true);
    }
}

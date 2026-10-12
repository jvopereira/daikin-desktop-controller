using System.IO;
using DaikinDesktopController.App.Persistence;
using DaikinDesktopController.App.Preferences;
using DaikinDesktopController.App.Secrets;
namespace DaikinDesktopController.App.Configuration;
public sealed class JsonAirConditionerConfigurationStore(string path, ISecretStore secrets) : IAirConditionerConfigurationStore
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DaikinDesktopController", "air-conditioner.json");
    public bool HasConfiguredAirConditioner => Load() is not null && secrets.Read() is not null;
    public AirConditionerConfiguration? Load()
    {
        var value = JsonFile.Read<AirConditionerConfiguration>(path);
        return value is not null && JsonPreferencesStore.IsIpv4(value.IpAddress) ? value : null;
    }
    public void Save(AirConditionerConfiguration configuration)
    {
        if (!JsonPreferencesStore.IsIpv4(configuration.IpAddress)) throw new ArgumentException("Invalid address.");
        JsonFile.Write(path, configuration);
    }
    public void Clear() => File.Delete(path);
}

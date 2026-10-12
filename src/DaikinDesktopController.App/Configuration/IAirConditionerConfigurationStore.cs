namespace DaikinDesktopController.App.Configuration;
public sealed record AirConditionerConfiguration(string? Id, string IpAddress);
public interface IAirConditionerConfigurationStore
{
    bool HasConfiguredAirConditioner { get; }
    AirConditionerConfiguration? Load();
    void Save(AirConditionerConfiguration configuration);
    void Clear();
}
public static class StartupDecision
{
    public static bool ShouldOpenPanel(IAirConditionerConfigurationStore store) => !store.HasConfiguredAirConditioner;
}

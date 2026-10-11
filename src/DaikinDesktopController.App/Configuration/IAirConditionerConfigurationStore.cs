namespace DaikinDesktopController.App.Configuration;
public interface IAirConditionerConfigurationStore { bool HasConfiguredAirConditioner { get; } }
public sealed class NotConfiguredStore : IAirConditionerConfigurationStore
{
    public bool HasConfiguredAirConditioner => false;
}
public static class StartupDecision
{
    public static bool ShouldOpenPanel(IAirConditionerConfigurationStore store) => !store.HasConfiguredAirConditioner;
}

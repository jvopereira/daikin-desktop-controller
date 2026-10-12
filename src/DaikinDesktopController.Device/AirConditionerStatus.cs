namespace DaikinDesktopController.Device;
public sealed record AirConditionerStatus(bool IsOn, string Mode, double TargetTemperature, double? RoomTemperature, string? FirmwareVersion)
{
    public TargetTemperatureRange? TargetTemperatureRange => global::DaikinDesktopController.Device.TargetTemperatureRange.ForMode(Mode);
}

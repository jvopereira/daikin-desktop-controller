namespace DaikinDesktopController.Device;
public sealed record AirConditionerStatus(bool IsOn, string Mode, double TargetTemperature, double? RoomTemperature, string? FirmwareVersion, string? Fan = null)
{
    public OperatingMode? OperatingMode => OperatingModeMapper.FromCode(Mode);
    public FanSpeed? FanSpeed => Fan is null ? null : FanSpeedMapper.FromCode(Fan);
    public TargetTemperatureRange? TargetTemperatureRange => global::DaikinDesktopController.Device.TargetTemperatureRange.ForMode(Mode);
}

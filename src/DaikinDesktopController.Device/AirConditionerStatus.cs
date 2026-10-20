namespace DaikinDesktopController.Device;
public sealed record AirConditionerStatus(bool IsOn, string Mode, double TargetTemperature, double? RoomTemperature, string? FirmwareVersion, string? Fan = null, bool? TurboMode = null, bool? ComfortMode = null, bool? EconomyMode = null, bool? Swing = null)
{
    public AirConditionerStatus WithOperatingMode(OperatingMode mode) => this with
    { Mode = OperatingModeMapper.ToCode(mode).ToString(System.Globalization.CultureInfo.InvariantCulture) };
    public OperatingMode? OperatingMode => OperatingModeMapper.FromCode(Mode);
    public FanSpeed? FanSpeed => Fan is null ? null : FanSpeedMapper.FromCode(Fan);
    public TargetTemperatureRange? TargetTemperatureRange => global::DaikinDesktopController.Device.TargetTemperatureRange.ForMode(Mode);
}

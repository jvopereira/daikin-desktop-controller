namespace DaikinDesktopController.Device;
public sealed record AirConditionerStatus(bool IsOn, string Mode, double TargetTemperature, double? RoomTemperature, string? FirmwareVersion);

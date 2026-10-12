namespace DaikinDesktopController.Device;
public enum FanSpeed { Auto, Quiet, Low, MediumLow, Medium, MediumHigh, High }
internal static class FanSpeedMapper
{
    public static FanSpeed? FromCode(string code) => code switch
    {
        "17" => FanSpeed.Auto,
        "18" => FanSpeed.Quiet,
        "3" => FanSpeed.Low,
        "4" => FanSpeed.MediumLow,
        "5" => FanSpeed.Medium,
        "6" => FanSpeed.MediumHigh,
        "7" => FanSpeed.High,
        _ => null
    };
    public static int ToCode(FanSpeed value) => value switch
    {
        FanSpeed.Auto => 17,
        FanSpeed.Quiet => 18,
        FanSpeed.Low => 3,
        FanSpeed.MediumLow => 4,
        FanSpeed.Medium => 5,
        FanSpeed.MediumHigh => 6,
        FanSpeed.High => 7,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}

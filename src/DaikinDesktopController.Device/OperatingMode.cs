namespace DaikinDesktopController.Device;
public enum OperatingMode { Auto, Cool, Heat, Dry, Fan }
internal static class OperatingModeMapper
{
    public static OperatingMode? FromCode(string code) => code switch
    {
        "0" => OperatingMode.Auto,
        "1" => OperatingMode.Auto,
        "3" => OperatingMode.Cool,
        "4" => OperatingMode.Heat,
        "2" => OperatingMode.Dry,
        "6" => OperatingMode.Fan,
        _ => null
    };
    public static int ToCode(OperatingMode value) => value switch
    {
        OperatingMode.Auto => 1,
        OperatingMode.Cool => 3,
        OperatingMode.Heat => 4,
        OperatingMode.Dry => 2,
        OperatingMode.Fan => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}

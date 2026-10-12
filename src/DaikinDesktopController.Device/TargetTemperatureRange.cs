namespace DaikinDesktopController.Device;
public sealed record TargetTemperatureRange(double Min, double Max, double Step)
{
    // Vendor capabilities: https://github.com/daikin-br/ha-custom-integration
    // The device reports mode but no temperature limits.
    public static TargetTemperatureRange? ForMode(string mode) => mode switch
    {
        "3" => new(18, 32, 1), "0" or "1" => new(18, 30, 1), "4" => new(10, 30, 1), _ => null
    };
}

using System.Text.Json;
namespace DaikinDesktopController.Device;
public abstract record AirConditionerCommand
{
    public sealed record SetPower(bool IsOn) : AirConditionerCommand;
    public sealed record SetTargetTemperature(double Celsius) : AirConditionerCommand;
    public sealed record SetOperatingMode(OperatingMode Mode) : AirConditionerCommand;
    public sealed record SetFanSpeed(FanSpeed Speed) : AirConditionerCommand;
    public bool IsSatisfiedBy(AirConditionerStatus status) => this switch
    {
        SetPower power => status.IsOn == power.IsOn,
        SetOperatingMode mode => status.OperatingMode == mode.Mode,
        SetFanSpeed fan => status.FanSpeed == fan.Speed,
        SetTargetTemperature temperature => status.TargetTemperature == temperature.Celsius,
        _ => false
    };
    public bool IsSupportedBy(AirConditionerStatus status) => this switch
    {
        SetPower => true,
        SetOperatingMode mode => Enum.IsDefined(mode.Mode),
        SetFanSpeed fan => Enum.IsDefined(fan.Speed) && status.FanSpeed is not null
            && status.OperatingMode is OperatingMode.Auto or OperatingMode.Cool or OperatingMode.Heat or OperatingMode.Fan,
        SetTargetTemperature temperature => status.TargetTemperatureRange is { } range
            && double.IsFinite(temperature.Celsius) && temperature.Celsius >= range.Min && temperature.Celsius <= range.Max
            && (temperature.Celsius - range.Min) % range.Step == 0,
        _ => false
    };
    internal byte[] ToPlaintext()
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject(); writer.WriteStartObject("port1");
            switch (this)
            {
                case SetOperatingMode mode:
                    writer.WriteNumber("mode", OperatingModeMapper.ToCode(mode.Mode));
                    if (mode.Mode == OperatingMode.Dry) writer.WriteNumber("fan", 17);
                    break;
                case SetFanSpeed fan: writer.WriteNumber("fan", FanSpeedMapper.ToCode(fan.Speed)); break;
                case SetPower power: writer.WriteNumber("power", power.IsOn ? 1 : 0); break;
                case SetTargetTemperature temperature: writer.WriteNumber("temperature", checked((int)temperature.Celsius)); break;
            }
            writer.WriteEndObject(); writer.WriteEndObject();
        }
        buffer.Write("BZ"u8);
        return buffer.ToArray();
    }
}

using System.Text.Json;
namespace DaikinDesktopController.Device;
public abstract record AirConditionerCommand
{
    public sealed record SetPower(bool IsOn) : AirConditionerCommand;
    public sealed record SetTargetTemperature(double Celsius) : AirConditionerCommand;
    public sealed record SetOperatingMode(OperatingMode Mode) : AirConditionerCommand;
    public sealed record SetFanSpeed(FanSpeed Speed) : AirConditionerCommand;
    public sealed record SetTurboMode(bool IsOn) : AirConditionerCommand;
    public sealed record SetComfortMode(bool IsOn) : AirConditionerCommand;
    public sealed record SetEconomyMode(bool IsOn) : AirConditionerCommand;
    public sealed record SetSwing(bool IsOn) : AirConditionerCommand;
    public bool IsSatisfiedBy(AirConditionerStatus status) => this switch
    {
        SetSwing flag => status.Swing == flag.IsOn,
        SetTurboMode flag => status.TurboMode == flag.IsOn,
        SetComfortMode flag => status.ComfortMode == flag.IsOn,
        SetEconomyMode flag => status.EconomyMode == flag.IsOn,
        SetPower power => status.IsOn == power.IsOn,
        SetOperatingMode mode => status.OperatingMode == mode.Mode,
        SetFanSpeed fan => status.FanSpeed == fan.Speed,
        SetTargetTemperature temperature => status.TargetTemperature == temperature.Celsius,
        _ => false
    };
    public bool IsSupportedBy(AirConditionerStatus status) => this switch
    {
        SetSwing => status.Swing is not null,
        SetTurboMode => status.TurboMode is not null,
        SetComfortMode => status.ComfortMode is not null,
        SetEconomyMode => status.EconomyMode is not null,
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
                case SetSwing flag:
                    writer.WriteNumber("v_swing", flag.IsOn ? 1 : 0);
                    break;
                case SetTurboMode flag:
                    writer.WriteNumber("powerchill", flag.IsOn ? 1 : 0);
                    if (flag.IsOn) writer.WriteNumber("econo", 0);
                    break;
                case SetComfortMode flag:
                    writer.WriteNumber("coanda", flag.IsOn ? 1 : 0);
                    break;
                case SetEconomyMode flag:
                    writer.WriteNumber("econo", flag.IsOn ? 1 : 0);
                    if (flag.IsOn) writer.WriteNumber("powerchill", 0);
                    break;
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

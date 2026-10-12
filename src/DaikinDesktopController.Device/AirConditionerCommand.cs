using System.Text.Json;
namespace DaikinDesktopController.Device;
public abstract record AirConditionerCommand
{
    public sealed record SetPower(bool IsOn) : AirConditionerCommand;
    public sealed record SetTargetTemperature(double Celsius) : AirConditionerCommand;
    public bool IsSatisfiedBy(AirConditionerStatus status) => this switch
    {
        SetPower power => status.IsOn == power.IsOn,
        SetTargetTemperature temperature => status.TargetTemperature == temperature.Celsius,
        _ => false
    };
    public bool IsSupportedBy(AirConditionerStatus status) => this switch
    {
        SetPower => true,
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
                case SetPower power: writer.WriteNumber("power", power.IsOn ? 1 : 0); break;
                case SetTargetTemperature temperature: writer.WriteNumber("temperature", checked((int)temperature.Celsius)); break;
            }
            writer.WriteEndObject(); writer.WriteEndObject();
        }
        buffer.Write("BZ"u8);
        return buffer.ToArray();
    }
}

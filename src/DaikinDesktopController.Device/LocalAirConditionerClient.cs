using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DaikinDesktopController.Device.LocalProtocol;
namespace DaikinDesktopController.Device;
public interface IAirConditionerClient
{
    Task<CommandResult> SendCommandAsync(IPAddress address, DeviceSecretKey key, AirConditionerCommand command, CancellationToken ct = default);
    Task<StatusRead> ReadStatusAsync(IPAddress address, DeviceSecretKey key, CancellationToken ct = default);
}
public sealed class LocalAirConditionerClient : IAirConditionerClient
{
    public const int ControlPort = 15914;
    private readonly int port;
    private readonly TimeSpan timeout;
    public LocalAirConditionerClient() : this(ControlPort, TimeSpan.FromSeconds(5)) { }
    internal LocalAirConditionerClient(int port, TimeSpan timeout) { this.port = port; this.timeout = timeout; }
    public async Task<StatusRead> ReadStatusAsync(IPAddress address, DeviceSecretKey key, CancellationToken ct = default)
    {
        try
        {
            var response = await RawHttp.GetAsync(new IPEndPoint(address, port), "/acstatus", timeout, ct).ConfigureAwait(false);
            if (response.StatusCode != 200) return new(StatusReadOutcome.UnexpectedResponse);
            var packet = ResponsePacket.Decode(response.Body, key);
            if (packet.Failure is not null) return new(StatusReadOutcome.UnexpectedResponse);
            return ParseStatus(packet);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(StatusReadOutcome.Unreachable); }
        catch (Exception e) when (e is SocketException or IOException) { return new(StatusReadOutcome.Unreachable); }
        catch (InvalidResponseException) { return new(StatusReadOutcome.UnexpectedResponse); }
    }
    public async Task<CommandResult> SendCommandAsync(IPAddress address, DeviceSecretKey key, AirConditionerCommand command, CancellationToken ct = default)
    {
        var plaintext = command.ToPlaintext();
        string body;
        try { body = RequestPacket.Encode(plaintext, key); }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
        try
        {
            var response = await RawHttp.SendAsync(new IPEndPoint(address, port), "POST", "/acstatus", body, timeout, ct).ConfigureAwait(false);
            if (response.StatusCode != 200) return new(CommandOutcome.UnexpectedResponse);
            var packet = ResponsePacket.Decode(response.Body, key);
            if (packet.Failure is not null) return new(CommandOutcome.UnexpectedResponse);
            var read = ParseStatus(packet);
            return read.Status is { } status
                ? new(command.IsSatisfiedBy(status) ? CommandOutcome.Confirmed : CommandOutcome.NotApplied, status)
                : new(CommandOutcome.UnexpectedResponse);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return new(CommandOutcome.NoResponse); }
        catch (Exception e) when (e is SocketException or IOException) { return new(CommandOutcome.NoResponse); }
        catch (InvalidResponseException) { return new(CommandOutcome.UnexpectedResponse); }
    }
    private static StatusRead ParseStatus(PacketDecode packet)
    {
            try
            {
                var json = new UTF8Encoding(false, true).GetString(packet.Plaintext!);
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("port1", out var unit)
                    || unit.ValueKind != JsonValueKind.Object) return new(StatusReadOutcome.KeyRejected);
                if (!unit.TryGetProperty("power", out var power) || !unit.TryGetProperty("mode", out var mode)
                    || !unit.TryGetProperty("temperature", out var temperature) || !temperature.TryGetDouble(out var target))
                    return new(StatusReadOutcome.UnexpectedResponse);
                bool isOn;
                if (power.ValueKind is JsonValueKind.True or JsonValueKind.False) isOn = power.GetBoolean();
                else if (power.TryGetInt32(out var value)) isOn = value != 0;
                else return new(StatusReadOutcome.UnexpectedResponse);
                double? room = null;
                if (unit.TryGetProperty("sensors", out var sensors) && sensors.ValueKind == JsonValueKind.Object
                    && sensors.TryGetProperty("room_temp", out var roomValue) && roomValue.ValueKind == JsonValueKind.Number
                    && roomValue.TryGetDouble(out var measured)) room = measured;
                var firmware = unit.TryGetProperty("fw_ver", out var fw) && fw.ValueKind == JsonValueKind.String ? fw.GetString() : null;
                var fan = unit.TryGetProperty("fan", out var fanValue) && fanValue.ValueKind == JsonValueKind.Number ? fanValue.ToString() : null;
                return new(StatusReadOutcome.Succeeded, new(isOn, mode.ToString(), target, room, firmware, fan));
            }
            catch (Exception e) when (e is JsonException or DecoderFallbackException) { return new(StatusReadOutcome.KeyRejected); }
            catch (InvalidOperationException) { return new(StatusReadOutcome.UnexpectedResponse); }
            finally { CryptographicOperations.ZeroMemory(packet.Plaintext!); }
    }

}

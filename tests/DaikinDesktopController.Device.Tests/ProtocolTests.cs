using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using DaikinDesktopController.Device;
using DaikinDesktopController.Device.Discovery;
using DaikinDesktopController.Device.LocalProtocol;
using Xunit;
namespace DaikinDesktopController.Device.Tests;
public class ProtocolTests
{
    internal static DeviceSecretKey Key => Parse(Convert.ToBase64String(Encoding.ASCII.GetBytes("YELLOW SUBMARINE")));
    internal static DeviceSecretKey Parse(string text) { Assert.True(DeviceSecretKey.TryParse(text, out var key)); return key!; }
    [Fact] public void CrcMatchesReference() => Assert.Equal(0xe039, Crc16.Compute("test"u8));
    [Theory]
    [InlineData("not base64")]
    [InlineData("MDEyMzQ1Njc4OQ==")]
    [InlineData("WVVMTE9XIFNVQk1BUklORQ== extra")]
    public void InvalidSecretsAreRejected(string text) => Assert.False(DeviceSecretKey.TryParse(text, out _));
    [Fact] public void KeyTrimsNewlineAndRedacts()
    {
        var key = Parse(Key.Text + "\n");
        Assert.Equal(Key.Text, key.Text); Assert.Equal("[redacted]", key.ToString());
    }
    [Fact] public void AesMatchesNistVectorIncludingPartialBlock()
    {
        var key = Convert.FromHexString("2b7e151628aed2a6abf7158809cf4f3c");
        var iv = Convert.FromHexString("000102030405060708090a0b0c0d0e0f");
        var plain = Convert.FromHexString("6bc1bee22e409f96e93d7e117393172aae2d8a571e03ac9c9eb76fac45af8e51");
        var cipher = Convert.FromHexString("3b3fd92eb72dad20333449f8e83cfb4ac8a64537a0b3a93fcde3cdad9f1ce58b");
        Assert.Equal(cipher, AesCfb128.Encrypt(plain, key, iv));
        Assert.Equal(cipher[..23], AesCfb128.Encrypt(plain[..23], key, iv));
        Assert.Equal(plain, AesCfb128.Decrypt(cipher, key, iv));
        Assert.Equal(plain[..23], AesCfb128.Decrypt(cipher[..23], key, iv));
        Assert.Equal(plain[..23], AesCfb128.Decrypt(Encrypt(plain[..23], key, iv), key, iv));
    }
    [Fact] public void RequestRoundTripsWithRandomIvAndValidCrc()
    {
        var plain = "{\"port1\":{\"power\":1}}BZ"u8.ToArray();
        var first = Convert.FromBase64String(RequestPacket.Encode(plain, Key));
        var second = Convert.FromBase64String(RequestPacket.Encode(plain, Key));
        Assert.False(first.AsSpan(0, 16).SequenceEqual(second.AsSpan(0, 16)));
        Assert.Equal(Crc16.Compute(first.AsSpan(0, first.Length - 2)), BinaryPrimitives.ReadUInt16LittleEndian(first.AsSpan(first.Length - 2)));
        Assert.Equal(plain, AesCfb128.Decrypt(first.AsSpan(16, first.Length - 18), Key.Bytes.Span, first.AsSpan(0, 16)));
    }
    [Fact] public void CommandPlaintextIsCompactAndSuffixed()
    {
        Assert.Equal("{\"port1\":{\"power\":1}}BZ", Encoding.UTF8.GetString(new AirConditionerCommand.SetPower(true).ToPlaintext()));
        Assert.Equal("{\"port1\":{\"power\":0}}BZ", Encoding.UTF8.GetString(new AirConditionerCommand.SetPower(false).ToPlaintext()));
        Assert.Equal("{\"port1\":{\"temperature\":24}}BZ", Encoding.UTF8.GetString(new AirConditionerCommand.SetTargetTemperature(24).ToPlaintext()));
    }
    [Theory]
    [InlineData("0", 18, 30)] [InlineData("1", 18, 30)] [InlineData("3", 18, 32)] [InlineData("4", 10, 30)]
    public void ModeDefinesTemperatureCapabilities(string mode, double min, double max)
    {
        Assert.Equal(new TargetTemperatureRange(min, max, 1), TargetTemperatureRange.ForMode(mode));
        var status = new AirConditionerStatus(true, mode, 24, null, null);
        Assert.True(new AirConditionerCommand.SetTargetTemperature(min).IsSupportedBy(status));
        Assert.False(new AirConditionerCommand.SetTargetTemperature(min - 1).IsSupportedBy(status));
        Assert.False(new AirConditionerCommand.SetTargetTemperature(max + 1).IsSupportedBy(status));
        Assert.False(new AirConditionerCommand.SetTargetTemperature(min + 0.5).IsSupportedBy(status));
    }
    [Theory] [InlineData("2")] [InlineData("6")] [InlineData("unknown")]
    public void UnsupportedModesRejectTemperature(string mode)
    {
        Assert.Null(TargetTemperatureRange.ForMode(mode));
        Assert.False(new AirConditionerCommand.SetTargetTemperature(24).IsSupportedBy(new(true, mode, 24, null, null)));
    }
    [Theory]
    [InlineData("0", OperatingMode.Auto)] [InlineData("1", OperatingMode.Auto)]
    [InlineData("2", OperatingMode.Dry)] [InlineData("3", OperatingMode.Cool)]
    [InlineData("4", OperatingMode.Heat)] [InlineData("6", OperatingMode.Fan)] [InlineData("99", null)]
    public void MapsReportedModes(string code, OperatingMode? expected) =>
        Assert.Equal(expected, new AirConditionerStatus(true, code, 24, null, null).OperatingMode);
    [Theory]
    [InlineData("17", FanSpeed.Auto)] [InlineData("18", FanSpeed.Quiet)] [InlineData("3", FanSpeed.Low)]
    [InlineData("4", FanSpeed.MediumLow)] [InlineData("5", FanSpeed.Medium)]
    [InlineData("6", FanSpeed.MediumHigh)] [InlineData("7", FanSpeed.High)] [InlineData("99", null)]
    public void MapsReportedFanSpeeds(string code, FanSpeed? expected) =>
        Assert.Equal(expected, new AirConditionerStatus(true, "3", 24, null, null, code).FanSpeed);
    [Theory]
    [InlineData("0", "17", true)] [InlineData("1", "17", true)] [InlineData("3", "3", true)]
    [InlineData("4", "5", true)] [InlineData("6", "18", true)] [InlineData("2", "17", false)]
    [InlineData("99", "17", false)] [InlineData("3", null, false)] [InlineData("3", "99", false)]
    public void FanCapabilitiesRequireSupportedModeAndSpeed(string mode, string? fan, bool supported)
    {
        var status = new AirConditionerStatus(true, mode, 24, null, null, fan);
        Assert.Equal(supported, new AirConditionerCommand.SetFanSpeed(FanSpeed.Medium).IsSupportedBy(status));
        Assert.False(new AirConditionerCommand.SetFanSpeed((FanSpeed)99).IsSupportedBy(status));
        Assert.False(new AirConditionerCommand.SetOperatingMode((OperatingMode)99).IsSupportedBy(status));
    }
    [Theory]
    [InlineData(OperatingMode.Auto, 1)] [InlineData(OperatingMode.Cool, 3)]
    [InlineData(OperatingMode.Heat, 4)] [InlineData(OperatingMode.Fan, 6)]
    public void ModeWritesOnlyMode(OperatingMode mode, int code) => Assert.Equal(
        "{\"port1\":{\"mode\":" + code + "}}BZ", Encoding.UTF8.GetString(new AirConditionerCommand.SetOperatingMode(mode).ToPlaintext()));
    [Fact] public void DryWritesAutomaticFanAndMediumWritesOnlyFan()
    {
        Assert.Equal("{\"port1\":{\"mode\":2,\"fan\":17}}BZ", Encoding.UTF8.GetString(new AirConditionerCommand.SetOperatingMode(OperatingMode.Dry).ToPlaintext()));
        Assert.Equal("{\"port1\":{\"fan\":5}}BZ", Encoding.UTF8.GetString(new AirConditionerCommand.SetFanSpeed(FanSpeed.Medium).ToPlaintext()));
        Assert.True(new AirConditionerCommand.SetOperatingMode(OperatingMode.Auto).IsSatisfiedBy(new(false, "0", 24, null, null)));
        Assert.True(new AirConditionerCommand.SetFanSpeed(FanSpeed.Medium).IsSatisfiedBy(new(false, "3", 24, null, null, "5")));
    }
    [Fact] public void FeatureCommandsWriteVendorPresets()
    {
        Assert.Equal("{\"port1\":{\"powerchill\":1,\"econo\":0}}BZ", Encoding.UTF8.GetString(new AirConditionerCommand.SetTurboMode(true).ToPlaintext()));
        Assert.Equal("{\"port1\":{\"powerchill\":0}}BZ", Encoding.UTF8.GetString(new AirConditionerCommand.SetTurboMode(false).ToPlaintext()));
        Assert.Equal("{\"port1\":{\"coanda\":1}}BZ", Encoding.UTF8.GetString(new AirConditionerCommand.SetComfortMode(true).ToPlaintext()));
        Assert.Equal("{\"port1\":{\"coanda\":0}}BZ", Encoding.UTF8.GetString(new AirConditionerCommand.SetComfortMode(false).ToPlaintext()));
        Assert.Equal("{\"port1\":{\"econo\":1,\"powerchill\":0}}BZ", Encoding.UTF8.GetString(new AirConditionerCommand.SetEconomyMode(true).ToPlaintext()));
        Assert.Equal("{\"port1\":{\"econo\":0}}BZ", Encoding.UTF8.GetString(new AirConditionerCommand.SetEconomyMode(false).ToPlaintext()));
    }
    [Theory] [InlineData(null)] [InlineData(false)] [InlineData(true)]
    public void FeatureSupportAndSatisfactionRequireReportedField(bool? reported)
    {
        var status = new AirConditionerStatus(false, "99", 24, null, null, null, reported, reported, reported);
        AirConditionerCommand[] commands = [new AirConditionerCommand.SetTurboMode(true), new AirConditionerCommand.SetComfortMode(true), new AirConditionerCommand.SetEconomyMode(true)];
        AirConditionerCommand[] offCommands = [new AirConditionerCommand.SetTurboMode(false), new AirConditionerCommand.SetComfortMode(false), new AirConditionerCommand.SetEconomyMode(false)];
        foreach (var command in commands) { Assert.Equal(reported is not null, command.IsSupportedBy(status)); Assert.Equal(reported == true, command.IsSatisfiedBy(status)); }
        foreach (var command in offCommands) { Assert.Equal(reported is not null, command.IsSupportedBy(status)); Assert.Equal(reported == false, command.IsSatisfiedBy(status)); }
    }
    internal static byte[] Encrypt(byte[] plain, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create(); aes.Key = key;
        var feedback = iv.ToArray(); var output = new byte[plain.Length];
        for (var offset = 0; offset < plain.Length; offset += 16)
        {
            var stream = aes.EncryptEcb(feedback, PaddingMode.None);
            var size = Math.Min(16, plain.Length - offset);
            for (var i = 0; i < size; i++) output[offset + i] = (byte)(plain[offset + i] ^ stream[i]);
            if (size == 16) Array.Copy(output, offset, feedback, 0, 16);
        }
        return output;
    }
    internal const string Json = "{\"port1\":{\"power\":1,\"mode\":2,\"fan\":17,\"temperature\":24,\"sensors\":{\"room_temp\":26},\"fw_ver\":\"1.0\"},\"idu\":1}";
    internal static string Packet(string json = Json)
    {
        var iv = Enumerable.Range(0,16).Select(i => (byte)i).ToArray();
        var cipher = Encrypt(Encoding.UTF8.GetBytes(json), Key.Bytes.ToArray(), iv);
        var bytes = new byte[19 + cipher.Length]; iv.CopyTo(bytes,0); cipher.CopyTo(bytes,17);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(bytes.Length-2), Crc16.Compute(bytes.AsSpan(0,bytes.Length-2)));
        return Convert.ToBase64String(bytes);
    }
    [Fact] public void PacketValidatesBeforeDecrypting()
    {
        Assert.Equal(Json, Encoding.UTF8.GetString(ResponsePacket.Decode(Packet(), Key).Plaintext!));
        var bytes = Convert.FromBase64String(Packet()); bytes[20] ^= 1;
        Assert.Equal(PacketFailure.CrcMismatch, ResponsePacket.Decode(Convert.ToBase64String(bytes), Key).Failure);
        Assert.Equal(PacketFailure.TooShort, ResponsePacket.Decode("AA==", Key).Failure);
        Assert.Equal(PacketFailure.InvalidBase64, ResponsePacket.Decode("!", Key).Failure);
    }
    [Fact] public void DiscoveryFiltersAndDeduplicatesWithoutAControlPort()
    {
        var address = IPAddress.Parse("192.0.2.1");
        ServiceAdvertisement Ad(string name, IPAddress ip, string? id, string? host = "host.local") =>
            new(name, [ip], id is null ? new Dictionary<string,string>() : new Dictionary<string,string> { ["apn"] = id }, host);
        var found = DiscoveryFilter.Apply([Ad("other",address,"other"), Ad("Daikin Smart AC",IPAddress.IPv6Loopback,"v6"),
            Ad("Daikin Smart AC",address,"unit"), Ad("Daikin Smart AC",IPAddress.Loopback,"unit"),
            Ad("Daikin Smart AC",address,null), Ad("Daikin Smart AC",address,null,null)]);
        Assert.Equal(2, found.Count); Assert.Equal("unit",found[0].Id); Assert.Equal(address,found[0].Address);
        Assert.Equal("host.local",found[1].Id); Assert.Equal(15914,LocalAirConditionerClient.ControlPort);
        Assert.DoesNotContain(typeof(DiscoveredAirConditioner).GetProperties(), p => p.Name.Contains("Port"));
    }
}
public class ClientTests
{
    [Theory]
    [InlineData(false, StatusReadOutcome.Succeeded)]
    [InlineData(true, StatusReadOutcome.KeyRejected)]
    public async Task ReadsIndentedHeadersAndFragmentedBody(bool wrongKey, StatusReadOutcome expected)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var body = ProtocolTests.Packet();
        var server = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(); using var stream = socket.GetStream();
            var buffer = new byte[1024]; var count = await stream.ReadAsync(buffer);
            Assert.StartsWith("GET /acstatus HTTP/1.1", Encoding.ASCII.GetString(buffer,0,count));
            var header = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\n    Content-Length: {body.Length}\r\n    Content-Type: text/plain\r\n\r\n");
            await stream.WriteAsync(header);
            foreach (var part in body.Chunk(7)) await stream.WriteAsync(Encoding.ASCII.GetBytes(part));
            // Content-Length completes the read even while the connection stays open.
            await Task.Delay(100);
        });
        var key = wrongKey ? ProtocolTests.Parse(Convert.ToBase64String(new byte[16])) : ProtocolTests.Key;
        var result = await new LocalAirConditionerClient(endpoint.Port, TimeSpan.FromSeconds(2)).ReadStatusAsync(IPAddress.Loopback,key);
        Assert.Equal(expected, result.Outcome);
        if (!wrongKey) { Assert.True(result.Status!.IsOn); Assert.Equal(24, result.Status.TargetTemperature); Assert.Equal(26, result.Status.RoomTemperature); Assert.Equal("17", result.Status.Fan); }
        await server;
    }
    [Theory]
    [InlineData("match", CommandOutcome.Confirmed)]
    [InlineData("different", CommandOutcome.NotApplied)]
    [InlineData("crc", CommandOutcome.UnexpectedResponse)]
    [InlineData("silent", CommandOutcome.NoResponse)]
    public async Task CommandUsesRawPostAndClassifiesReply(string kind, CommandOutcome expected)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () =>
        {
            using var peer = await listener.AcceptTcpClientAsync(); using var stream = peer.GetStream();
            using var received = new MemoryStream(); var chunk = new byte[1024];
            string request; int end, length;
            while (true)
            {
                var count = await stream.ReadAsync(chunk); Assert.True(count > 0); received.Write(chunk, 0, count);
                request = Encoding.ASCII.GetString(received.ToArray()); end = request.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (end < 0) continue;
                var headers = request[..end].Split("\r\n");
                Assert.Equal("POST /acstatus HTTP/1.1", headers[0]); Assert.Equal(3, headers.Length);
                length = int.Parse(headers.Single(h => h.StartsWith("Content-Length:", StringComparison.Ordinal)).Split(':')[1]);
                if (request.Length - end - 4 >= length) break;
            }
            var packet = Convert.FromBase64String(request.Substring(end + 4, length));
            Assert.Equal(Crc16.Compute(packet.AsSpan(0, packet.Length - 2)), BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(packet.Length - 2)));
            Assert.Equal("{\"port1\":{\"power\":1}}BZ", Encoding.UTF8.GetString(AesCfb128.Decrypt(packet.AsSpan(16, packet.Length - 18), ProtocolTests.Key.Bytes.Span, packet.AsSpan(0, 16))));
            if (kind == "silent") { await Task.Delay(500); return; }
            var body = ProtocolTests.Packet(kind == "different" ? ProtocolTests.Json.Replace("\"power\":1", "\"power\":0") : ProtocolTests.Json);
            if (kind == "crc") { var bytes = Convert.FromBase64String(body); bytes[^1] ^= 1; body = Convert.ToBase64String(bytes); }
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\n\r\n{body}"));
        });
        var result = await new LocalAirConditionerClient(port, TimeSpan.FromMilliseconds(250)).SendCommandAsync(IPAddress.Loopback, ProtocolTests.Key, new AirConditionerCommand.SetPower(true));
        Assert.Equal(expected, result.Outcome);
        if (kind is "match" or "different") Assert.NotNull(result.Status);
        await server;
    }
    [Theory] [InlineData(null)] [InlineData("17")] [InlineData("\"17\"")]
    public async Task OptionalFanDoesNotRejectStatus(string? fan)
    {
        var json = "{\"port1\":{\"power\":1,\"mode\":3,\"temperature\":24" + (fan is null ? "" : ",\"fan\":" + fan) + "}}";
        var body = ProtocolTests.Packet(json);
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var server = Task.Run(async () =>
        {
            using var peer = await listener.AcceptTcpClientAsync(); var stream = peer.GetStream();
            Assert.True(await stream.ReadAsync(new byte[1024]) > 0);
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\n\r\n{body}"));
        });
        var result = await new LocalAirConditionerClient(((IPEndPoint)listener.LocalEndpoint).Port, TimeSpan.FromSeconds(2))
            .ReadStatusAsync(IPAddress.Loopback, ProtocolTests.Key);
        Assert.Equal(StatusReadOutcome.Succeeded, result.Outcome);
        Assert.Equal(fan == "17" ? "17" : null, result.Status!.Fan);
        await server;
    }
    [Theory] [InlineData(null, null)] [InlineData("0", false)] [InlineData("-2", true)] [InlineData("0.5", true)] [InlineData("true", true)] [InlineData("false", false)] [InlineData("\"1\"", null)] [InlineData("null", null)] [InlineData("{}", null)] [InlineData("[]", null)]
    public async Task OptionalFeaturesDoNotRejectStatus(string? flag, bool? expected)
    {
        var json = "{\"port1\":{\"power\":1,\"mode\":3,\"temperature\":24" + (flag is null ? "" : ",\"powerchill\":" + flag + ",\"coanda\":" + flag + ",\"econo\":" + flag) + "}}";
        var body = ProtocolTests.Packet(json);
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var server = Task.Run(async () =>
        {
            using var peer = await listener.AcceptTcpClientAsync(); var stream = peer.GetStream();
            Assert.True(await stream.ReadAsync(new byte[1024]) > 0);
            await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\n\r\n{body}"));
        });
        var result = await new LocalAirConditionerClient(((IPEndPoint)listener.LocalEndpoint).Port, TimeSpan.FromSeconds(2))
            .ReadStatusAsync(IPAddress.Loopback, ProtocolTests.Key);
        Assert.Equal(StatusReadOutcome.Succeeded, result.Outcome);
        Assert.Equal(expected, result.Status!.TurboMode);
        Assert.Equal(expected, result.Status.ComfortMode);
        Assert.Equal(expected, result.Status.EconomyMode);
        await server;
    }
    [Fact] public async Task ClosedPortIsUnreachable()
    {
        using var listener = new TcpListener(IPAddress.Loopback,0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        Assert.Equal(StatusReadOutcome.Unreachable,(await new LocalAirConditionerClient(port,TimeSpan.FromSeconds(1))
            .ReadStatusAsync(IPAddress.Loopback,ProtocolTests.Key)).Outcome);
    }
    [Fact] public async Task SilentServerTimesOutAndCallerCancellationPropagates()
    {
        using var listener = new TcpListener(IPAddress.Loopback,0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accept = listener.AcceptTcpClientAsync();
        var request = new LocalAirConditionerClient(port,TimeSpan.FromMilliseconds(100)).ReadStatusAsync(IPAddress.Loopback,ProtocolTests.Key);
        using var peer = await accept;
        Assert.Equal(StatusReadOutcome.Unreachable,(await request).Outcome);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new LocalAirConditionerClient(port,TimeSpan.FromSeconds(1))
            .ReadStatusAsync(IPAddress.Loopback,ProtocolTests.Key,cancel.Token));
    }
    [Theory]
    [InlineData("crc", StatusReadOutcome.UnexpectedResponse)]
    [InlineData("not-json", StatusReadOutcome.KeyRejected)]
    [InlineData("{}", StatusReadOutcome.KeyRejected)]
    public async Task PacketAndPayloadFailuresAreClassified(string payload, StatusReadOutcome expected)
    {
        var body = payload == "crc" ? ProtocolTests.Packet() : ProtocolTests.Packet(payload);
        if (payload == "crc")
        {
            var bytes = Convert.FromBase64String(body); bytes[^1] ^= 1; body = Convert.ToBase64String(bytes);
        }
        using var listener = new TcpListener(IPAddress.Loopback,0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () =>
        {
            using var peer = await listener.AcceptTcpClientAsync(); var stream = peer.GetStream();
            Assert.True(await stream.ReadAsync(new byte[1024]) > 0);
            // No Content-Length: completion requires reading to EOF.
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n    Content-Type: text/plain\r\n\r\n" + body));
        });
        Assert.Equal(expected,(await new LocalAirConditionerClient(port,TimeSpan.FromSeconds(1))
            .ReadStatusAsync(IPAddress.Loopback,ProtocolTests.Key)).Outcome);
        await server;
    }
    [Theory]
    [InlineData("HTTP/1.1 503 Error\r\nContent-Length: 0\r\n\r\n")]
    [InlineData("HTTP/1.1 200 OK\r\nContent-Length: 1\r\n\r\n!")]
    [InlineData("HTTP/1.1 200 OK\r\nContent-Length: 20\r\n\r\nshort")]
    [InlineData("HTTP/1.1 200 OK\r\nContent-Length: 999999999\r\n\r\n")]
    public async Task InvalidResponsesAreUnexpected(string response)
    {
        using var listener = new TcpListener(IPAddress.Loopback,0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () => { using var peer = await listener.AcceptTcpClientAsync();
            var stream = peer.GetStream(); Assert.True(await stream.ReadAsync(new byte[1024]) > 0); await stream.WriteAsync(Encoding.ASCII.GetBytes(response)); });
        Assert.Equal(StatusReadOutcome.UnexpectedResponse,(await new LocalAirConditionerClient(port,TimeSpan.FromSeconds(1))
            .ReadStatusAsync(IPAddress.Loopback,ProtocolTests.Key)).Outcome);
        await server;
    }
}

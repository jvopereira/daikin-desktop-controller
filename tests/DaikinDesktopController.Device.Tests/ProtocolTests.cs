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
        Assert.Equal(plain, AesCfb128.Decrypt(cipher, key, iv));
        Assert.Equal(plain[..23], AesCfb128.Decrypt(cipher[..23], key, iv));
        Assert.Equal(plain[..23], AesCfb128.Decrypt(Encrypt(plain[..23], key, iv), key, iv));
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
    internal const string Json = "{\"port1\":{\"power\":1,\"mode\":2,\"temperature\":24,\"sensors\":{\"room_temp\":26},\"fw_ver\":\"1.0\"},\"idu\":1}";
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
        if (!wrongKey) { Assert.True(result.Status!.IsOn); Assert.Equal(24, result.Status.TargetTemperature); Assert.Equal(26, result.Status.RoomTemperature); }
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

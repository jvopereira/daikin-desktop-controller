using System.Buffers.Binary;
namespace DaikinDesktopController.Device.LocalProtocol;
internal enum PacketFailure { InvalidBase64, TooShort, CrcMismatch }
internal sealed record PacketDecode(byte[]? Plaintext, PacketFailure? Failure);
internal static class ResponsePacket
{
    public static PacketDecode Decode(string body, DeviceSecretKey key)
    {
        byte[] packet;
        try { packet = Convert.FromBase64String(body); }
        catch (FormatException) { return new(null, PacketFailure.InvalidBase64); }
        if (packet.Length < 19) return new(null, PacketFailure.TooShort);
        if (Crc16.Compute(packet.AsSpan(0, packet.Length - 2)) != BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(packet.Length - 2)))
            return new(null, PacketFailure.CrcMismatch);
        return new(AesCfb128.Decrypt(packet.AsSpan(17, packet.Length - 19), key.Bytes.Span, packet.AsSpan(0, 16)), null);
    }
}

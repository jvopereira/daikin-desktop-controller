using System.Buffers.Binary;
using System.Security.Cryptography;
namespace DaikinDesktopController.Device.LocalProtocol;
internal static class RequestPacket
{
    public static string Encode(ReadOnlySpan<byte> plaintext, DeviceSecretKey key)
    {
        var iv = RandomNumberGenerator.GetBytes(16);
        var cipher = AesCfb128.Encrypt(plaintext, key.Bytes.Span, iv);
        var packet = new byte[18 + cipher.Length];
        iv.CopyTo(packet, 0); cipher.CopyTo(packet, 16);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(packet.Length - 2), Crc16.Compute(packet.AsSpan(0, packet.Length - 2)));
        return Convert.ToBase64String(packet);
    }
}

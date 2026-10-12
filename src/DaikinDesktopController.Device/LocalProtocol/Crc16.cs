namespace DaikinDesktopController.Device.LocalProtocol;
internal static class Crc16
{
    public static ushort Compute(ReadOnlySpan<byte> bytes)
    {
        ushort crc = 0xffff;
        foreach (var value in bytes)
        {
            crc ^= (ushort)(value << 8);
            for (var bit = 0; bit < 8; bit++)
                crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
        }
        return (ushort)(crc ^ 0xffff);
    }
}

using System.Diagnostics.CodeAnalysis;
namespace DaikinDesktopController.Device;
public sealed class DeviceSecretKey
{
    private readonly byte[] bytes;
    private DeviceSecretKey(string text, byte[] value) { Text = text; bytes = value; }
    public string Text { get; }
    public ReadOnlyMemory<byte> Bytes => bytes;
    public static bool TryParse(string? input, [NotNullWhen(true)] out DeviceSecretKey? key)
    {
        key = null;
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > 44 || text.Any(char.IsWhiteSpace)) return false;
        try
        {
            var bytes = Convert.FromBase64String(text);
            if (bytes.Length is not (16 or 24 or 32) || Convert.ToBase64String(bytes) != text) return false;
            key = new DeviceSecretKey(text, bytes);
            return true;
        }
        catch (FormatException) { return false; }
    }
    public override string ToString() => "[redacted]";
}

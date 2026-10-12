using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace DaikinDesktopController.App.Persistence;
internal static class JsonFile
{
    private static readonly JsonSerializerOptions Options = new()
    { WriteIndented = true, Converters = { new JsonStringEnumConverter(allowIntegerValues: false) } };
    public static T? Read<T>(string path)
    {
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException or JsonException)
        { return default; }
    }
    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(stream, value, Options); stream.Flush(flushToDisk: true); }
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

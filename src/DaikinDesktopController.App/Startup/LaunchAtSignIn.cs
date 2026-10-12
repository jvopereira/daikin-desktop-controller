using Microsoft.Win32;
namespace DaikinDesktopController.App.Startup;
public interface IRunKey
{
    string? Read(string name);
    void Write(string name, string value);
    void Delete(string name);
}
public sealed class WindowsRunKey : IRunKey
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public string? Read(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(name) as string;
    }
    public void Delete(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
    public void Write(string name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue(name, value, RegistryValueKind.String);
    }
}
public sealed class LaunchAtSignIn(IRunKey runKey)
{
    public const string ValueName = "DaikinDesktopController";
    public void Apply(bool enabled, string processPath)
    {
        if (!enabled)
        {
            if (runKey.Read(ValueName) is not null) runKey.Delete(ValueName);
            return;
        }
        if (string.IsNullOrWhiteSpace(processPath) || processPath.Contains('"'))
            throw new ArgumentException("An executable path without quotes is required.", nameof(processPath));
        var command = $"\"{processPath}\"";
        if (runKey.Read(ValueName) != command) runKey.Write(ValueName, command);
    }
}

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Net;
using DaikinDesktopController.App.Localization;
using DaikinDesktopController.App.Preferences;
namespace DaikinDesktopController.App.Settings;
public partial class DeviceSettingsView : UserControl
{
    private readonly PreferencesSession preferences;
    public DeviceSettingsView(PreferencesSession preferences)
    {
        this.preferences = preferences;
        InitializeComponent(); Address.Text = preferences.Current.ManualIpAddress ?? "";
    }
    private void Message(string key) => Feedback.SetBinding(TextBlock.TextProperty, new Binding($"[{key}]") { Source = Localizer.Instance });
    private void AddressKeyDown(object sender, KeyEventArgs e)
    { if (e.Key == Key.Enter) { CommitAddress(sender, e); e.Handled = true; } }
    private void CommitAddress(object sender, RoutedEventArgs e)
    {
        var text = Address.Text.Trim();
        if (text.Length != 0 && !JsonPreferencesStore.IsIpv4(text)) { Message("InvalidIp"); return; }
        try
        {
            var address = text.Length == 0 ? null : IPAddress.Parse(text).ToString();
            preferences.Update(p => p with { ManualIpAddress = address });
            Message("DeviceIpSaved");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { Message("PreferencesStorageFailure"); }
    }
}

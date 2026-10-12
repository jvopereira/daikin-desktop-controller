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
    private bool loading = true;
    public DeviceSettingsView(PreferencesSession preferences)
    {
        this.preferences = preferences;
        InitializeComponent();
        RefreshInterval.ItemsSource = AppPreferences.StatusRefreshIntervals.Select(seconds =>
        {
            var item = new ComboBoxItem { Tag = seconds };
            item.SetBinding(ContentControl.ContentProperty, new Binding($"[StatusRefresh{seconds}]") { Source = Localizer.Instance });
            return item;
        }).ToArray();
        Sync();
        Loaded += (_, _) => { preferences.Changed += PreferencesChanged; Sync(); };
        Unloaded += (_, _) => preferences.Changed -= PreferencesChanged;
    }
    private void PreferencesChanged(AppPreferences old, AppPreferences now) => Sync();
    private void Sync()
    {
        loading = true;
        Address.Text = preferences.Current.ManualIpAddress ?? "";
        RefreshInterval.SelectedItem = RefreshInterval.Items.Cast<ComboBoxItem>()
            .First(i => (int)i.Tag == preferences.Current.StatusRefreshIntervalSeconds);
        loading = false;
    }
    private void IntervalChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading || RefreshInterval.SelectedItem is not ComboBoxItem item) return;
        try { preferences.Update(p => p with { StatusRefreshIntervalSeconds = (int)item.Tag }); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        { Sync(); Message("PreferencesStorageFailure"); }
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

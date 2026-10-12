using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DaikinDesktopController.App.Localization;
using DaikinDesktopController.App.Preferences;
using DaikinDesktopController.App.Secrets;
namespace DaikinDesktopController.App.Settings;
public partial class SettingsWindow : Window
{
    private readonly GeneralSettingsView general;
    private readonly SecretsSettingsView secrets;
    private readonly DeviceSettingsView device;
    public event EventHandler? SecretKeySaved;
    public SettingsWindow(PreferencesSession preferences, ISecretStore secretStore)
    {
        InitializeComponent();
        general = new GeneralSettingsView(preferences);
        secrets = new SecretsSettingsView(secretStore);
        device = new DeviceSettingsView(preferences);
        secrets.SecretKeySaved += (_, _) => SecretKeySaved?.Invoke(this, EventArgs.Empty);
        Navigation.ItemsSource = Enum.GetValues<SettingsSection>().Select(section =>
        {
            var item = new ListBoxItem { Tag = section };
            item.SetBinding(ContentControl.ContentProperty, new Binding($"[{section}]") { Source = Localizer.Instance });
            return item;
        }).ToArray();
    }
    public void Show(SettingsSection section = SettingsSection.Secrets)
    {
        Navigation.SelectedIndex = (int)section;
        base.Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }
    private void SectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SectionContent is null || Navigation.SelectedItem is not ListBoxItem item) return;
        SectionContent.Content = item.Tag switch
        {
            SettingsSection.General => general,
            SettingsSection.Secrets => secrets,
            SettingsSection.Device => device,
            _ => null
        };
    }
}

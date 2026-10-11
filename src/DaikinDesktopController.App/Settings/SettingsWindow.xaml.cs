using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DaikinDesktopController.App.Localization;
using DaikinDesktopController.App.Preferences;
namespace DaikinDesktopController.App.Settings;
public partial class SettingsWindow : Window
{
    private readonly GeneralSettingsView general;
    public SettingsWindow(PreferencesSession preferences)
    {
        InitializeComponent();
        general = new GeneralSettingsView(preferences);
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
        if (item.Tag is SettingsSection.General) { SectionContent.Content = general; return; }
        var placeholder = new TextBlock { FontSize = 16, TextWrapping = TextWrapping.Wrap };
        placeholder.SetBinding(TextBlock.TextProperty, new Binding($"[{item.Tag}Placeholder]") { Source = Localizer.Instance });
        SectionContent.Content = placeholder;
    }
}

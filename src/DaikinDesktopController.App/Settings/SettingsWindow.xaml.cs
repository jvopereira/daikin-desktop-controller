using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DaikinDesktopController.App.Localization;
namespace DaikinDesktopController.App.Settings;
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
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
        if (Placeholder is null || Navigation.SelectedItem is not ListBoxItem item) return;
        Placeholder.SetBinding(TextBlock.TextProperty, new Binding($"[{item.Tag}Placeholder]") { Source = Localizer.Instance });
    }
}

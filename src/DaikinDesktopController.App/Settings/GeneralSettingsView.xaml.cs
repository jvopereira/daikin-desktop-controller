using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DaikinDesktopController.App.Localization;
using DaikinDesktopController.App.Preferences;
namespace DaikinDesktopController.App.Settings;
public partial class GeneralSettingsView : UserControl
{
    private readonly PreferencesSession session;
    private bool loading = true;
    public GeneralSettingsView(PreferencesSession session)
    {
        this.session = session;
        InitializeComponent();
        Language.ItemsSource = Localizer.SupportedLanguages.Select(c => new ComboBoxItem
        { Content = Localizer.LanguageName(c), Tag = c.Name }).ToArray();
        Theme.ItemsSource = Enum.GetValues<ThemePreference>().Select(value =>
        {
            var item = new ComboBoxItem { Tag = value };
            item.SetBinding(ContentControl.ContentProperty, new Binding($"[Theme{value}]") { Source = Localizer.Instance });
            return item;
        }).ToArray();
        Sync();
        Loaded += (_, _) => { session.Changed += PreferencesChanged; Sync(); };
        Unloaded += (_, _) => session.Changed -= PreferencesChanged;
    }
    private void PreferencesChanged(AppPreferences old, AppPreferences now) => Sync();
    private void Sync()
    {
        loading = true;
        var current = session.Current;
        var culture = Localizer.ResolveCulture(current.Language, CultureInfo.InstalledUICulture);
        Language.SelectedItem = Language.Items.Cast<ComboBoxItem>().First(i => (string)i.Tag == culture.Name);
        Theme.SelectedItem = Theme.Items.Cast<ComboBoxItem>().First(i => (ThemePreference)i.Tag == current.Theme);
        Launch.IsChecked = current.LaunchAtSignIn;
        loading = false;
    }
    private void LanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!loading && Language.SelectedItem is ComboBoxItem item)
            session.Update(p => p with { Language = (string)item.Tag });
    }
    private void ThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!loading && Theme.SelectedItem is ComboBoxItem item)
            session.Update(p => p with { Theme = (ThemePreference)item.Tag });
    }
    private void LaunchChanged(object sender, RoutedEventArgs e)
    {
        if (!loading) session.Update(p => p with { LaunchAtSignIn = Launch.IsChecked == true });
    }
}

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DaikinDesktopController.App.Localization;
using DaikinDesktopController.App.Secrets;
using DaikinDesktopController.Device;
namespace DaikinDesktopController.App.Settings;
public partial class SecretsSettingsView : UserControl
{
    private readonly ISecretStore secrets;
    public event EventHandler? SecretKeySaved;
    public SecretsSettingsView(ISecretStore secrets)
    {
        this.secrets = secrets;
        InitializeComponent();
        RefreshHint();
    }
    private void RefreshHint()
    {
        try { SavedHint.Visibility = secrets.Read() is null ? Visibility.Collapsed : Visibility.Visible; }
        catch (Win32Exception e) { StorageError(e); }
    }
    private void Message(string key) => Feedback.SetBinding(TextBlock.TextProperty, new Binding($"[{key}]") { Source = Localizer.Instance });
    private void StorageError(Win32Exception error)
    {
        // Native error messages contain neither credential contents nor target input.
        Feedback.SetBinding(TextBlock.TextProperty, new Binding("[KeyStorageFailure]")
            { Source = Localizer.Instance, StringFormat = "{0} " + error.Message });
    }
    private void Reveal(object sender, RoutedEventArgs e)
    {
        VisibleKey.Text = HiddenKey.Password; HiddenKey.Clear();
        HiddenKey.Visibility = Visibility.Collapsed; VisibleKey.Visibility = Visibility.Visible;
    }
    private void HideKey(object sender, RoutedEventArgs e)
    {
        HiddenKey.Password = VisibleKey.Text; VisibleKey.Clear();
        VisibleKey.Visibility = Visibility.Collapsed; HiddenKey.Visibility = Visibility.Visible;
    }
    private void SaveKey(object sender, RoutedEventArgs e)
    {
        var text = VisibleKey.Visibility == Visibility.Visible ? VisibleKey.Text : HiddenKey.Password;
        if (!DeviceSecretKey.TryParse(text, out var key)) { Message("InvalidKey"); return; }
        try
        {
            secrets.Save(key);
            HiddenKey.Clear(); VisibleKey.Clear();
            SavedHint.Visibility = Visibility.Visible;
            Message("KeySaved");
            SecretKeySaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Win32Exception error) { StorageError(error); }
    }
}

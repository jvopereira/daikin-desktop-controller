using System.ComponentModel;
using System.Windows;
namespace DaikinDesktopController.App.Panel;
public partial class CompactControlPanel : Window
{
    public event EventHandler? SettingsRequested;
    public CompactControlPanel() => InitializeComponent();
    private void OpenSettings(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}

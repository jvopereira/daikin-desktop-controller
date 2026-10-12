using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DaikinDesktopController.App.Localization;
using DaikinDesktopController.App.Setup;
using DaikinDesktopController.App.Settings;
using DaikinDesktopController.Device;
namespace DaikinDesktopController.App.Panel;
public partial class CompactControlPanel : Window
{
    private readonly SetupCoordinator coordinator;
    private SettingsSection section;
    public event EventHandler? SettingsRequested;
    public event Action<SettingsSection>? SectionRequested;
    public CompactControlPanel(SetupCoordinator coordinator)
    {
        this.coordinator = coordinator;
        InitializeComponent();
        coordinator.StateChanged += Render;
        Render();
    }
    private static void Bind(DependencyObject target, DependencyProperty property, string key) =>
        BindingOperations.SetBinding(target, property, new Binding($"[{key}]") { Source = Localizer.Instance });
    private void Render()
    {
        var state = coordinator.State;
        var prefix = state switch
        {
            NeedsSecretKey => "FirstRun", Discovering => "Discovering", Pairing => "Pairing",
            ChoosingAirConditioner => "Choosing", Configured => "Configured", KeyRejected => "KeyRejected",
            NotFound => "NotFound", StorageFailure => "StorageFailure", _ => "UnexpectedResponse"
        };
        Bind(StateTitle, TextBlock.TextProperty, prefix + "Title");
        Bind(StateMessage, TextBlock.TextProperty, prefix + "Message");
        Progress.Visibility = state is Discovering or Pairing ? Visibility.Visible : Visibility.Collapsed;
        Candidates.ItemsSource = (state as ChoosingAirConditioner)?.Candidates;
        Candidates.Visibility = state is ChoosingAirConditioner ? Visibility.Visible : Visibility.Collapsed;
        var needsSecrets = state is NeedsSecretKey or KeyRejected or StorageFailure;
        section = needsSecrets ? SettingsSection.Secrets : SettingsSection.Device;
        ContextAction.Visibility = needsSecrets || state is NotFound or UnexpectedResponse ? Visibility.Visible : Visibility.Collapsed;
        Bind(ContextAction, ContentControl.ContentProperty, needsSecrets ? "OpenSecrets" : "OpenDeviceSettings");
        Retry.Visibility = state is NotFound or UnexpectedResponse or StorageFailure ? Visibility.Visible : Visibility.Collapsed;
    }
    private async void Choose(object sender, SelectionChangedEventArgs e)
    { if (Candidates.SelectedItem is DiscoveredAirConditioner candidate) await coordinator.SelectAsync(candidate); }
    private async void TryAgain(object sender, RoutedEventArgs e) => await coordinator.StartAsync();
    private void OpenSection(object sender, RoutedEventArgs e) => SectionRequested?.Invoke(section);
    private void OpenSettings(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
    protected override void OnClosing(CancelEventArgs e)
    { e.Cancel = true; Hide(); base.OnClosing(e); }
}

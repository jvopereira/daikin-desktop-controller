using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DaikinDesktopController.App.Localization;
using DaikinDesktopController.App.Setup;
using DaikinDesktopController.App.Monitoring;
using DaikinDesktopController.App.Settings;
using DaikinDesktopController.Device;
namespace DaikinDesktopController.App.Panel;
public partial class CompactControlPanel : Window
{
    private readonly SetupCoordinator coordinator;
    private readonly StatusMonitor monitor;
    private SettingsSection section;
    public event EventHandler? SettingsRequested;
    public event Action<SettingsSection>? SectionRequested;
    public CompactControlPanel(SetupCoordinator coordinator, StatusMonitor monitor)
    {
        this.coordinator = coordinator;
        this.monitor = monitor;
        InitializeComponent();
        coordinator.StateChanged += Render;
        monitor.StateChanged += Render;
        Localizer.Instance.PropertyChanged += LanguageChanged;
        IsVisibleChanged += (_, _) => Render();
        Render();
    }
    private static void Bind(DependencyObject target, DependencyProperty property, string key) =>
        BindingOperations.SetBinding(target, property, new Binding($"[{key}]") { Source = Localizer.Instance });
    private void LanguageChanged(object? sender, PropertyChangedEventArgs e) => Render();
    private void Render()
    {
        var state = coordinator.State;
        if (state is Configured && IsVisible) monitor.Start(); else monitor.Stop();
        StatusView.Visibility = Visibility.Collapsed;
        StateTitle.Visibility = StateMessage.Visibility = Visibility.Visible;
        if (state is Configured)
        {
            RenderStatus();
            return;
        }
        var prefix = state switch
        {
            NeedsSecretKey => "FirstRun", Discovering => "Discovering", Pairing => "Pairing",
            ChoosingAirConditioner => "Choosing", KeyRejected => "KeyRejected",
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
    private void RenderStatus()
    {
        Candidates.Visibility = ContextAction.Visibility = Retry.Visibility = Progress.Visibility = Visibility.Collapsed;
        switch (monitor.State)
        {
            case Available available:
                StateTitle.Visibility = StateMessage.Visibility = Visibility.Collapsed;
                StatusView.Visibility = Visibility.Visible;
                Bind(Power, TextBlock.TextProperty, available.Status.IsOn ? "PowerOn" : "PowerOff");
                TargetValue.Text = TemperatureText.Format(available.Status.TargetTemperature);
                RoomView.Visibility = available.Status.RoomTemperature is null ? Visibility.Collapsed : Visibility.Visible;
                RoomValue.Text = available.Status.RoomTemperature is { } room ? TemperatureText.Format(room) : "";
                break;
            case Unavailable unavailable:
                Bind(StateTitle, TextBlock.TextProperty, "UnavailableTitle");
                Bind(StateMessage, TextBlock.TextProperty, unavailable.Reason == UnavailableReason.StorageFailure
                    ? "StorageFailureMessage" : $"Unavailable{unavailable.Reason}Message");
                Retry.Visibility = Visibility.Visible;
                var needsSecrets = unavailable.Reason is UnavailableReason.KeyRejected or UnavailableReason.StorageFailure;
                section = needsSecrets ? SettingsSection.Secrets : SettingsSection.Device;
                ContextAction.Visibility = needsSecrets || unavailable.Reason == UnavailableReason.Unreachable ? Visibility.Visible : Visibility.Collapsed;
                Bind(ContextAction, ContentControl.ContentProperty, needsSecrets ? "OpenSecrets" : "OpenDeviceSettings");
                break;
            default:
                Bind(StateTitle, TextBlock.TextProperty, "ReadingStatusTitle");
                Bind(StateMessage, TextBlock.TextProperty, "ReadingStatusMessage");
                Progress.Visibility = Visibility.Visible;
                break;
        }
    }
    private async void Choose(object sender, SelectionChangedEventArgs e)
    { if (Candidates.SelectedItem is DiscoveredAirConditioner candidate) await coordinator.SelectAsync(candidate); }
    private async void TryAgain(object sender, RoutedEventArgs e) {
        if (coordinator.State is Configured) await monitor.RefreshAsync();
        else await coordinator.StartAsync();
    }
    private void OpenSection(object sender, RoutedEventArgs e) => SectionRequested?.Invoke(section);
    private void OpenSettings(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);
    protected override void OnClosing(CancelEventArgs e)
    { e.Cancel = true; Hide(); base.OnClosing(e); }
}

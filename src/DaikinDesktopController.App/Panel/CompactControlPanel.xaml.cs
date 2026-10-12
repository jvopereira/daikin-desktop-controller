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
    private readonly AirConditionerControls controls;
    private bool rendering, dragging;
    private SettingsSection section;
    public event EventHandler? SettingsRequested;
    public event Action<SettingsSection>? SectionRequested;
    public CompactControlPanel(SetupCoordinator coordinator, StatusMonitor monitor, AirConditionerControls controls)
    {
        this.coordinator = coordinator;
        this.monitor = monitor;
        this.controls = controls;
        InitializeComponent();
        coordinator.StateChanged += Render;
        controls.Changed += Render;
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
                RenderControls();
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
    private void RenderControls()
    {
        rendering = true;
        try
        {
            var mode = controls.OperatingMode;
            var fan = controls.FanSpeed;
            OperatingModeSelector.ItemsSource = Enum.GetValues<OperatingMode>().Where(value => value != OperatingMode.Heat).Select(value => new ControlOption<OperatingMode>(value, Localizer.Instance["OperatingMode" + value])).ToArray();
            FanSpeedSelector.ItemsSource = Enum.GetValues<FanSpeed>().Select(value => new ControlOption<FanSpeed>(value, Localizer.Instance["FanSpeed" + value])).ToArray();
            OperatingModeSelector.SelectedValue = mode.Value;
            FanSpeedSelector.SelectedValue = fan.Value;
            OperatingModeSelector.IsEnabled = controls.CanControl;
            FanSpeedSelector.IsEnabled = controls.CanControl && controls.FanSpeedSupported;
            OperatingModePending.Visibility = mode.IsPending ? Visibility.Visible : Visibility.Collapsed;
            FanSpeedPending.Visibility = fan.IsPending ? Visibility.Visible : Visibility.Collapsed;
            OperatingModeError.Text = mode.Value is null ? Localizer.Instance["OperatingModeUnknownMessage"]
                : mode.Error is null ? "" : Localizer.Instance["CommandNotAppliedMessage"];
            FanSpeedError.Text = !controls.FanSpeedSupported || fan.Error == ControlError.Unsupported
                ? Localizer.Instance["FanSpeedUnsupportedMessage"]
                : fan.Error is null ? "" : Localizer.Instance["CommandNotAppliedMessage"];
            var power = controls.Power;
            var temperature = controls.TargetTemperature;
            Bind(Power, TextBlock.TextProperty, power.Value ? "PowerOn" : "PowerOff");
            Bind(PowerToggle, ContentControl.ContentProperty, power.Value ? "TurnOff" : "TurnOn");
            PowerToggle.IsChecked = power.Value;
            PowerToggle.IsEnabled = controls.CanControl;
            PowerPending.Visibility = power.IsPending ? Visibility.Visible : Visibility.Collapsed;
            PowerError.Text = power.Error is null ? "" : Localizer.Instance["CommandNotAppliedMessage"];
            TemperaturePending.Visibility = temperature.IsPending ? Visibility.Visible : Visibility.Collapsed;
            var range = controls.TargetTemperatureRange;
            TemperatureError.Text = range is null || temperature.Error == ControlError.Unsupported
                ? Localizer.Instance["TargetTemperatureUnsupportedMessage"]
                : temperature.Error is null ? "" : Localizer.Instance["CommandNotAppliedMessage"];
            Decrease.IsEnabled = Increase.IsEnabled = TemperatureSlider.IsEnabled = controls.CanControl && range is not null;
            if (range is not null)
            {
                TemperatureSlider.Minimum = range.Min; TemperatureSlider.Maximum = range.Max;
                TemperatureSlider.TickFrequency = TemperatureSlider.SmallChange = range.Step;
            }
            if (!dragging)
            {
                TemperatureSlider.Value = temperature.Value;
                TargetValue.Text = TemperatureText.Format(temperature.Value);
            }
        }
        finally { rendering = false; }
    }
    private sealed record ControlOption<T>(T Value, string Label);
    private async void ChangeOperatingMode(object sender, SelectionChangedEventArgs e)
    {
        if (!rendering && OperatingModeSelector.SelectedItem is ControlOption<OperatingMode> option)
            await controls.SetOperatingMode(option.Value);
    }
    private async void ChangeFanSpeed(object sender, SelectionChangedEventArgs e)
    {
        if (!rendering && FanSpeedSelector.SelectedItem is ControlOption<FanSpeed> option)
            await controls.SetFanSpeed(option.Value);
    }
    private async void ChangePower(object sender, RoutedEventArgs e) => await controls.SetPower(!controls.Power.Value);
    private async void DecreaseTemperature(object sender, RoutedEventArgs e) => await controls.StepTargetTemperature(-1);
    private async void IncreaseTemperature(object sender, RoutedEventArgs e) => await controls.StepTargetTemperature(1);
    private void TemperatureDragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e) => dragging = true;
    private async void TemperatureDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        dragging = false;
        if (!e.Canceled) await controls.SetTargetTemperature(TemperatureSlider.Value);
        RenderControls();
    }
    private async void TemperatureChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (rendering || controls is null) return;
        TargetValue.Text = TemperatureText.Format(e.NewValue);
        if (!dragging) await controls.SetTargetTemperature(e.NewValue);
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

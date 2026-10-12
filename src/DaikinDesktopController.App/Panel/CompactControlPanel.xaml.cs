using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
    private string shownTarget = "";
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
        IsVisibleChanged += (_, _) => { if (IsVisible) PlayOpen(); Render(); };
        Render();
    }
    private static bool ReduceMotion => !SystemParameters.ClientAreaAnimation;
    private static void Bind(DependencyObject target, DependencyProperty property, string key) =>
        BindingOperations.SetBinding(target, property, new Binding($"[{key}]") { Source = Localizer.Instance });
    private void LanguageChanged(object? sender, PropertyChangedEventArgs e) => Render();
    private void PlayOpen()
    {
        if (ReduceMotion) return;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(220);
        Card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        CardOffset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, duration) { EasingFunction = ease });
    }
    private void Render()
    {
        var state = coordinator.State;
        if (state is Configured && IsVisible) monitor.Start(); else monitor.Stop();
        StatusView.Visibility = Visibility.Collapsed;
        StateView.Visibility = Visibility.Visible;
        RoomView.Visibility = PowerToggle.Visibility = FooterStatusView.Visibility = Visibility.Collapsed;
        HeaderTitle.Visibility = Visibility.Visible;
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
                StateView.Visibility = HeaderTitle.Visibility = Visibility.Collapsed;
                StatusView.Visibility = PowerToggle.Visibility = FooterStatusView.Visibility = Visibility.Visible;
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
            var text = Localizer.Instance;
            var mode = controls.OperatingMode;
            var fan = controls.FanSpeed;
            OperatingModeSelector.ItemsSource = Enum.GetValues<OperatingMode>().Where(value => value != OperatingMode.Heat).Select(value => new ControlOption<OperatingMode>(value, text["OperatingMode" + value])).ToArray();
            FanSpeedSelector.ItemsSource = Enum.GetValues<FanSpeed>().Select(value => new ControlOption<FanSpeed>(value, text["FanSpeed" + value])).ToArray();
            OperatingModeSelector.SelectedValue = mode.Value;
            FanSpeedSelector.SelectedValue = fan.Value;
            OperatingModeSelector.IsEnabled = controls.CanControl;
            FanSpeedSelector.IsEnabled = controls.CanControl && controls.FanSpeedSupported;
            OperatingModeSelector.Tag = FindResource(PanelPresentation.ModeIconKey(mode.Value));
            PanelState.SetValue(OperatingModeSelector, mode.Value is { } m ? text["OperatingMode" + m] : "—");
            PanelState.SetValue(FanSpeedSelector, fan.Value is { } f ? text["FanSpeed" + f] : "—");
            PanelState.SetIsPending(OperatingModeSelector, mode.IsPending);
            PanelState.SetIsPending(FanSpeedSelector, fan.IsPending);
            Explain(OperatingModeSelector, mode.Value is null ? text["OperatingModeUnknownMessage"]
                : mode.Error is null ? null : text["CommandNotAppliedMessage"]);
            Explain(FanSpeedSelector, !controls.FanSpeedSupported || fan.Error == ControlError.Unsupported
                ? text["FanSpeedUnsupportedMessage"]
                : fan.Error is null ? null : text["CommandNotAppliedMessage"]);
            RenderFeature(TurboToggle, controls.TurboMode, controls.TurboModeSupported, "TurboModeUnsupportedMessage");
            RenderFeature(ComfortToggle, controls.ComfortMode, controls.ComfortModeSupported, "ComfortModeUnsupportedMessage");
            RenderFeature(EconomyToggle, controls.EconomyMode, controls.EconomyModeSupported, "EconomyModeUnsupportedMessage");
            PanelState.SetValue(TurboToggle, text[controls.TurboMode.Value ? "PowerOn" : "PowerOff"]);
            PanelState.SetValue(ComfortToggle, text[controls.ComfortMode.Value ? "AirflowComfort" : "AirflowNormal"]);
            var power = controls.Power;
            var temperature = controls.TargetTemperature;
            Bind(PowerToggle, AutomationProperties.NameProperty, power.Value ? "TurnOff" : "TurnOn");
            PowerToggle.IsChecked = power.Value;
            PowerToggle.IsEnabled = controls.CanControl;
            PanelState.SetIsPending(PowerToggle, power.IsPending);
            Explain(PowerToggle, power.Error is null ? null : text["CommandNotAppliedMessage"]);
            Illustration.IsOn = power.Value;
            Illustration.Mode = mode.Value;
            ControlsArea.Opacity = power.Value ? 1 : 0.6;
            var range = controls.TargetTemperatureRange;
            Explain(TemperatureSlider, range is null || temperature.Error == ControlError.Unsupported
                ? text["TargetTemperatureUnsupportedMessage"]
                : temperature.Error is null ? null : text["CommandNotAppliedMessage"]);
            Pulse(TargetValue, temperature.IsPending);
            Decrease.IsEnabled = Increase.IsEnabled = TemperatureSlider.IsEnabled = controls.CanControl && range is not null;
            RangeLabels.Visibility = range is null ? Visibility.Hidden : Visibility.Visible;
            if (range is not null)
            {
                TemperatureSlider.Minimum = range.Min; TemperatureSlider.Maximum = range.Max;
                TemperatureSlider.TickFrequency = TemperatureSlider.SmallChange = range.Step;
                RangeMin.Text = TemperatureText.FormatValue(range.Min) + "°";
                RangeMax.Text = TemperatureText.FormatValue(range.Max) + "°";
            }
            TemperatureSlider.Tag = FindResource(mode.Value == OperatingMode.Cool ? "SliderFillCool" : "SliderFillWarm");
            if (!dragging)
            {
                TemperatureSlider.Value = temperature.Value;
                ShowTarget(temperature.Value);
            }
            var notApplied = new[] { power.Error, temperature.Error, mode.Error, fan.Error, controls.TurboMode.Error,
                controls.ComfortMode.Error, controls.EconomyMode.Error }.Contains(ControlError.NotApplied);
            var pending = power.IsPending || temperature.IsPending || mode.IsPending || fan.IsPending
                || controls.TurboMode.IsPending || controls.ComfortMode.IsPending || controls.EconomyMode.IsPending;
            RenderFooter(PanelPresentation.Footer(notApplied, mode.Value is null, pending));
        }
        finally { rendering = false; }
    }
    private void RenderFeature(ToggleButton toggle, ControlValue<bool> value, bool supported, string unsupportedMessage)
    {
        toggle.IsChecked = value.Value;
        toggle.IsEnabled = controls.CanControl && supported;
        PanelState.SetIsPending(toggle, value.IsPending);
        Explain(toggle, !supported || value.Error == ControlError.Unsupported ? Localizer.Instance[unsupportedMessage]
            : value.Error == ControlError.NotApplied ? Localizer.Instance["CommandNotAppliedMessage"] : null);
    }
    // Explanations are shown as tooltips and exposed to assistive technology as help text.
    private static void Explain(FrameworkElement element, string? message)
    {
        element.ToolTip = message;
        AutomationProperties.SetHelpText(element, message ?? "");
    }
    private void RenderFooter(FooterStatus status)
    {
        Bind(FooterText, TextBlock.TextProperty, status.Key);
        FooterIcon.Tag = FindResource(status.Severity switch
        {
            FooterSeverity.Error => "Icon.Alert", FooterSeverity.Pending => "Icon.Sync", _ => "Icon.ShieldCheck"
        });
        FooterIcon.Foreground = ThemeBrush(status.Severity switch
        {
            FooterSeverity.Error => "SystemFillColorCriticalBrush", FooterSeverity.Pending => "TextFillColorSecondaryBrush",
            _ => "SystemFillColorSuccessBrush"
        });
        FooterText.Foreground = ThemeBrush(status.Severity == FooterSeverity.Error
            ? "SystemFillColorCriticalBrush" : "TextFillColorSecondaryBrush");
        FooterSpin.BeginAnimation(RotateTransform.AngleProperty, status.Severity == FooterSeverity.Pending && !ReduceMotion
            ? new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.2)) { RepeatBehavior = RepeatBehavior.Forever } : null);
    }
    private Brush ThemeBrush(string key) => TryFindResource(key) as Brush ?? Brushes.Gray;
    private void ShowTarget(double value)
    {
        var formatted = TemperatureText.FormatValue(value);
        if (formatted == shownTarget) return;
        var animate = shownTarget != "" && !ReduceMotion;
        shownTarget = TargetValue.Text = formatted;
        if (!animate) return;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        TargetOffset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease });
    }
    private static void Pulse(UIElement element, bool active) => element.BeginAnimation(OpacityProperty, active && !ReduceMotion
        ? new DoubleAnimation(1, 0.45, TimeSpan.FromMilliseconds(600)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever }
        : null);
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
    private async void ChangeTurboMode(object sender, RoutedEventArgs e) => await controls.SetTurboMode(!controls.TurboMode.Value);
    private async void ChangeComfortMode(object sender, RoutedEventArgs e) => await controls.SetComfortMode(!controls.ComfortMode.Value);
    private async void ChangeEconomyMode(object sender, RoutedEventArgs e) => await controls.SetEconomyMode(!controls.EconomyMode.Value);
    private async void ChangePower(object sender, RoutedEventArgs e) => await controls.SetPower(!controls.Power.Value);
    private async void DecreaseTemperature(object sender, RoutedEventArgs e) => await controls.StepTargetTemperature(-1);
    private async void IncreaseTemperature(object sender, RoutedEventArgs e) => await controls.StepTargetTemperature(1);
    private void TemperatureDragStarted(object sender, DragStartedEventArgs e) => dragging = true;
    private async void TemperatureDragCompleted(object sender, DragCompletedEventArgs e)
    {
        dragging = false;
        if (!e.Canceled) await controls.SetTargetTemperature(TemperatureSlider.Value);
        RenderControls();
    }
    private async void TemperatureChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (rendering || controls is null) return;
        ShowTarget(e.NewValue);
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

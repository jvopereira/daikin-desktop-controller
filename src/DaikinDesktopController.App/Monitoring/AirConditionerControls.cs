using DaikinDesktopController.Device;
namespace DaikinDesktopController.App.Monitoring;
public enum ControlError { Unsupported, NotApplied }
public sealed record ControlValue<T>(T Value, bool IsPending, ControlError? Error);
// All operations run on the UI context, like StatusMonitor.
public sealed class AirConditionerControls : IDisposable
{
    private readonly StatusMonitor monitor;
    private sealed class Slot<T>
    {
        public long Sequence;
        public T? Pending;
        public bool HasPending;
        public ControlError? Error;
        public ControlValue<T> Value(T confirmed) => new(HasPending ? Pending! : confirmed, HasPending, Error);
        public void Reset() { Sequence++; Pending = default; HasPending = false; Error = null; }
    }
    private readonly Slot<bool> turbo = new();
    private readonly Slot<bool> comfort = new();
    private readonly Slot<bool> economy = new();
    private readonly Slot<bool> power = new();
    private readonly Slot<double> temperature = new();
    private readonly Slot<OperatingMode?> mode = new();
    private readonly Slot<FanSpeed?> fan = new();
    private AirConditionerStatus? Confirmed => (monitor.State as Available)?.Status;
    public ControlValue<bool> Power => power.Value(Confirmed?.IsOn ?? false);
    public ControlValue<double> TargetTemperature => temperature.Value(Confirmed?.TargetTemperature ?? 0);
    public ControlValue<OperatingMode?> OperatingMode => mode.Value(Confirmed?.OperatingMode);
    public ControlValue<FanSpeed?> FanSpeed => fan.Value(Confirmed?.FanSpeed);
    public ControlValue<bool> TurboMode => turbo.Value(Confirmed?.TurboMode ?? false);
    public bool TurboModeSupported => Confirmed?.TurboMode is not null;
    public ControlValue<bool> ComfortMode => comfort.Value(Confirmed?.ComfortMode ?? false);
    public bool ComfortModeSupported => Confirmed?.ComfortMode is not null;
    public ControlValue<bool> EconomyMode => economy.Value(Confirmed?.EconomyMode ?? false);
    public bool EconomyModeSupported => Confirmed?.EconomyMode is not null;
    public bool FanSpeedSupported => Confirmed is { } status && new AirConditionerCommand.SetFanSpeed(Device.FanSpeed.Auto).IsSupportedBy(status);
    public TargetTemperatureRange? TargetTemperatureRange => Confirmed?.TargetTemperatureRange;
    public bool CanControl => monitor.CanSendCommands;
    public event Action? Changed;
    public AirConditionerControls(StatusMonitor monitor) { this.monitor = monitor; monitor.StateChanged += StateChanged; }
    private void StateChanged()
    {
        if (monitor.State is ReadingStatus or Unavailable || !CanControl)
        {
            power.Reset(); temperature.Reset(); mode.Reset(); fan.Reset(); turbo.Reset(); comfort.Reset(); economy.Reset();
        }
        Changed?.Invoke();
    }
    public Task SetTurboMode(bool value) => SetAsync(turbo, TurboMode.Value, value, new AirConditionerCommand.SetTurboMode(value));
    public Task SetComfortMode(bool value) => SetAsync(comfort, ComfortMode.Value, value, new AirConditionerCommand.SetComfortMode(value));
    public Task SetEconomyMode(bool value) => SetAsync(economy, EconomyMode.Value, value, new AirConditionerCommand.SetEconomyMode(value));
    public Task SetPower(bool value) => SetAsync(power, Power.Value, value, new AirConditionerCommand.SetPower(value));
    public Task SetTargetTemperature(double value) => SetAsync(temperature, TargetTemperature.Value, value, new AirConditionerCommand.SetTargetTemperature(value));
    public Task SetOperatingMode(OperatingMode value) => SetAsync(mode, OperatingMode.Value, (OperatingMode?)value, new AirConditionerCommand.SetOperatingMode(value));
    public Task SetFanSpeed(FanSpeed value) => SetAsync(fan, FanSpeed.Value, (FanSpeed?)value, new AirConditionerCommand.SetFanSpeed(value));
    public Task StepTargetTemperature(int direction)
    {
        var range = TargetTemperatureRange;
        return range is null ? Task.CompletedTask : SetTargetTemperature(Math.Clamp(TargetTemperature.Value + Math.Sign(direction) * range.Step, range.Min, range.Max));
    }
    private async Task SetAsync<T>(Slot<T> slot, T current, T value, AirConditionerCommand command)
    {
        if (!CanControl || Confirmed is not { } confirmed) return;
        if (EqualityComparer<T>.Default.Equals(value, current)) return;
        if (!command.IsSupportedBy(confirmed))
        {
            slot.Error = ControlError.Unsupported;
            Changed?.Invoke(); return;
        }
        var sequence = ++slot.Sequence;
        slot.Pending = value; slot.HasPending = true; slot.Error = null;
        Changed?.Invoke();
        await monitor.SendAsync(command);
        if (sequence != slot.Sequence) return;
        slot.Error = Confirmed is { } status && !command.IsSatisfiedBy(status) ? ControlError.NotApplied : null;
        slot.Pending = default; slot.HasPending = false;
        Changed?.Invoke();
    }
    public void Dispose() => monitor.StateChanged -= StateChanged;
}

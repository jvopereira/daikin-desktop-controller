using DaikinDesktopController.Device;
namespace DaikinDesktopController.App.Monitoring;
public enum ControlError { Unsupported, NotApplied }
public sealed record ControlValue<T>(T Value, bool IsPending, ControlError? Error);
// All operations run on the UI context, like StatusMonitor.
public sealed class AirConditionerControls : IDisposable
{
    private readonly StatusMonitor monitor;
    private long powerSequence, temperatureSequence;
    private bool? pendingPower;
    private double? pendingTemperature;
    private ControlError? powerError, temperatureError;
    private AirConditionerStatus? Confirmed => (monitor.State as Available)?.Status;
    public ControlValue<bool> Power => new(pendingPower ?? Confirmed?.IsOn ?? false, pendingPower.HasValue, powerError);
    public ControlValue<double> TargetTemperature => new(pendingTemperature ?? Confirmed?.TargetTemperature ?? 0, pendingTemperature.HasValue, temperatureError);
    public TargetTemperatureRange? TargetTemperatureRange => Confirmed?.TargetTemperatureRange;
    public bool CanControl => monitor.CanSendCommands;
    public event Action? Changed;
    public AirConditionerControls(StatusMonitor monitor) { this.monitor = monitor; monitor.StateChanged += StateChanged; }
    private void StateChanged()
    {
        if (monitor.State is ReadingStatus or Unavailable || !CanControl)
        {
            powerSequence++; temperatureSequence++;
            pendingPower = null; pendingTemperature = null; powerError = temperatureError = null;
        }
        Changed?.Invoke();
    }
    public Task SetPower(bool value) => SetAsync(new AirConditionerCommand.SetPower(value), true);
    public Task SetTargetTemperature(double value) => SetAsync(new AirConditionerCommand.SetTargetTemperature(value), false);
    public Task StepTargetTemperature(int direction)
    {
        var range = TargetTemperatureRange;
        return range is null ? Task.CompletedTask : SetTargetTemperature(Math.Clamp(TargetTemperature.Value + Math.Sign(direction) * range.Step, range.Min, range.Max));
    }
    private async Task SetAsync(AirConditionerCommand command, bool power)
    {
        if (!CanControl || Confirmed is not { } confirmed) return;
        if (power ? ((AirConditionerCommand.SetPower)command).IsOn == Power.Value
            : ((AirConditionerCommand.SetTargetTemperature)command).Celsius == TargetTemperature.Value) return;
        if (!command.IsSupportedBy(confirmed))
        {
            if (power) powerError = ControlError.Unsupported; else temperatureError = ControlError.Unsupported;
            Changed?.Invoke(); return;
        }
        var sequence = power ? ++powerSequence : ++temperatureSequence;
        if (power) { pendingPower = ((AirConditionerCommand.SetPower)command).IsOn; powerError = null; }
        else { pendingTemperature = ((AirConditionerCommand.SetTargetTemperature)command).Celsius; temperatureError = null; }
        Changed?.Invoke();
        await monitor.SendAsync(command);
        if (sequence != (power ? powerSequence : temperatureSequence)) return;
        var error = Confirmed is { } status && !command.IsSatisfiedBy(status) ? ControlError.NotApplied : (ControlError?)null;
        if (power) { pendingPower = null; powerError = error; }
        else { pendingTemperature = null; temperatureError = error; }
        Changed?.Invoke();
    }
    public void Dispose() => monitor.StateChanged -= StateChanged;
}

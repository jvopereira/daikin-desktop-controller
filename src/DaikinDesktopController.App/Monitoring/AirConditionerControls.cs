using DaikinDesktopController.Device;
namespace DaikinDesktopController.App.Monitoring;
public enum ControlError { Unsupported, NotApplied }
public sealed record ControlValue<T>(T Value, bool IsPending, ControlError? Error);
// All operations run on the UI context, like StatusMonitor.
public sealed class AirConditionerControls : IDisposable
{
    private readonly StatusMonitor monitor;
    private readonly TimeProvider clock;
    private readonly TimeSpan debounce;
    private readonly Dictionary<object, QueuedCommand> queued = new();
    private sealed record QueuedCommand(long Order, Func<Task> Send, Action Complete);
    private long order;
    private long lastChanged;
    private CancellationTokenSource? worker;
    private bool disposed;
    private bool? panelConfigured;
    private bool panelVisible;
    public bool HasWork => worker is not null || queued.Count != 0;
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
    private readonly Slot<bool> swing = new();
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
    public ControlValue<bool> Swing => swing.Value(Confirmed?.Swing ?? false);
    public bool SwingSupported => Confirmed?.Swing is not null;
    public bool FanSpeedSupported => Effective is { } status && new AirConditionerCommand.SetFanSpeed(Device.FanSpeed.Auto).IsSupportedBy(status);
    private AirConditionerStatus? Effective => Confirmed is { } status && OperatingMode.Value is { } value
        ? status.WithOperatingMode(value) : Confirmed;
    public TargetTemperatureRange? TargetTemperatureRange => Effective?.TargetTemperatureRange;
    public bool CanControl => monitor.CanSendCommands;
    public event Action? Changed;
    public AirConditionerControls(StatusMonitor monitor, TimeProvider? clock = null, TimeSpan? debounce = null)
    {
        this.monitor = monitor; this.clock = clock ?? TimeProvider.System;
        this.debounce = debounce ?? TimeSpan.FromMilliseconds(500);
        monitor.StateChanged += StateChanged;
    }
    public void SetPanelState(bool configured, bool visible)
    {
        panelConfigured = configured; panelVisible = visible;
        UpdateMonitoring();
    }
    private void UpdateMonitoring()
    {
        if (panelConfigured is not { } configured || disposed) return;
        if (configured && (panelVisible || HasWork)) monitor.Start();
        else monitor.Stop();
    }
    private void StateChanged()
    {
        if (monitor.State is ReadingStatus or Unavailable || !CanControl)
        {
            CancelPending();
        }
        Changed?.Invoke();
    }
    public Task SetTurboMode(bool value)
    {
        if (value && CanControl && TurboModeSupported && EconomyModeSupported)
            _ = SetEconomyMode(false);
        return SetAsync(turbo, TurboMode.Value, value, new AirConditionerCommand.SetTurboMode(value));
    }
    public Task SetComfortMode(bool value) => SetAsync(comfort, ComfortMode.Value, value, new AirConditionerCommand.SetComfortMode(value));
    public Task SetEconomyMode(bool value)
    {
        if (value && CanControl && EconomyModeSupported && TurboModeSupported)
            _ = SetTurboMode(false);
        return SetAsync(economy, EconomyMode.Value, value, new AirConditionerCommand.SetEconomyMode(value));
    }
    public Task SetSwing(bool value) => SetAsync(swing, Swing.Value, value, new AirConditionerCommand.SetSwing(value));
    public Task SetPower(bool value) => SetAsync(power, Power.Value, value, new AirConditionerCommand.SetPower(value));
    public Task SetTargetTemperature(double value) => SetAsync(temperature, TargetTemperature.Value, value, new AirConditionerCommand.SetTargetTemperature(value));
    public Task SetOperatingMode(OperatingMode value) => SetAsync(mode, OperatingMode.Value, (OperatingMode?)value, new AirConditionerCommand.SetOperatingMode(value));
    public Task SetFanSpeed(FanSpeed value) => SetAsync(fan, FanSpeed.Value, (FanSpeed?)value, new AirConditionerCommand.SetFanSpeed(value));
    public Task StepTargetTemperature(int direction)
    {
        var range = TargetTemperatureRange;
        return range is null ? Task.CompletedTask : SetTargetTemperature(Math.Clamp(TargetTemperature.Value + Math.Sign(direction) * range.Step, range.Min, range.Max));
    }
    private Task SetAsync<T>(Slot<T> slot, T current, T value, AirConditionerCommand command)
    {
        if (disposed || !CanControl || Effective is not { } confirmed) return Task.CompletedTask;
        if (EqualityComparer<T>.Default.Equals(value, current)) return Task.CompletedTask;
        if (!command.IsSupportedBy(confirmed))
        {
            slot.Error = ControlError.Unsupported;
            Changed?.Invoke(); return Task.CompletedTask;
        }
        var sequence = ++slot.Sequence;
        slot.Pending = value; slot.HasPending = true; slot.Error = null;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (queued.Remove(slot, out var previous)) previous.Complete();
        queued[slot] = new(++order, async () =>
        {
            if (Confirmed is { } latest && !command.IsSupportedBy(latest))
            {
                if (sequence == slot.Sequence)
                {
                    slot.Error = ControlError.Unsupported; slot.Pending = default; slot.HasPending = false;
                    Changed?.Invoke();
                }
                return;
            }
            if (Confirmed is { } before && !command.IsSatisfiedBy(before))
                await monitor.SendAsync(command);
            if (sequence != slot.Sequence) return;
            slot.Error = Confirmed is { } status && !command.IsSatisfiedBy(status) ? ControlError.NotApplied : null;
            slot.Pending = default; slot.HasPending = false;
            Changed?.Invoke();
        }, () => completion.TrySetResult());
        lastChanged = clock.GetTimestamp();
        if (worker is null)
        {
            var attempt = new CancellationTokenSource();
            worker = attempt;
            _ = RunAsync(attempt);
        }
        Changed?.Invoke();
        return completion.Task;
    }
    private async Task RunAsync(CancellationTokenSource attempt)
    {
        try
        {
            while (queued.Count != 0 && !attempt.IsCancellationRequested)
            {
                var remaining = debounce - clock.GetElapsedTime(lastChanged);
                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining, clock, attempt.Token);
                    continue;
                }
                var next = queued.MinBy(pair => pair.Value.Order);
                queued.Remove(next.Key);
                try { await next.Value.Send(); }
                finally { next.Value.Complete(); }
            }
        }
        catch (OperationCanceledException) when (attempt.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(worker, attempt)) worker = null;
            attempt.Dispose();
            UpdateMonitoring();
            Changed?.Invoke();
        }
    }
    private void CancelPending()
    {
        var previous = worker; worker = null; previous?.Cancel();
        foreach (var item in queued.Values) item.Complete();
        queued.Clear();
        power.Reset(); temperature.Reset(); mode.Reset(); fan.Reset(); turbo.Reset(); comfort.Reset(); economy.Reset(); swing.Reset();
    }
    public void Dispose()
    {
        disposed = true;
        monitor.StateChanged -= StateChanged;
        CancelPending();
    }
}

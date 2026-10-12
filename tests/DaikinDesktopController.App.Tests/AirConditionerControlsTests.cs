using DaikinDesktopController.App.Monitoring;
using DaikinDesktopController.Device;
using Xunit;
namespace DaikinDesktopController.App.Tests;
public sealed class AirConditionerControlsTests
{
    [Fact] public async Task PendingIsImmediateAndOlderCompletionCannotClearNewerIntent()
    {
        using var f = new StatusMonitorTests.Fixture();
        var status = new AirConditionerStatus(true, "3", 22, null, null);
        f.Client.Read = _ => Task.FromResult(new StatusRead(StatusReadOutcome.Succeeded, status));
        f.Monitor.Start(); using var controls = new AirConditionerControls(f.Monitor);
        var first = new TaskCompletionSource<CommandResult>();
        var second = new TaskCompletionSource<CommandResult>();
        f.Client.Command = _ => f.Client.CommandCalls == 1 ? first.Task : second.Task;
        var a = controls.StepTargetTemperature(1); var b = controls.StepTargetTemperature(1);
        Assert.Equal(24, controls.TargetTemperature.Value); Assert.True(controls.TargetTemperature.IsPending);
        status = status with { TargetTemperature = 23 }; first.SetResult(new(CommandOutcome.Confirmed, status));
        // The second command holds the gate while the first refresh waits.
        Assert.True(controls.TargetTemperature.IsPending); Assert.Equal(24, controls.TargetTemperature.Value);
        status = status with { TargetTemperature = 24 }; second.SetResult(new(CommandOutcome.Confirmed, status));
        await Task.WhenAll(a, b);
        Assert.Equal(2, f.Client.CommandCalls); Assert.False(controls.TargetTemperature.IsPending);
        Assert.Equal(24, controls.TargetTemperature.Value); Assert.Null(controls.TargetTemperature.Error);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task TimeoutRefreshesWithoutResendingAndReconciles(bool applied)
    {
        using var f = new StatusMonitorTests.Fixture(); f.Monitor.Start();
        using var controls = new AirConditionerControls(f.Monitor);
        f.Client.Command = _ => Task.FromResult(new CommandResult(CommandOutcome.NoResponse));
        f.Client.Read = _ => Task.FromResult(new StatusRead(StatusReadOutcome.Succeeded, new(!applied, "3", 24, null, null)));
        await controls.SetPower(false);
        Assert.Equal(1, f.Client.CommandCalls); Assert.Equal(2, f.Client.Calls);
        Assert.Equal(applied ? null : ControlError.NotApplied, controls.Power.Error);
    }
    [Fact] public async Task EqualAndUnsupportedValuesSendNothing()
    {
        using var f = new StatusMonitorTests.Fixture(); f.Monitor.Start();
        using var controls = new AirConditionerControls(f.Monitor);
        await controls.SetPower(true); await controls.SetTargetTemperature(24);
        await controls.SetTargetTemperature(25);
        Assert.Equal(ControlError.Unsupported, controls.TargetTemperature.Error);
        Assert.Equal(0, f.Client.CommandCalls);
    }
    [Fact] public async Task RejectionShowsConfirmedValueAndNewIntentClearsError()
    {
        using var f = new StatusMonitorTests.Fixture(); f.Monitor.Start();
        using var controls = new AirConditionerControls(f.Monitor);
        f.Client.Command = _ => Task.FromResult(new CommandResult(CommandOutcome.NotApplied, new(true, "3", 24, null, null)));
        await controls.SetPower(false);
        Assert.True(controls.Power.Value); Assert.Equal(ControlError.NotApplied, controls.Power.Error);
        var reply = new TaskCompletionSource<CommandResult>();
        f.Client.Command = _ => reply.Task;
        var retry = controls.SetPower(false);
        Assert.Null(controls.Power.Error); Assert.True(controls.Power.IsPending);
        reply.SetResult(new(CommandOutcome.NotApplied)); await retry;
    }
    [Fact] public async Task TemperatureAtBoundDoesNotSend()
    {
        using var f = new StatusMonitorTests.Fixture();
        f.Client.Read = _ => Task.FromResult(new StatusRead(StatusReadOutcome.Succeeded, new(true, "3", 32, null, null)));
        f.Monitor.Start(); using var controls = new AirConditionerControls(f.Monitor);
        await controls.StepTargetTemperature(1); Assert.Equal(0, f.Client.CommandCalls);
    }
    [Fact] public async Task UnavailableClearsPendingAndBlocksSends()
    {
        using var f = new StatusMonitorTests.Fixture(); f.Monitor.Start();
        using var controls = new AirConditionerControls(f.Monitor);
        f.Secrets.Fail = true; await f.Monitor.RefreshAsync(); await controls.SetPower(false);
        Assert.False(controls.CanControl); Assert.False(controls.Power.IsPending);
        Assert.Null(controls.Power.Error); Assert.Equal(0, f.Client.CommandCalls);
    }
    private static AirConditionerStatus Initial => new(true, "3", 24, null, null, "17");
    private static AirConditionerStatus Applied(bool fan, bool second = false) => fan
        ? Initial with { Fan = second ? "7" : "5" }
        : Initial with { Mode = second ? "6" : "4" };
    private static Task Change(AirConditionerControls controls, bool fan, bool second = false) => fan
        ? controls.SetFanSpeed(second ? Device.FanSpeed.High : Device.FanSpeed.Medium)
        : controls.SetOperatingMode(second ? Device.OperatingMode.Fan : Device.OperatingMode.Heat);
    private static ControlValue<int?> Value(AirConditionerControls controls, bool fan) => fan
        ? new((int?)controls.FanSpeed.Value, controls.FanSpeed.IsPending, controls.FanSpeed.Error)
        : new((int?)controls.OperatingMode.Value, controls.OperatingMode.IsPending, controls.OperatingMode.Error);
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task ModeAndFanKeepNewerPendingIntent(bool fan)
    {
        using var f = new StatusMonitorTests.Fixture(); var status = Initial;
        f.Client.Read = _ => Task.FromResult(new StatusRead(StatusReadOutcome.Succeeded, status));
        f.Monitor.Start(); using var controls = new AirConditionerControls(f.Monitor);
        var first = new TaskCompletionSource<CommandResult>(); var second = new TaskCompletionSource<CommandResult>();
        var entered = new TaskCompletionSource();
        f.Client.Command = _ => { if (f.Client.CommandCalls == 1) return first.Task; entered.SetResult(); return second.Task; };
        var a = Change(controls, fan);
        Assert.True(Value(controls, fan).IsPending);
        Assert.Equal(fan ? (int)Device.FanSpeed.Medium : (int)Device.OperatingMode.Heat, Value(controls, fan).Value);
        var b = Change(controls, fan, true);
        status = Applied(fan); first.SetResult(new(CommandOutcome.Confirmed, status));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(Value(controls, fan).IsPending);
        Assert.Equal(fan ? (int)Device.FanSpeed.High : (int)Device.OperatingMode.Fan, Value(controls, fan).Value);
        status = Applied(fan, true); second.SetResult(new(CommandOutcome.Confirmed, status));
        await Task.WhenAll(a, b);
        Assert.Equal(2, f.Client.CommandCalls); Assert.False(Value(controls, fan).IsPending); Assert.Null(Value(controls, fan).Error);
    }
    [Theory] [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task ModeAndFanTimeoutRefreshesOnce(bool fan, bool applied)
    {
        using var f = new StatusMonitorTests.Fixture(); var status = Initial;
        f.Client.Read = _ => Task.FromResult(new StatusRead(StatusReadOutcome.Succeeded, status));
        f.Monitor.Start(); using var controls = new AirConditionerControls(f.Monitor);
        f.Client.Command = _ => { if (applied) status = Applied(fan); return Task.FromResult(new CommandResult(CommandOutcome.NoResponse)); };
        await Change(controls, fan);
        Assert.Equal(1, f.Client.CommandCalls); Assert.Equal(2, f.Client.Calls);
        Assert.False(Value(controls, fan).IsPending);
        Assert.Equal(applied ? null : ControlError.NotApplied, Value(controls, fan).Error);
        Assert.Equal(fan ? (int?)status.FanSpeed : (int?)status.OperatingMode, Value(controls, fan).Value);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task ModeAndFanRejectionRestoresConfirmedAndRetryClearsError(bool fan)
    {
        using var f = new StatusMonitorTests.Fixture();
        f.Client.Read = _ => Task.FromResult(new StatusRead(StatusReadOutcome.Succeeded, Initial));
        f.Monitor.Start(); using var controls = new AirConditionerControls(f.Monitor);
        f.Client.Command = _ => Task.FromResult(new CommandResult(CommandOutcome.NotApplied, Initial));
        await Change(controls, fan);
        Assert.Equal(fan ? (int?)Initial.FanSpeed : (int?)Initial.OperatingMode, Value(controls, fan).Value);
        Assert.Equal(ControlError.NotApplied, Value(controls, fan).Error);
        var reply = new TaskCompletionSource<CommandResult>(); f.Client.Command = _ => reply.Task;
        var retry = Change(controls, fan);
        Assert.True(Value(controls, fan).IsPending); Assert.Null(Value(controls, fan).Error);
        f.Secrets.Fail = true; reply.SetResult(new(CommandOutcome.NoResponse)); await retry;
        Assert.False(Value(controls, fan).IsPending); Assert.Null(Value(controls, fan).Error);
        await Change(controls, fan); Assert.Equal(2, f.Client.CommandCalls);
    }
    [Fact] public async Task ModeAndFanEqualValuesAndDryFanSendNothing()
    {
        using var f = new StatusMonitorTests.Fixture(); var status = Initial;
        f.Client.Read = _ => Task.FromResult(new StatusRead(StatusReadOutcome.Succeeded, status));
        f.Monitor.Start(); using var controls = new AirConditionerControls(f.Monitor);
        await controls.SetOperatingMode(Device.OperatingMode.Cool); await controls.SetFanSpeed(Device.FanSpeed.Auto);
        Assert.Equal(0, f.Client.CommandCalls);
        status = Initial with { Mode = "2" }; await f.Monitor.RefreshAsync();
        Assert.False(controls.FanSpeedSupported); await controls.SetFanSpeed(Device.FanSpeed.High);
        Assert.Equal(ControlError.Unsupported, controls.FanSpeed.Error); Assert.Equal(0, f.Client.CommandCalls);
    }
    [Fact] public void UnknownCodesHaveNoSelectedValue()
    {
        using var f = new StatusMonitorTests.Fixture();
        f.Client.Read = _ => Task.FromResult(new StatusRead(StatusReadOutcome.Succeeded, Initial with { Mode = "99", Fan = "99" }));
        f.Monitor.Start(); using var controls = new AirConditionerControls(f.Monitor);
        Assert.Null(controls.OperatingMode.Value); Assert.Null(controls.FanSpeed.Value); Assert.False(controls.FanSpeedSupported);
    }
}

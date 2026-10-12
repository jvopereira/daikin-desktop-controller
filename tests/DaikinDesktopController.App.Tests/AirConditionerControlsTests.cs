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
}

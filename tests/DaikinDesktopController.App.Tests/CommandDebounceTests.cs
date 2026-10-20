using DaikinDesktopController.App.Monitoring;
using DaikinDesktopController.Device;
using Xunit;

namespace DaikinDesktopController.App.Tests;

public sealed class CommandDebounceTests
{
    private static AirConditionerStatus Initial => new(true, "3", 24, null, null, "17", false, false, false, false);

    private static void ApplyCommands(StatusMonitorTests.Fixture f)
    {
        var status = Initial;
        f.Client.Read = _ => Task.FromResult(new StatusRead(StatusReadOutcome.Succeeded, status));
        f.Client.Command = _ =>
        {
            status = f.Client.Commands.Last() switch
            {
                AirConditionerCommand.SetTargetTemperature t => status with { TargetTemperature = t.Celsius },
                AirConditionerCommand.SetPower p => status with { IsOn = p.IsOn },
                AirConditionerCommand.SetSwing s => status with { Swing = s.IsOn },
                AirConditionerCommand.SetTurboMode t => status with { TurboMode = t.IsOn, EconomyMode = t.IsOn ? false : status.EconomyMode },
                AirConditionerCommand.SetEconomyMode e => status with { EconomyMode = e.IsOn, TurboMode = e.IsOn ? false : status.TurboMode },
                AirConditionerCommand.SetOperatingMode m => status.WithOperatingMode(m.Mode),
                _ => status
            };
            return Task.FromResult(new CommandResult(CommandOutcome.Confirmed, status));
        };
        f.Monitor.Start();
    }

    [Fact] public async Task RapidChangesResetGlobalDelayAndSendOnlyFinalValues()
    {
        using var f = new StatusMonitorTests.Fixture(); ApplyCommands(f);
        using var controls = new AirConditionerControls(f.Monitor, f.Clock);
        var first = controls.StepTargetTemperature(1);
        Assert.Equal(25, controls.TargetTemperature.Value); Assert.True(controls.TargetTemperature.IsPending);
        f.Clock.Advance(TimeSpan.FromMilliseconds(499));
        var second = controls.StepTargetTemperature(1);
        var swing = controls.SetSwing(true);
        f.Clock.Advance(TimeSpan.FromMilliseconds(499));
        Assert.Empty(f.Client.Commands);
        f.Clock.Advance(TimeSpan.FromMilliseconds(1));
        await Task.WhenAll(first, second, swing).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal<AirConditionerCommand>([new AirConditionerCommand.SetTargetTemperature(26), new AirConditionerCommand.SetSwing(true)], f.Client.Commands);
        Assert.False(controls.TargetTemperature.IsPending); Assert.False(controls.HasWork);
    }

    [Fact] public async Task ReturningToConfirmedValueSendsNothingIncludingPower()
    {
        using var f = new StatusMonitorTests.Fixture(); ApplyCommands(f);
        using var controls = new AirConditionerControls(f.Monitor, f.Clock);
        var off = controls.SetPower(false); var on = controls.SetPower(true);
        f.Clock.Advance(TimeSpan.FromMilliseconds(500));
        await Task.WhenAll(off, on).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(f.Client.Commands); Assert.False(controls.Power.IsPending);
    }

    [Fact] public async Task DifferentControlsAreSentInOrderOfTheirLastChanges()
    {
        using var f = new StatusMonitorTests.Fixture(); ApplyCommands(f);
        using var controls = new AirConditionerControls(f.Monitor, f.Clock);
        var first = controls.SetTargetTemperature(25);
        var power = controls.SetPower(false);
        var latest = controls.SetTargetTemperature(26);
        f.Clock.Advance(TimeSpan.FromMilliseconds(500));
        await Task.WhenAll(first, power, latest).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal<AirConditionerCommand>([new AirConditionerCommand.SetPower(false), new AirConditionerCommand.SetTargetTemperature(26)], f.Client.Commands);
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task DebouncedTimeoutReadsStatusWithoutResending(bool applied)
    {
        using var f = new StatusMonitorTests.Fixture(); ApplyCommands(f);
        using var controls = new AirConditionerControls(f.Monitor, f.Clock);
        f.Client.Command = _ =>
        {
            f.Client.Read = _ => Task.FromResult(new StatusRead(StatusReadOutcome.Succeeded, Initial with { Swing = applied }));
            return Task.FromResult(new CommandResult(CommandOutcome.NoResponse));
        };
        var pending = controls.SetSwing(true);
        f.Clock.Advance(TimeSpan.FromMilliseconds(500));
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Single(f.Client.Commands); Assert.Equal(2, f.Client.Calls);
        Assert.Equal(applied, controls.Swing.Value);
        Assert.Equal(applied ? null : ControlError.NotApplied, controls.Swing.Error);
    }

    [Fact] public async Task DisposeDiscardsUnsentCommands()
    {
        using var f = new StatusMonitorTests.Fixture(); ApplyCommands(f);
        var controls = new AirConditionerControls(f.Monitor, f.Clock);
        var pending = controls.SetSwing(true); controls.Dispose();
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        f.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Empty(f.Client.Commands); Assert.False(controls.HasWork);
    }

    [Fact] public async Task InFlightReplyCannotClearNewerIntentAndIntermediateValuesAreDropped()
    {
        using var f = new StatusMonitorTests.Fixture(); ApplyCommands(f);
        using var controls = new AirConditionerControls(f.Monitor, f.Clock);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reply = new TaskCompletionSource<CommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Client.Command = _ => { entered.TrySetResult(); return reply.Task; };
        var first = controls.SetTargetTemperature(25);
        f.Clock.Advance(TimeSpan.FromMilliseconds(500)); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = controls.SetTargetTemperature(26); var third = controls.SetTargetTemperature(27);
        Assert.Equal(27, controls.TargetTemperature.Value);
        reply.SetResult(new(CommandOutcome.Confirmed, Initial with { TargetTemperature = 25 }));
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(controls.TargetTemperature.IsPending); Assert.Equal(27, controls.TargetTemperature.Value);
        f.Clock.Advance(TimeSpan.FromMilliseconds(500));
        await Task.WhenAll(second, third).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal<AirConditionerCommand>([new AirConditionerCommand.SetTargetTemperature(25), new AirConditionerCommand.SetTargetTemperature(27)], f.Client.Commands);
    }

    [Fact] public async Task StopCancelsDelayedCommandsAndClearsPending()
    {
        using var f = new StatusMonitorTests.Fixture(); ApplyCommands(f);
        using var controls = new AirConditionerControls(f.Monitor, f.Clock);
        var pending = controls.SetSwing(true); f.Monitor.Stop();
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        f.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Empty(f.Client.Commands); Assert.False(controls.Swing.IsPending); Assert.False(controls.HasWork);
    }

    [Fact] public async Task LastExclusiveFeatureWinsBeforeSending()
    {
        using var f = new StatusMonitorTests.Fixture(); ApplyCommands(f);
        using var controls = new AirConditionerControls(f.Monitor, f.Clock);
        var turbo = controls.SetTurboMode(true); var economy = controls.SetEconomyMode(true);
        Assert.False(controls.TurboMode.Value); Assert.True(controls.EconomyMode.Value);
        f.Clock.Advance(TimeSpan.FromMilliseconds(500));
        await Task.WhenAll(turbo, economy).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal<AirConditionerCommand>([new AirConditionerCommand.SetEconomyMode(true)], f.Client.Commands);
    }

    [Fact] public async Task PendingDryModeDisablesFanChangesImmediately()
    {
        using var f = new StatusMonitorTests.Fixture(); ApplyCommands(f);
        using var controls = new AirConditionerControls(f.Monitor, f.Clock);
        var mode = controls.SetOperatingMode(OperatingMode.Dry);
        var fan = controls.SetFanSpeed(FanSpeed.High);
        Assert.False(controls.FanSpeedSupported);
        f.Clock.Advance(TimeSpan.FromMilliseconds(500));
        await Task.WhenAll(mode, fan).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal<AirConditionerCommand>([new AirConditionerCommand.SetOperatingMode(OperatingMode.Dry)], f.Client.Commands);
        Assert.Equal(ControlError.Unsupported, controls.FanSpeed.Error);
    }

    [Fact] public async Task HiddenPanelFinishesPendingWorkThenStopsMonitoring()
    {
        using var f = new StatusMonitorTests.Fixture(); ApplyCommands(f);
        using var controls = new AirConditionerControls(f.Monitor, f.Clock);
        controls.SetPanelState(configured: true, visible: true);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Monitor.StateChanged += () => { if (!f.Monitor.CanSendCommands) stopped.TrySetResult(); };
        var pending = controls.SetSwing(true);
        controls.SetPanelState(configured: true, visible: false);
        Assert.True(f.Monitor.CanSendCommands);
        f.Clock.Advance(TimeSpan.FromMilliseconds(500));
        await Task.WhenAll(pending, stopped.Task).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal<AirConditionerCommand>([new AirConditionerCommand.SetSwing(true)], f.Client.Commands);
        Assert.False(f.Monitor.CanSendCommands);
        var reads = f.Client.Calls;
        f.Clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(reads, f.Client.Calls);
    }

    [Fact] public async Task ReopeningWhilePendingKeepsSessionAlive()
    {
        using var f = new StatusMonitorTests.Fixture(); ApplyCommands(f);
        using var controls = new AirConditionerControls(f.Monitor, f.Clock);
        controls.SetPanelState(configured: true, visible: true);
        var pending = controls.SetSwing(true);
        controls.SetPanelState(configured: true, visible: false);
        controls.SetPanelState(configured: true, visible: true);
        Assert.Equal(1, f.Client.Calls);
        f.Clock.Advance(TimeSpan.FromMilliseconds(500));
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(f.Monitor.CanSendCommands); Assert.Equal(2, f.Client.Calls);
    }

    [Fact] public async Task ChangedReportedModeRejectsQueuedFanBeforeSending()
    {
        using var f = new StatusMonitorTests.Fixture(); ApplyCommands(f);
        using var controls = new AirConditionerControls(f.Monitor, f.Clock);
        var fan = controls.SetFanSpeed(FanSpeed.High);
        f.Client.Read = _ => Task.FromResult(new StatusRead(StatusReadOutcome.Succeeded, Initial.WithOperatingMode(OperatingMode.Dry)));
        await f.Monitor.RefreshAsync();
        f.Clock.Advance(TimeSpan.FromMilliseconds(500));
        await fan.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Empty(f.Client.Commands); Assert.Equal(ControlError.Unsupported, controls.FanSpeed.Error);
    }

    [Fact] public async Task UnconfiguredPanelCancelsPendingWork()
    {
        using var f = new StatusMonitorTests.Fixture(); ApplyCommands(f);
        using var controls = new AirConditionerControls(f.Monitor, f.Clock);
        controls.SetPanelState(configured: true, visible: true);
        var pending = controls.SetSwing(true);
        controls.SetPanelState(configured: false, visible: true);
        await pending.WaitAsync(TimeSpan.FromSeconds(5));
        f.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Empty(f.Client.Commands); Assert.False(controls.HasWork);
    }
}

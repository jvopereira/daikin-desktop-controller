using System.Globalization;
using System.IO;
using System.Net;
using System.Threading.Channels;
using DaikinDesktopController.App.Configuration;
using DaikinDesktopController.App.Monitoring;
using DaikinDesktopController.App.Preferences;
using DaikinDesktopController.App.Secrets;
using DaikinDesktopController.Device;
using Microsoft.Extensions.Time.Testing;
using Xunit;
namespace DaikinDesktopController.App.Tests;
public sealed class StatusMonitorTests
{
    [Fact] public void StartReadsImmediatelyAndBecomesAvailable()
    {
        using var f = new Fixture();
        f.Monitor.Start();
        Assert.IsType<Available>(f.Monitor.State);
        Assert.True(f.Monitor.CanSendCommands);
        Assert.Equal(1, f.Client.Calls);
    }
    [Theory]
    [InlineData(5)] [InlineData(10)] [InlineData(30)] [InlineData(60)]
    public async Task PollsEveryStatusRefreshInterval(int seconds)
    {
        using var f = new Fixture(seconds);
        f.Monitor.Start(); await f.Client.NextRead();
        f.Clock.Advance(TimeSpan.FromSeconds(seconds - 0.1));
        Assert.Equal(1, f.Client.Calls);
        f.Clock.Advance(TimeSpan.FromSeconds(0.1));
        await f.Client.NextRead();
        Assert.Equal(2, f.Client.Calls);
    }
    [Fact] public async Task ChangingIntervalAppliesToRunningMonitor()
    {
        using var f = new Fixture();
        f.Monitor.Start(); await f.Client.NextRead(); await f.Clock.NextTimer();
        f.Clock.Advance(TimeSpan.FromSeconds(4));
        f.Preferences.Update(p => p with { StatusRefreshIntervalSeconds = 30 });
        await f.Clock.NextTimer();
        f.Clock.Advance(TimeSpan.FromSeconds(29.9)); Assert.Equal(1, f.Client.Calls);
        f.Clock.Advance(TimeSpan.FromSeconds(0.1)); await f.Client.NextRead();
        Assert.Equal(2, f.Client.Calls);
    }
    [Fact] public async Task FailedReadRetriesOnceAfterDelayAndRecovers()
    {
        using var f = new Fixture();
        f.Client.Read = _ => Task.FromResult(f.Client.Calls == 1 ? new StatusRead(StatusReadOutcome.Unreachable) : Success());
        var unavailable = false;
        f.Monitor.StateChanged += () => unavailable |= f.Monitor.State is Unavailable;
        var refresh = f.Monitor.RefreshAsync();
        f.Clock.Advance(TimeSpan.FromSeconds(1.9)); Assert.Equal(1, f.Client.Calls);
        f.Clock.Advance(TimeSpan.FromSeconds(0.1)); await refresh;
        Assert.Equal(2, f.Client.Calls); Assert.IsType<Available>(f.Monitor.State); Assert.False(unavailable);
    }
    [Theory]
    [InlineData(StatusReadOutcome.Unreachable, UnavailableReason.Unreachable)]
    [InlineData(StatusReadOutcome.KeyRejected, UnavailableReason.KeyRejected)]
    [InlineData(StatusReadOutcome.UnexpectedResponse, UnavailableReason.ReadError)]
    [InlineData(StatusReadOutcome.Succeeded, UnavailableReason.ReadError)]
    public async Task SecondFailureMarksUnavailableAndKeepsConfiguration(StatusReadOutcome outcome, UnavailableReason reason)
    {
        using var f = new Fixture();
        var saved = f.Configuration.Load();
        f.Client.Read = _ => Task.FromResult(new StatusRead(outcome));
        var refresh = f.Monitor.RefreshAsync(); f.Clock.Advance(TimeSpan.FromSeconds(2)); await refresh;
        Assert.Equal(new Unavailable(reason), f.Monitor.State); Assert.Equal(2, f.Client.Calls);
        Assert.Equal(saved, f.Configuration.Load()); Assert.False(StartupDecision.ShouldOpenPanel(f.Configuration));
    }
    [Fact] public async Task StorageFailureMarksUnavailable()
    {
        using var f = new Fixture(); f.Secrets.Fail = true;
        await f.Monitor.RefreshAsync();
        Assert.Equal(new Unavailable(UnavailableReason.StorageFailure), f.Monitor.State); Assert.Equal(0, f.Client.Calls);
    }
    [Fact] public async Task UnavailableRejectsCommandsWithoutQueuingAndRecoveryReenablesCommands()
    {
        using var f = new Fixture(); f.Secrets.Fail = true;
        await f.Monitor.RefreshAsync();
        Assert.Null(await f.Monitor.SendAsync(new AirConditionerCommand.SetPower(false)));
        f.Secrets.Fail = false; await f.Monitor.RefreshAsync();
        Assert.True(f.Monitor.CanSendCommands); Assert.Equal(0, f.Client.CommandCalls);
        Assert.NotNull(await f.Monitor.SendAsync(new AirConditionerCommand.SetPower(false)));
        Assert.Equal(1, f.Client.CommandCalls);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task CommandRefreshesImmediatelyAndIsNotResent(bool timeout)
    {
        using var f = new Fixture(); f.Monitor.Start();
        f.Client.Command = _ => Task.FromResult(new CommandResult(timeout ? CommandOutcome.NoResponse : CommandOutcome.Confirmed));
        Assert.NotNull(await f.Monitor.SendAsync(new AirConditionerCommand.SetPower(false)));
        Assert.Equal(1, f.Client.CommandCalls); Assert.Equal(2, f.Client.Calls); Assert.IsType<Available>(f.Monitor.State);
    }

    [Fact] public async Task StopCancelsPollingAndIgnoresLateResults()
    {
        using var f = new Fixture(); var completion = new TaskCompletionSource<StatusRead>(); CancellationToken token = default;
        f.Client.Read = ct => { token = ct; return completion.Task; };
        var refresh = f.Monitor.RefreshAsync(); f.Monitor.Stop();
        Assert.True(token.IsCancellationRequested); Assert.False(f.Monitor.CanSendCommands);
        completion.SetResult(Success()); await refresh;
        Assert.IsType<ReadingStatus>(f.Monitor.State);
        f.Clock.Advance(TimeSpan.FromMinutes(2)); Assert.Equal(1, f.Client.Calls);
    }
    [Fact] public async Task RestartIgnoresLateResultsFromPreviousRead()
    {
        using var f = new Fixture(); var completion = new TaskCompletionSource<StatusRead>();
        f.Client.Read = _ => completion.Task;
        var first = f.Monitor.RefreshAsync();
        f.Client.Read = _ => Task.FromResult(Success()); var second = f.Monitor.RefreshAsync();
        completion.SetResult(new(StatusReadOutcome.KeyRejected)); await first; await second;
        Assert.IsType<Available>(f.Monitor.State); Assert.Equal(2, f.Client.Calls);
    }
    [Fact] public async Task PollingRecoversAutomaticallyAndRetainsLastStatusDuringRead()
    {
        using var f = new Fixture(); f.Monitor.Start(); await f.Client.NextRead();
        var last = f.Monitor.State;
        var completion = new TaskCompletionSource<StatusRead>();
        f.Client.Read = _ => completion.Task;
        var refresh = f.Monitor.RefreshAsync();
        Assert.Equal(last, f.Monitor.State);
        completion.SetResult(new(StatusReadOutcome.Unreachable));
        await f.Clock.NextTimer(); // initial polling wait
        await f.Clock.NextTimer(); // retry delay
        f.Clock.Advance(TimeSpan.FromSeconds(2)); await refresh;
        Assert.IsType<Unavailable>(f.Monitor.State);
        await f.Clock.NextTimer(); // polling wait after failure
        f.Client.Read = _ => Task.FromResult(Success());
        await f.Client.NextRead(); await f.Client.NextRead(); // failed read and retry
        f.Clock.Advance(TimeSpan.FromSeconds(10)); await f.Client.NextRead();
        Assert.IsType<Available>(f.Monitor.State); Assert.True(f.Monitor.CanSendCommands);
    }
    [Fact] public async Task StopDuringCommandDoesNotRestartMonitoring()
    {
        using var f = new Fixture(); f.Monitor.Start();
        var completion = new TaskCompletionSource<CommandResult>();
        f.Client.Command = _ => completion.Task;
        var command = f.Monitor.SendAsync(new AirConditionerCommand.SetPower(false));
        f.Monitor.Stop(); completion.SetResult(new(CommandOutcome.NoResponse)); await command;
        f.Clock.Advance(TimeSpan.FromSeconds(60)); Assert.Equal(1, f.Client.Calls);
        Assert.False(f.Monitor.CanSendCommands);
    }
    [Fact] public async Task CommandsKeepSessionTokenAndDeviceCallsNeverOverlap()
    {
        using var f = new Fixture(); f.Monitor.Start();
        var active = 0; var maximum = 0; CancellationToken firstToken = default;
        var firstReply = new TaskCompletionSource<CommandResult>();
        var secondReply = new TaskCompletionSource<CommandResult>();
        var secondEntered = new TaskCompletionSource();
        f.Client.Command = async ct =>
        {
            active++; maximum = Math.Max(maximum, active);
            var number = f.Client.CommandCalls;
            if (number == 1) firstToken = ct; else secondEntered.SetResult();
            var result = await (number == 1 ? firstReply.Task : secondReply.Task);
            active--; return result;
        };
        f.Client.Read = _ =>
        {
            active++; maximum = Math.Max(maximum, active); active--;
            return Task.FromResult(Success());
        };
        var first = f.Monitor.SendAsync(new AirConditionerCommand.SetPower(false));
        var second = f.Monitor.SendAsync(new AirConditionerCommand.SetPower(true));
        Assert.Equal(1, f.Client.CommandCalls); Assert.False(firstToken.IsCancellationRequested);
        firstReply.SetResult(new(CommandOutcome.Confirmed, new(false, "3", 24, null, null)));
        await secondEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(firstToken.IsCancellationRequested);
        secondReply.SetResult(new(CommandOutcome.Confirmed, new(true, "3", 24, null, null)));
        await Task.WhenAll(first, second);
        Assert.Equal(1, maximum); Assert.Equal(2, f.Client.CommandCalls);
    }
    [Fact] public async Task StopCancelsScheduledPollingAndDisposeUnsubscribes()
    {
        using var f = new Fixture(); f.Monitor.Start();
        f.Monitor.Dispose();
        f.Preferences.Update(p => p with { StatusRefreshIntervalSeconds = 5 });
        f.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(1, f.Client.Calls);
        Assert.Null(await f.Monitor.SendAsync(new AirConditionerCommand.SetPower(false)));
    }
    [Fact] public void StartIsIdempotentWhileRunning()
    {
        using var f = new Fixture(); f.Monitor.Start(); f.Monitor.Start(); Assert.Equal(1, f.Client.Calls);
    }
    [Theory]
    [InlineData("en", 24, "24 °C")] [InlineData("en", 24.5, "24.5 °C")]
    [InlineData("pt-BR", 24, "24 °C")] [InlineData("pt-BR", 24.5, "24,5 °C")]
    public void TemperatureUsesDisplayLanguage(string language, double value, string expected)
    {
        var previous = CultureInfo.CurrentUICulture;
        try { CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language); Assert.Equal(expected, TemperatureText.Format(value)); }
        finally { CultureInfo.CurrentUICulture = previous; }
    }
    private static StatusRead Success() => new(StatusReadOutcome.Succeeded, new(true, "2", 24, 26, "1.0"));
    internal sealed class Fixture : IDisposable
    {
        public TrackingClock Clock { get; } = new();
        public Secrets Secrets { get; } = new();
        public Configuration Configuration { get; } = new();
        public Client Client { get; } = new();
        public PreferencesSession Preferences { get; }
        public StatusMonitor Monitor { get; }
        public Fixture(int seconds = 10)
        {
            Preferences = new(new Store(seconds)); Monitor = new(Client, Secrets, Configuration, Preferences, Clock);
        }
        public void Dispose() => Monitor.Dispose();
    }
    internal sealed class TrackingClock : TimeProvider
    {
        private readonly FakeTimeProvider inner = new();
        private readonly Channel<bool> timers = Channel.CreateUnbounded<bool>();
        public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();
        public override long GetTimestamp() => inner.GetTimestamp();
        public override long TimestampFrequency => inner.TimestampFrequency;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = inner.CreateTimer(callback, state, dueTime, period);
            timers.Writer.TryWrite(true);
            return timer;
        }
        public void Advance(TimeSpan time) => inner.Advance(time);
        public async Task NextTimer() => await timers.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }
    internal sealed class Client : IAirConditionerClient
    {
        private readonly Channel<int> reads = Channel.CreateUnbounded<int>();
        public int CommandCalls { get; private set; }
        public Func<CancellationToken, Task<CommandResult>> Command { get; set; } = _ => Task.FromResult(new CommandResult(CommandOutcome.Confirmed));
        public Task<CommandResult> SendCommandAsync(IPAddress address, DeviceSecretKey key, AirConditionerCommand command, CancellationToken ct = default)
        { CommandCalls++; return Command(ct); }
        public int Calls { get; private set; }
        public Func<CancellationToken, Task<StatusRead>> Read { get; set; } = _ => Task.FromResult(Success());
        public Task<StatusRead> ReadStatusAsync(IPAddress address, DeviceSecretKey key, CancellationToken ct)
        {
            Calls++;
            var result = Read(ct);
            // The tests observe reads after the synchronous monitor continuation has installed its wait.
            reads.Writer.TryWrite(Calls);
            return result;
        }
        public async Task NextRead() => await reads.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }
    internal sealed class Secrets : ISecretStore
    {
        public bool Fail { get; set; }
        public DeviceSecretKey? Read()
        {
            if (Fail) throw new IOException("Test storage failure");
            DeviceSecretKey.TryParse(Convert.ToBase64String(new byte[16]), out var key); return key;
        }
        public void Save(DeviceSecretKey key) => throw new NotSupportedException();
        public void Delete() => throw new NotSupportedException();
    }
    internal sealed class Configuration : IAirConditionerConfigurationStore
    {
        private readonly AirConditionerConfiguration value = new("unit", "192.0.2.1");
        public bool HasConfiguredAirConditioner => true;
        public AirConditionerConfiguration Load() => value;
        public void Save(AirConditionerConfiguration configuration) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
    }
    private sealed class Store(int seconds) : IPreferencesStore
    {
        public AppPreferences Load() => new(StatusRefreshIntervalSeconds: seconds);
        public void Save(AppPreferences preferences) { }
    }
}

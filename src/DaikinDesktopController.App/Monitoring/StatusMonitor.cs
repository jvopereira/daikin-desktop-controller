using System.ComponentModel;
using System.IO;
using System.Net;
using DaikinDesktopController.App.Configuration;
using DaikinDesktopController.App.Preferences;
using DaikinDesktopController.App.Secrets;
using DaikinDesktopController.Device;
namespace DaikinDesktopController.App.Monitoring;
// Public operations and continuations run on the UI context.
public sealed class StatusMonitor : IDisposable
{
    private readonly IAirConditionerClient client;
    private readonly ISecretStore secrets;
    private readonly IAirConditionerConfigurationStore configuration;
    private readonly PreferencesSession preferences;
    private readonly TimeProvider clock;
    private readonly TimeSpan retryDelay;
    private CancellationTokenSource? session;
    private readonly SemaphoreSlim deviceGate = new(1, 1);
    private CancellationTokenSource? run;
    private CancellationTokenSource? wait;
    private bool disposed;
    public MonitorState State { get; private set; } = new ReadingStatus();
    public event Action? StateChanged;
    public bool CanSendCommands => session is not null && run is not null && State is Available;
    public StatusMonitor(IAirConditionerClient client, ISecretStore secrets,
        IAirConditionerConfigurationStore configuration, PreferencesSession preferences,
        TimeProvider clock, TimeSpan? retryDelay = null)
    {
        this.client = client; this.secrets = secrets; this.configuration = configuration;
        this.preferences = preferences; this.clock = clock;
        this.retryDelay = retryDelay ?? TimeSpan.FromSeconds(2);
        preferences.Changed += PreferencesChanged;
    }
    private void Set(MonitorState state) { State = state; StateChanged?.Invoke(); }
    private bool Current(CancellationTokenSource attempt) => ReferenceEquals(run, attempt) && !attempt.IsCancellationRequested;
    private CancellationTokenSource Begin()
    {
        run?.Cancel();
        session ??= new CancellationTokenSource();
        var attempt = CancellationTokenSource.CreateLinkedTokenSource(session.Token);
        run = attempt;
        return attempt;
    }
    public void Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (run is not null) return;
        var attempt = Begin();
        Set(new ReadingStatus());
        _ = RunAsync(attempt, null);
    }
    public void Stop()
    {
        if (run is null && session is null) return;
        var previous = run;
        run = null;
        previous?.Cancel();
        var oldSession = session;
        session = null;
        oldSession?.Cancel();
        StateChanged?.Invoke();
    }
    // Completes after the immediate read (including its retry), not after the polling loop.
    public Task RefreshAsync()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var attempt = Begin();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = RunAsync(attempt, completion);
        return completion.Task;
    }
    private async Task RunAsync(CancellationTokenSource attempt, TaskCompletionSource? firstRead)
    {
        try
        {
            await ReadAsync(attempt);
            firstRead?.TrySetResult();
            while (Current(attempt))
            {
                using var delay = CancellationTokenSource.CreateLinkedTokenSource(attempt.Token);
                wait = delay;
                try { await Task.Delay(TimeSpan.FromSeconds(preferences.Current.StatusRefreshIntervalSeconds), clock, delay.Token); }
                catch (OperationCanceledException) when (!attempt.IsCancellationRequested) { continue; }
                finally { if (ReferenceEquals(wait, delay)) wait = null; }
                if (Current(attempt)) await ReadAsync(attempt);
            }
        }
        catch (OperationCanceledException) when (attempt.IsCancellationRequested) { }
        finally
        {
            firstRead?.TrySetResult();
            if (ReferenceEquals(run, attempt)) run = null;
            attempt.Dispose();
        }
    }
    private async Task ReadAsync(CancellationTokenSource attempt)
    {
        AirConditionerConfiguration? saved;
        DeviceSecretKey? key;
        try { saved = configuration.Load(); key = secrets.Read(); }
        catch (Exception e) when (e is Win32Exception or UnauthorizedAccessException or System.Security.SecurityException or IOException)
        { if (Current(attempt)) Set(new Unavailable(UnavailableReason.StorageFailure)); return; }
        if (!Current(attempt)) return;
        if (key is null) { Set(new Unavailable(UnavailableReason.KeyRejected)); return; }
        if (saved is null || !JsonPreferencesStore.IsIpv4(saved.IpAddress))
        { Set(new Unavailable(UnavailableReason.ReadError)); return; }
        var address = IPAddress.Parse(saved.IpAddress);
        for (var read = 0; read < 2; read++)
        {
            StatusRead result;
            await deviceGate.WaitAsync(attempt.Token);
            try { result = await client.ReadStatusAsync(address, key, attempt.Token); }
            finally { deviceGate.Release(); }
            if (!Current(attempt)) return;
            if (result.Outcome == StatusReadOutcome.Succeeded && result.Status is { } status)
            { Set(new Available(status)); return; }
            if (read == 0) await Task.Delay(retryDelay, clock, attempt.Token);
            else Set(new Unavailable(result.Outcome switch
            {
                StatusReadOutcome.Unreachable => UnavailableReason.Unreachable,
                StatusReadOutcome.KeyRejected => UnavailableReason.KeyRejected,
                _ => UnavailableReason.ReadError
            }));
        }
    }
    public async Task<CommandResult?> SendAsync(AirConditionerCommand command)
    {
        if (!CanSendCommands) return null;
        var currentSession = session!;
        try
        {
            await deviceGate.WaitAsync(currentSession.Token);
            CommandResult result;
            try
            {
                if (!ReferenceEquals(session, currentSession) || !CanSendCommands) return null;
                if (State is not Available available || !command.IsSupportedBy(available.Status)) return null;
                var saved = configuration.Load();
                var key = secrets.Read();
                if (saved is null || key is null || !JsonPreferencesStore.IsIpv4(saved.IpAddress)) return null;
                result = await client.SendCommandAsync(IPAddress.Parse(saved.IpAddress), key, command, currentSession.Token);
                if (ReferenceEquals(session, currentSession) && result.Status is { } status) Set(new Available(status));
            }
            finally { deviceGate.Release(); }
            if (ReferenceEquals(session, currentSession)) await RefreshAsync();
            return result;
        }
        catch (OperationCanceledException) when (currentSession.IsCancellationRequested) { return null; }
        catch (Exception e) when (e is Win32Exception or UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            if (ReferenceEquals(session, currentSession)) Set(new Unavailable(UnavailableReason.StorageFailure));
            return null;
        }
    }
    private void PreferencesChanged(AppPreferences old, AppPreferences now)
    { if (old.StatusRefreshIntervalSeconds != now.StatusRefreshIntervalSeconds) wait?.Cancel(); }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        preferences.Changed -= PreferencesChanged;
        Stop();
    }
}

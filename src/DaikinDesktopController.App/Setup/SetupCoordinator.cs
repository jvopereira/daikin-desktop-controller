using System.ComponentModel;
using System.IO;
using System.Net;
using DaikinDesktopController.App.Configuration;
using DaikinDesktopController.App.Preferences;
using DaikinDesktopController.App.Secrets;
using DaikinDesktopController.Device;
namespace DaikinDesktopController.App.Setup;
// Public operations run on the UI context. Cancellation and identity checks also
// protect against implementations that complete after ignoring cancellation.
public sealed class SetupCoordinator : IDisposable
{
    private readonly IDeviceDiscovery discovery;
    private readonly IAirConditionerClient client;
    private readonly ISecretStore secrets;
    private readonly IAirConditionerConfigurationStore configuration;
    private readonly PreferencesSession preferences;
    private CancellationTokenSource? run;
    public SetupState State { get; private set; }
    public event Action? StateChanged;
    public SetupCoordinator(IDeviceDiscovery discovery, IAirConditionerClient client, ISecretStore secrets,
        IAirConditionerConfigurationStore configuration, PreferencesSession preferences)
    {
        this.discovery = discovery; this.client = client; this.secrets = secrets;
        this.configuration = configuration; this.preferences = preferences;
        try { State = configuration.HasConfiguredAirConditioner ? new Configured() : new NeedsSecretKey(); }
        catch (Win32Exception) { State = new StorageFailure(); }
    }
    private void Set(SetupState state) { State = state; StateChanged?.Invoke(); }
    private bool Current(CancellationTokenSource attempt) => ReferenceEquals(run, attempt) && !attempt.IsCancellationRequested;
    private CancellationTokenSource Begin()
    {
        run?.Cancel();
        var attempt = new CancellationTokenSource();
        run = attempt;
        return attempt;
    }
    public Task StartAsync() => ExecuteAsync(null);
    public Task SelectAsync(DiscoveredAirConditioner candidate)
    {
        if (State is not ChoosingAirConditioner choosing || !choosing.Candidates.Contains(candidate)) return Task.CompletedTask;
        return ExecuteAsync(candidate);
    }
    private async Task ExecuteAsync(DiscoveredAirConditioner? selection)
    {
        var attempt = Begin();
        try
        {
            var key = secrets.Read();
            // Keep the last successful configuration until replacement pairing saves successfully.
            if (!Current(attempt)) return;
            if (key is null) { Set(new NeedsSecretKey()); return; }
            if (selection is not null)
            { await PairAsync(selection.Id, selection.Address, key, attempt); return; }
            if (JsonPreferencesStore.IsIpv4(preferences.Current.ManualIpAddress))
            {
                var outcome = await PairAsync(null, IPAddress.Parse(preferences.Current.ManualIpAddress!), key, attempt);
                if (!Current(attempt) || outcome != StatusReadOutcome.Unreachable) return;
            }
            Set(new Discovering());
            var candidates = await discovery.DiscoverAsync(attempt.Token);
            if (!Current(attempt)) return;
            if (candidates.Count == 0) Set(new NotFound());
            else if (candidates.Count == 1) await PairAsync(candidates[0].Id, candidates[0].Address, key, attempt);
            else Set(new ChoosingAirConditioner(candidates));
        }
        catch (OperationCanceledException) when (attempt.IsCancellationRequested) { }
        catch (Exception e) when (e is Win32Exception or UnauthorizedAccessException or System.Security.SecurityException or IOException)
        { if (Current(attempt)) Set(new StorageFailure()); }
        catch (System.Net.Sockets.SocketException) { if (Current(attempt)) Set(new NotFound()); }
        catch (Exception e) when (e is InvalidOperationException or System.Net.NetworkInformation.NetworkInformationException)
        { if (Current(attempt)) Set(new UnexpectedResponse()); }
        finally
        {
            if (ReferenceEquals(run, attempt)) run = null;
            attempt.Dispose();
        }
    }
    private async Task<StatusReadOutcome> PairAsync(string? id, IPAddress address, DeviceSecretKey key, CancellationTokenSource attempt)
    {
        if (!Current(attempt)) return StatusReadOutcome.Unreachable;
        Set(new Pairing());
        var result = await client.ReadStatusAsync(address, key, attempt.Token);
        if (!Current(attempt)) return result.Outcome;
        switch (result.Outcome)
        {
            case StatusReadOutcome.Succeeded when result.Status is not null:
                configuration.Save(new(id, address.ToString())); Set(new Configured(result.Status)); break;
            case StatusReadOutcome.KeyRejected: Set(new KeyRejected()); break;
            case StatusReadOutcome.Unreachable: Set(new NotFound()); break;
            default: Set(new UnexpectedResponse()); break;
        }
        return result.Outcome;
    }
    public void Dispose() { run?.Cancel(); run = null; }
}

using System.IO;
using System.Net;
using DaikinDesktopController.App.Configuration;
using DaikinDesktopController.App.Preferences;
using DaikinDesktopController.App.Secrets;
using DaikinDesktopController.App.Setup;
using DaikinDesktopController.Device;
using Xunit;
namespace DaikinDesktopController.App.Tests;
public class SetupTests
{
    private static readonly DiscoveredAirConditioner Unit = new("unit", IPAddress.Parse("192.0.2.1"), "host");
    [Fact] public async Task OneCandidatePairsAndSaves()
    {
        var fixture = new Fixture([Unit]); await fixture.Coordinator.StartAsync();
        Assert.IsType<Configured>(fixture.Coordinator.State); Assert.Equal("unit",fixture.Configuration.Value!.Id);
    }
    [Fact] public async Task MultipleCandidatesWaitForSelection()
    {
        var fixture = new Fixture([Unit, Unit with { Id = "second" }]); await fixture.Coordinator.StartAsync();
        Assert.IsType<ChoosingAirConditioner>(fixture.Coordinator.State); Assert.Null(fixture.Configuration.Value);
        await fixture.Coordinator.SelectAsync(Unit); Assert.IsType<Configured>(fixture.Coordinator.State);
    }
    [Fact] public async Task NoCandidatesAreNotFound()
    { var fixture = new Fixture([]); await fixture.Coordinator.StartAsync(); Assert.IsType<NotFound>(fixture.Coordinator.State); }
    [Fact] public async Task NoKeyNeedsSecrets()
    {
        var fixture = new Fixture([Unit]); fixture.Secrets.Key = null; await fixture.Coordinator.StartAsync();
        Assert.IsType<NeedsSecretKey>(fixture.Coordinator.State); Assert.Equal(0,fixture.Discovery.Calls);
    }
    [Theory]
    [InlineData(StatusReadOutcome.KeyRejected)]
    [InlineData(StatusReadOutcome.UnexpectedResponse)]
    public async Task FailedPairingDoesNotSave(StatusReadOutcome outcome)
    {
        var fixture = new Fixture([Unit]); fixture.Client.Read = (_,_) => Task.FromResult(new StatusRead(outcome));
        await fixture.Coordinator.StartAsync(); Assert.Null(fixture.Configuration.Value);
        if (outcome == StatusReadOutcome.KeyRejected) Assert.IsType<KeyRejected>(fixture.Coordinator.State);
        else Assert.IsType<UnexpectedResponse>(fixture.Coordinator.State);
    }
    [Theory]
    [InlineData(StatusReadOutcome.Unreachable)]
    [InlineData(StatusReadOutcome.KeyRejected)]
    [InlineData(StatusReadOutcome.UnexpectedResponse)]
    public async Task FailedReplacementPreservesExistingConfiguration(StatusReadOutcome outcome)
    {
        var fixture = new Fixture([Unit]);
        var previous = new AirConditionerConfiguration("previous", "192.0.2.10");
        fixture.Configuration.Save(previous);
        fixture.Client.Read = (_,_) => Task.FromResult(new StatusRead(outcome));
        await fixture.Coordinator.StartAsync();
        Assert.Equal(previous, fixture.Configuration.Load());
        Assert.False(StartupDecision.ShouldOpenPanel(fixture.Configuration));
    }
    [Fact] public async Task UnreachableManualReplacementAndEmptyDiscoveryPreserveExistingConfiguration()
    {
        var fixture = new Fixture([], "192.0.2.2");
        var previous = new AirConditionerConfiguration("previous", "192.0.2.10");
        fixture.Configuration.Save(previous);
        fixture.Client.Read = (_,_) => Task.FromResult(new StatusRead(StatusReadOutcome.Unreachable));
        await fixture.Coordinator.StartAsync();
        Assert.Equal(1, fixture.Discovery.Calls);
        Assert.IsType<NotFound>(fixture.Coordinator.State);
        Assert.Equal(previous, fixture.Configuration.Load());
    }
    [Fact] public async Task ExistingConfigurationIsReplacedOnlyAfterSuccessfulStatusRead()
    {
        var fixture = new Fixture([Unit]);
        var previous = new AirConditionerConfiguration("previous", "192.0.2.10");
        fixture.Configuration.Save(previous);
        var completion = new TaskCompletionSource<StatusRead>();
        fixture.Client.Read = (_,_) => completion.Task;
        var pairing = fixture.Coordinator.StartAsync();
        Assert.IsType<Pairing>(fixture.Coordinator.State);
        Assert.Equal(previous, fixture.Configuration.Load());
        completion.SetResult(Success());
        await pairing;
        Assert.IsType<Configured>(fixture.Coordinator.State);
        Assert.Equal(new AirConditionerConfiguration(Unit.Id, Unit.Address.ToString()), fixture.Configuration.Load());
    }
    [Fact] public async Task CancelledReplacementPreservesExistingConfiguration()
    {
        var fixture = new Fixture([Unit]);
        var previous = new AirConditionerConfiguration("previous", "192.0.2.10");
        fixture.Configuration.Save(previous);
        var completion = new TaskCompletionSource<StatusRead>();
        fixture.Client.Read = (_,_) => completion.Task;
        var pairing = fixture.Coordinator.StartAsync();
        fixture.Coordinator.Dispose();
        completion.SetResult(Success());
        await pairing;
        Assert.Equal(previous, fixture.Configuration.Load());
    }
    [Fact] public async Task UnreachableManualAddressFallsBackToDiscovery()
    {
        var fixture = new Fixture([Unit], "192.0.2.2");
        fixture.Client.Read = (address,_) => Task.FromResult(address.Equals(Unit.Address) ? Success() : new(StatusReadOutcome.Unreachable));
        await fixture.Coordinator.StartAsync(); Assert.Equal(1,fixture.Discovery.Calls); Assert.IsType<Configured>(fixture.Coordinator.State);
    }
    [Fact] public async Task ReachableManualAddressSkipsDiscovery()
    {
        var fixture = new Fixture([],"192.0.2.2"); await fixture.Coordinator.StartAsync();
        Assert.Equal(0,fixture.Discovery.Calls); Assert.Equal("192.0.2.2",fixture.Configuration.Value!.IpAddress);
    }
    [Fact] public async Task RestartCannotSaveStaleResultsEvenIfClientIgnoresCancellation()
    {
        var fixture = new Fixture([Unit]); var completion = new TaskCompletionSource<StatusRead>(); CancellationToken firstToken = default;
        fixture.Client.Read = (_,ct) => { firstToken = ct; return completion.Task; };
        var first = fixture.Coordinator.StartAsync(); fixture.Secrets.Key = null;
        await fixture.Coordinator.StartAsync(); Assert.True(firstToken.IsCancellationRequested);
        completion.SetResult(Success()); await first;
        Assert.IsType<NeedsSecretKey>(fixture.Coordinator.State); Assert.Null(fixture.Configuration.Value);
    }
    [Fact] public async Task RestartCannotPairStaleDiscoveryResults()
    {
        var fixture = new Fixture([Unit]);
        var completion = new TaskCompletionSource<IReadOnlyList<DiscoveredAirConditioner>>();
        fixture.Discovery.Resolve = _ => completion.Task;
        var first = fixture.Coordinator.StartAsync();
        fixture.Secrets.Key = null; await fixture.Coordinator.StartAsync();
        completion.SetResult([Unit]); await first;
        Assert.IsType<NeedsSecretKey>(fixture.Coordinator.State); Assert.Null(fixture.Configuration.Value);
    }
    [Fact] public void ConfigurationRequiresBothStoresAndToleratesMalformedFiles()
    {
        var path = Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json"); var secrets = new Secrets();
        try
        {
            var store = new JsonAirConditionerConfigurationStore(path,secrets);
            Assert.False(store.HasConfiguredAirConditioner);
            store.Save(new("unit","192.0.2.1")); Assert.Equal(new("unit","192.0.2.1"),store.Load()); Assert.True(store.HasConfiguredAirConditioner);
            secrets.Key = null; Assert.False(store.HasConfiguredAirConditioner);
            File.WriteAllText(path,"{bad"); Assert.Null(store.Load()); store.Clear(); Assert.Null(store.Load());
        }
        finally { File.Delete(path); }
    }
    [Theory]
    [InlineData("192.0.2.1","192.0.2.1")]
    [InlineData("invalid",null)]
    [InlineData("1234",null)]
    [InlineData("127.1",null)]
    [InlineData("::1",null)]
    public void PreferencesValidateManualAddress(string input,string? expected)
    {
        var path = Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
        try { var store = new JsonPreferencesStore(path); store.Save(new(ManualIpAddress:input)); Assert.Equal(expected,store.Load().ManualIpAddress); }
        finally { File.Delete(path); }
    }
    private static StatusRead Success() => new(StatusReadOutcome.Succeeded,new(true,"2",24,26,"1.0"));
    private sealed class Fixture
    {
        public Secrets Secrets { get; } = new(); public Configuration Configuration { get; } = new();
        public Discovery Discovery { get; } public Client Client { get; } = new(); public SetupCoordinator Coordinator { get; }
        public Fixture(IReadOnlyList<DiscoveredAirConditioner> units,string? manual = null)
        {
            Discovery = new(units); Coordinator = new(Discovery,Client,Secrets,Configuration,new PreferencesSession(new Preferences(manual)));
        }
    }
    private sealed class Secrets : ISecretStore
    {
        public DeviceSecretKey? Key { get; set; } = Parse();
        private static DeviceSecretKey Parse() { DeviceSecretKey.TryParse(Convert.ToBase64String(new byte[16]),out var key); return key!; }
        public DeviceSecretKey? Read() => Key; public void Save(DeviceSecretKey key) => Key = key; public void Delete() => Key = null;
    }
    private sealed class Configuration : IAirConditionerConfigurationStore
    {
        public AirConditionerConfiguration? Value { get; private set; }
        public bool HasConfiguredAirConditioner => Value is not null;
        public AirConditionerConfiguration? Load() => Value;
        public void Save(AirConditionerConfiguration value) => Value = value;
        public void Clear() => Value = null;
    }
    private sealed class Discovery(IReadOnlyList<DiscoveredAirConditioner> units) : IDeviceDiscovery
    {
        public int Calls { get; private set; }
        public Func<CancellationToken,Task<IReadOnlyList<DiscoveredAirConditioner>>> Resolve { get; set; } = _ => Task.FromResult(units);
        public Task<IReadOnlyList<DiscoveredAirConditioner>> DiscoverAsync(CancellationToken ct) { Calls++; return Resolve(ct); }
    }
    private sealed class Client : IAirConditionerClient
    {
        public Func<IPAddress,CancellationToken,Task<StatusRead>> Read { get; set; } = (_,_) => Task.FromResult(Success());
        public Task<StatusRead> ReadStatusAsync(IPAddress address,DeviceSecretKey key,CancellationToken ct) => Read(address,ct);
    }
    private sealed class Preferences(string? manual) : IPreferencesStore
    { public AppPreferences Load() => new(ManualIpAddress:manual); public void Save(AppPreferences preferences) { } }
}

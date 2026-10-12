using System.Net;
namespace DaikinDesktopController.Device;
public sealed record DiscoveredAirConditioner(string Id, IPAddress Address, string? HostName);
public interface IDeviceDiscovery
{
    Task<IReadOnlyList<DiscoveredAirConditioner>> DiscoverAsync(CancellationToken ct = default);
}

using System.Net;
using System.Net.Sockets;
using Zeroconf;
namespace DaikinDesktopController.Device.Discovery;
public sealed class ZeroconfDeviceDiscovery : IDeviceDiscovery
{
    public async Task<IReadOnlyList<DiscoveredAirConditioner>> DiscoverAsync(CancellationToken ct = default)
    {
        var hosts = await ZeroconfResolver.ResolveAsync("_iota._tcp.local.", scanTime: TimeSpan.FromSeconds(5),
            retries: 1, cancellationToken: ct).ConfigureAwait(false);
        var advertisements = await Task.WhenAll(hosts.Select(host => MapAsync(host, ct))).ConfigureAwait(false);
        return DiscoveryFilter.Apply(advertisements);
    }
    private static async Task<ServiceAdvertisement> MapAsync(IZeroconfHost host, CancellationToken ct)
    {
        var addresses = host.IPAddresses.Select(text => IPAddress.TryParse(text, out var ip) ? ip : null).OfType<IPAddress>().ToArray();
        var txt = host.Services.Values.Where(service => service.Name == "_iota._tcp.local.")
            .SelectMany(service => service.Properties).SelectMany(set => set)
            .Where(pair => pair.Value is not null).GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.OrdinalIgnoreCase);
        string? hostname = null;
        // Zeroconf exposes addresses and instance names, but not the SRV target hostname.
        // When apn is absent, try a bounded reverse lookup; retain the address as
        // the fallback identity if this network has no reverse DNS.
        if (!txt.TryGetValue("apn", out var apn) || string.IsNullOrWhiteSpace(apn))
        {
            var address = addresses.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            if (address is not null)
            {
                hostname = address.ToString();
                try
                {
                    hostname = (await Dns.GetHostEntryAsync(address.ToString(), AddressFamily.InterNetwork, ct).WaitAsync(TimeSpan.FromSeconds(1), ct)
                        .ConfigureAwait(false)).HostName;
                }
                catch (Exception e) when (e is SocketException or TimeoutException) { }
            }
        }
        return new(host.DisplayName, addresses, txt, hostname);
    }
}

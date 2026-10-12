using System.Net;
using System.Net.Sockets;
namespace DaikinDesktopController.Device.Discovery;
internal sealed record ServiceAdvertisement(string InstanceName, IReadOnlyList<IPAddress> Addresses,
    IReadOnlyDictionary<string, string> Txt, string? HostName);
internal static class DiscoveryFilter
{
    public static IReadOnlyList<DiscoveredAirConditioner> Apply(IEnumerable<ServiceAdvertisement> advertisements)
    {
        var result = new Dictionary<string, DiscoveredAirConditioner>(StringComparer.OrdinalIgnoreCase);
        foreach (var advertisement in advertisements)
        {
            if (advertisement.InstanceName != "Daikin Smart AC") continue;
            var address = advertisement.Addresses.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            if (address is null) continue;
            var id = advertisement.Txt.TryGetValue("apn", out var apn) && !string.IsNullOrWhiteSpace(apn) ? apn : advertisement.HostName;
            if (string.IsNullOrWhiteSpace(id)) continue;
            result.TryAdd(id, new(id, address, advertisement.HostName));
        }
        return result.Values.ToArray();
    }
}

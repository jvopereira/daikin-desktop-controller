using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
namespace DaikinDesktopController.Device.LocalProtocol;
internal sealed record RawResponse(int StatusCode, string Body);
internal sealed class InvalidResponseException : Exception;
internal static class RawHttp
{
    private const int MaxHeaders = 16 * 1024;
    private const int MaxBody = 1024 * 1024;
    public static async Task<RawResponse> GetAsync(IPEndPoint endpoint, string path, TimeSpan timeout, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        using var socket = new TcpClient(endpoint.AddressFamily);
        await socket.ConnectAsync(endpoint.Address, endpoint.Port, deadline.Token).ConfigureAwait(false);
        using var stream = socket.GetStream();
        var request = Encoding.ASCII.GetBytes($"GET {path} HTTP/1.1\r\nHost: {endpoint.Address}:{endpoint.Port}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(request, deadline.Token).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int? headerEnd = null, contentLength = null;
        var status = 0;
        while (true)
        {
            var count = await stream.ReadAsync(chunk, deadline.Token).ConfigureAwait(false);
            if (count == 0) break;
            buffer.Write(chunk, 0, count);
            if (buffer.Length > MaxHeaders + MaxBody) throw new InvalidResponseException();
            if (headerEnd is null)
            {
                var text = Encoding.ASCII.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
                var end = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (end < 0) { if (buffer.Length > MaxHeaders) throw new InvalidResponseException(); continue; }
                headerEnd = end + 4;
                if (headerEnd > MaxHeaders) throw new InvalidResponseException();
                var lines = text[..end].Split("\r\n");
                var parts = lines[0].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || !parts[0].StartsWith("HTTP/1.", StringComparison.Ordinal) || !int.TryParse(parts[1], out status))
                    throw new InvalidResponseException();
                foreach (var line in lines.Skip(1))
                {
                    var colon = line.IndexOf(':');
                    if (colon < 0) continue;
                    var name = line[..colon].Trim();
                    if (name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)) throw new InvalidResponseException();
                    if (!name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!int.TryParse(line[(colon + 1)..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var size)
                        || size > MaxBody || (contentLength.HasValue && contentLength != size)) throw new InvalidResponseException();
                    contentLength = size;
                }
            }
            var received = buffer.Length - headerEnd.Value;
            if (received > MaxBody) throw new InvalidResponseException();
            if (contentLength.HasValue && received >= contentLength.Value) break;
        }
        if (headerEnd is null || (contentLength.HasValue && buffer.Length - headerEnd.Value < contentLength.Value))
            throw new InvalidResponseException();
        var length = contentLength ?? (int)buffer.Length - headerEnd.Value;
        return new(status, Encoding.ASCII.GetString(buffer.GetBuffer(), headerEnd.Value, length));
    }
}

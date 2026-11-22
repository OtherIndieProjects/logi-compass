using System.Net;

namespace AAALogistics.Infrastructure.Configuration;

public static class ListenerAddress
{
    public static (IPAddress Address, int Port) Parse(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.Port is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                $"La URL de escucha no es válida: '{url}'.");
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return (IPAddress.Loopback, uri.Port);
        }

        if (!IPAddress.TryParse(uri.IdnHost, out var address))
        {
            throw new InvalidOperationException(
                $"Listeners solo admite una IP o localhost: '{url}'.");
        }

        return (address, uri.Port);
    }
}

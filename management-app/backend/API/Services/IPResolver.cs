using OutlineManager.API.Interfaces;
using System.Net;
using System.Net.Sockets;

namespace OutlineManager.API.Services;

public class IPResolver(ILogger<IPResolver> logger) : IIPResolver
{
    public async Task<string?> ResolveAsync(string hostName)
    {
		try
		{
            var ips = await Dns.GetHostAddressesAsync(hostName, AddressFamily.InterNetwork);
            return ips.FirstOrDefault()?.ToString() ?? hostName;
        }
		catch (Exception exception)
		{
            if (logger.IsEnabled(LogLevel.Critical))
                logger.LogCritical(exception, "Failed to resolve IP address for host: {HostName}", hostName);

            return hostName;
		}
    }
}

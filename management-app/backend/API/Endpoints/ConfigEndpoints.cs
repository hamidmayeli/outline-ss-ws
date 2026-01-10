using Microsoft.AspNetCore.Http.HttpResults;
using OutlineManager.API.Interfaces;
using OutlineManager.API.Models;

namespace OutlineManager.API.Endpoints;

public static class ConfigEndpoints
{
    extension(RouteGroupBuilder group)
    {
        public RouteGroupBuilder MapConfigEndpoints()
        {
            group.MapGet("/{clientId}", GetConfigAsync)
                .WithName("GetClientConfig")
                .AllowAnonymous();

            return group;
        }
    }
    private static async Task<Results<Ok<string>, NotFound>> GetConfigAsync(
        string clientId,
        IClientRepository clientRepository,
        AppSettings appSettings)
    {
        var client = await clientRepository.GetByIdAsync(clientId);

        if (client == null || !client.IsActive)
            return TypedResults.NotFound();

        var config = BuildYamlConfig(client, appSettings);

        return TypedResults.Ok(config);
    }

    private static string BuildYamlConfig(Client client, AppSettings appSettings)
    {
        var tcpUrl = $"wss://{appSettings.Domain}{appSettings.TcpPath}";
        var udpUrl = $"wss://{appSettings.Domain}{appSettings.UdpPath}";

        return $@"transport:
  $type: tcpudp
  tcp:
    $type: shadowsocks
    endpoint:
      $type: websocket
      url: {tcpUrl}
    cipher: {client.Cipher}
    secret: {client.Secret}
  udp:
    $type: shadowsocks
    endpoint:
      $type: websocket
      url: {udpUrl}
    cipher: {client.Cipher}
    secret: {client.Secret}
";
    }
}

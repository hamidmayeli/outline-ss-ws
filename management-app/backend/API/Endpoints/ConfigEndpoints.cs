using Microsoft.AspNetCore.Http.HttpResults;
using OutlineManager.API.DTOs;
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
    private static async Task<Results<Ok<ConfigResponse>, NotFound>> GetConfigAsync(
        string clientId,
        IClientRepository clientRepository,
        AppSettings appSettings)
    {
        var client = await clientRepository.GetByIdAsync(clientId);

        if (client == null || !client.IsActive)
            return TypedResults.NotFound();

        var config = BuildConfig(client, appSettings);

        return TypedResults.Ok(config);
    }

    private static ConfigResponse BuildConfig(Client client, AppSettings appSettings)
        => new ()
        {
          Transport = new()
          {
              Type = "tcpudp",
              Tcp = new ()
              {
                  Type = "shadowsocks",
                  Endpoint = new()
                  {
                      Type = "websocket",
                      Url = $"wss://{appSettings.Domain}{appSettings.TcpPath}",
                  },
                  Cipher = client.Cipher,
                  Secret = client.Secret,
              },
              Udp = new()
              {
                  
                  Type = "shadowsocks",
                  Endpoint = new()
                  {
                      Type = "websocket",
                      Url = $"wss://{appSettings.Domain}{appSettings.UdpPath}",
                  },
                  Cipher = client.Cipher,
                  Secret = client.Secret,
              },
          }  
        };
}

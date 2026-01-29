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
    private static async Task<Results<Ok<ConfigResponse>, NotFound, Conflict<string>>> GetConfigAsync(
        string clientId,
        IClientRepository clientRepository,
        AppSettings appSettings)
    {
        var client = await clientRepository.GetByIdAsync(clientId);

        if (client == null)
            return TypedResults.NotFound();

        if (!client.IsActive)
            return TypedResults.Conflict("Limits reached.");

        var config = await BuildConfig(client, appSettings);

        return TypedResults.Ok(config);
    }

    private static async Task<ConfigResponse> BuildConfig(Client client, AppSettings appSettings)
    {
        return new()
        {
            Transport = new()
            {
                Type = "tcpudp",
                Tcp = new()
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
}

using OutlineManager.API.DTOs;
using OutlineManager.API.Models;
using System.Net;
using System.Net.Http.Json;

namespace OutlineManager.API.Tests.TestCases;

public class UserIntractsWithConfigs : TestCaseBase
{
    [Fact]
    public async Task When_id_does_not_exist()
    {
        var clientId = Guid.NewGuid().ToString();

        var client = _fixture.CreateClient();

        var response = await client.GetAsync($"/api/v1/config/{clientId}");

        Assert.Multiple(
            () => Assert.Equal(HttpStatusCode.NotFound, response.StatusCode),
            async () => Assert.Empty(await response.Content.ReadAsStringAsync())
        );
    }

    [Fact]
    public async Task When_id_does_exists()
    {
        var clientId = Guid.NewGuid().ToString();

        await _fixture.SetClient(new Client
        {
            Id = clientId,
            Name = "name",
            Secret = "secret"
        });

        var client = _fixture.CreateClient();

        var response = await client.GetAsync($"/api/v1/config/{clientId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var expectedContent = """{"transport":{"$type":"tcpudp","tcp":{"$type":"shadowsocks","endpoint":{"$type":"websocket","url":"wss://localhost/tcp-ws"},"cipher":"chacha20-ietf-poly1305","secret":"secret"},"udp":{"$type":"shadowsocks","endpoint":{"$type":"websocket","url":"wss://localhost/udp-ws"},"cipher":"chacha20-ietf-poly1305","secret":"secret"}}}""";
        Assert.Equal(expectedContent, content);
    }

    [Fact]
    public async Task When_GetConfig_WithInactiveClient_ReturnsConflict()
    {
        var clientId = Guid.NewGuid().ToString();

        await _fixture.SetClient(new Client
        {
            Id = clientId,
            Name = "InactiveClient",
            Secret = "secret",
            IsActive = false,
        });

        var client = _fixture.CreateClient();

        var response = await client.GetAsync($"/api/v1/config/{clientId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Limits reached.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task When_GetConfig_NoAuthRequired_AllowsAnonymousAccess()
    {
        var clientId = Guid.NewGuid().ToString();

        await _fixture.SetClient(new Client
        {
            Id = clientId,
            Name = "name",
            Secret = "secret"
        });

        var client = _fixture.CreateClient();

        var response = await client.GetAsync($"/api/v1/config/{clientId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task When_GetConfig_WithSingleConnectionClient_RegeneratesSecret()
    {
        var clientId = Guid.NewGuid().ToString();
        var originalSecret = "original-secret";

        await _fixture.SetClient(new Client
        {
            Id = clientId,
            Name = "SingleConnClient",
            Secret = originalSecret,
            IsSingleConnection = true,
            AccessKeyId = 1
        });

        var client = _fixture.CreateClient();

        var response = await client.GetAsync($"/api/v1/config/{clientId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var config = await response.Content.ReadFromJsonAsync<ConfigResponse>();
        Assert.NotNull(config);
        Assert.NotEqual(originalSecret, config.Transport.Tcp.Secret);
        Assert.NotEqual(originalSecret, config.Transport.Udp.Secret);
        Assert.Equal(config.Transport.Tcp.Secret, config.Transport.Udp.Secret);
    }

    [Fact]
    public async Task When_GetConfig_WithSingleConnectionClient_EachRequestReturnsDifferentSecret()
    {
        var clientId = Guid.NewGuid().ToString();

        await _fixture.SetClient(new Client
        {
            Id = clientId,
            Name = "SingleConnClient",
            Secret = "initial-secret",
            IsSingleConnection = true,
            AccessKeyId = 1
        });

        var client = _fixture.CreateClient();

        var response1 = await client.GetAsync($"/api/v1/config/{clientId}");
        var config1 = await response1.Content.ReadFromJsonAsync<ConfigResponse>();

        var response2 = await client.GetAsync($"/api/v1/config/{clientId}");
        var config2 = await response2.Content.ReadFromJsonAsync<ConfigResponse>();

        Assert.NotNull(config1);
        Assert.NotNull(config2);
        Assert.NotEqual(config1.Transport.Tcp.Secret, config2.Transport.Tcp.Secret);
    }

    [Fact]
    public async Task When_GetConfig_WithNonSingleConnectionClient_SecretRemainsUnchanged()
    {
        var clientId = Guid.NewGuid().ToString();
        var originalSecret = "static-secret";

        await _fixture.SetClient(new Client
        {
            Id = clientId,
            Name = "NormalClient",
            Secret = originalSecret,
            IsSingleConnection = false,
            AccessKeyId = 1
        });

        var client = _fixture.CreateClient();

        var response = await client.GetAsync($"/api/v1/config/{clientId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var config = await response.Content.ReadFromJsonAsync<ConfigResponse>();
        Assert.NotNull(config);
        Assert.Equal(originalSecret, config.Transport.Tcp.Secret);
    }
}

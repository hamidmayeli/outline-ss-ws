using OutlineManager.API.Models;
using System.Net;

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
}

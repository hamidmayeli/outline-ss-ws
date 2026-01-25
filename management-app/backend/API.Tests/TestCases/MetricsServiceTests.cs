using Microsoft.Extensions.DependencyInjection;
using OutlineManager.API.Interfaces;
using OutlineManager.API.Models;

namespace OutlineManager.API.Tests.TestCases;

public class MetricsServiceTests : TestCaseBase
{
    [Fact]
    public async Task When_GetClientUsageLast30Days_WithNoData_ReturnsZeroUsage()
    {
        // Arrange
        var clientId = "test-client-1";
        
        // Setup raw metrics response with no data for this client
        _fixture.PrometheusHttpHandler.AddRoute("/metrics", """
            # HELP shadowsocks_data_bytes Bytes transferred
            # TYPE shadowsocks_data_bytes counter
            shadowsocks_data_bytes{access_key="other-client",dir="c>p"} 1000
            """);
        
        var metricsService = _fixture.Services.GetRequiredService<IMetricsService>();

        // Act
        var usage = await metricsService.GetClientUsageLast30DaysAsync(clientId);

        // Assert
        Assert.NotNull(usage);
        Assert.Equal(0, usage.TotalBytesTransferred);
        Assert.Equal(0, usage.BytesUploaded);
        Assert.Equal(0, usage.BytesDownloaded);
        Assert.Equal(0, usage.TunnelTimeSeconds);
        Assert.Equal(0, usage.TotalConnections);
    }

    [Fact]
    public async Task When_GetClientUsageLast30Days_WithData_ReturnsCorrectUsage()
    {
        // Arrange
        var clientId = "test-client-1";
        var accessKeyId = 123;
        
        var client = new Client
        {
            Id = clientId,
            Name = "Test Client 1",
            Secret = "secret1",
            IsActive = true,
            AccessKeyId = accessKeyId
        };

        await _fixture.SetClient([client]);
        
        // Setup raw metrics response
        _fixture.PrometheusHttpHandler.AddRoute("/metrics", $$"""
            # HELP shadowsocks_data_bytes Bytes transferred
            # TYPE shadowsocks_data_bytes counter
            shadowsocks_data_bytes{access_key="{{accessKeyId}}",dir="c>p"} 1048576
            shadowsocks_data_bytes{access_key="{{accessKeyId}}",dir="c<p"} 2097152
            shadowsocks_data_bytes{access_key="{{accessKeyId}}",dir="p>t"} 1048576
            shadowsocks_data_bytes{access_key="{{accessKeyId}}",dir="t<p"} 2097152
            # HELP shadowsocks_tunnel_time_seconds Tunnel time
            # TYPE shadowsocks_tunnel_time_seconds gauge
            shadowsocks_tunnel_time_seconds{access_key="{{accessKeyId}}"} 3600.5
            # HELP shadowsocks_tcp_connections_closed TCP connections closed
            # TYPE shadowsocks_tcp_connections_closed counter
            shadowsocks_tcp_connections_closed{access_key="{{accessKeyId}}",status="OK"} 150
            """);

        var metricsService = _fixture.Services.GetRequiredService<IMetricsService>();

        // Act
        var usage = await metricsService.GetClientUsageLast30DaysAsync(clientId);

        // Assert
        Assert.NotNull(usage);
        Assert.Equal(6291456, usage.TotalBytesTransferred); // 2MB up + 4MB down
        Assert.Equal(2097152, usage.BytesUploaded); // 1MB + 1MB
        Assert.Equal(4194304, usage.BytesDownloaded); // 2MB + 2MB
        Assert.Equal(3600.5, usage.TunnelTimeSeconds);
        Assert.Equal(150, usage.TotalConnections);
    }

    [Fact]
    public async Task When_GetAllClientsHourlyUsage_WithNoClients_ReturnsEmptyList()
    {
        // Arrange
        _fixture.PrometheusHttpHandler.AddRoute("/metrics", "# No metrics");
        
        var metricsService = _fixture.Services.GetRequiredService<IMetricsService>();

        // Act
        var usage = await metricsService.GetAllClientsHourlyUsageAsync(24);

        // Assert
        Assert.NotNull(usage);
        Assert.Empty(usage);
    }

    [Fact]
    public async Task When_GetAllClientsHourlyUsage_WithClients_ReturnsDataForAllClients()
    {
        // Arrange
        var client1 = new Client
        {
            Id = "1",
            Name = "Client1",
            Secret = "secret1",
            IsActive = true
        };
        var client2 = new Client
        {
            Id = "2",
            Name = "Client2",
            Secret = "secret2",
            IsActive = true
        };

        await _fixture.SetClient([client1, client2]);
        
        _fixture.PrometheusHttpHandler.AddRoute("/metrics", """
            shadowsocks_data_bytes{access_key="1",dir="c>p"} 1000
            shadowsocks_data_bytes{access_key="2",dir="c>p"} 2000
            """);

        var metricsService = _fixture.Services.GetRequiredService<IMetricsService>();

        // Act
        var usage = await metricsService.GetAllClientsHourlyUsageAsync(24);

        // Assert
        Assert.NotNull(usage);
        Assert.Equal(2, usage.Count);
        Assert.Contains(usage, u => u.ClientId == "1" && u.ClientName == "Client1");
        Assert.Contains(usage, u => u.ClientId == "2" && u.ClientName == "Client2");
        // Note: With raw metrics we only get current snapshot, not historical hourly data
        Assert.All(usage, u => Assert.Single(u.DataPoints));
    }

    [Fact]
    public async Task When_GetAllClientsDailyUsage_WithNoClients_ReturnsEmptyList()
    {
        // Arrange
        _fixture.PrometheusHttpHandler.AddRoute("/metrics", "# No metrics");
        
        var metricsService = _fixture.Services.GetRequiredService<IMetricsService>();

        // Act
        var usage = await metricsService.GetAllClientsDailyUsageAsync(30);

        // Assert
        Assert.NotNull(usage);
        Assert.Empty(usage);
    }

    [Fact]
    public async Task When_GetAllClientsDailyUsage_WithClients_ReturnsDataForAllClients()
    {
        // Arrange
        var client1 = new Client
        {
            Id = "1",
            Name = "Client1",
            Secret = "secret1",
            IsActive = true,
            AccessKeyId = 1
        };

        await _fixture.SetClient([client1]);
        
        _fixture.PrometheusHttpHandler.AddRoute("/metrics", """
            shadowsocks_data_bytes{access_key="1",dir="c>p"} 500
            shadowsocks_data_bytes{access_key="1",dir="c<p"} 1500
            """);

        var metricsService = _fixture.Services.GetRequiredService<IMetricsService>();

        // Act
        var usage = await metricsService.GetAllClientsDailyUsageAsync(30);

        // Assert
        Assert.NotNull(usage);
        Assert.Single(usage);
        Assert.Equal("1", usage[0].ClientId);
        Assert.Equal("Client1", usage[0].ClientName);
        // Note: With raw metrics we only get current snapshot, not historical daily data
        Assert.Single(usage[0].DataPoints);
        Assert.Equal(DateTime.UtcNow.Date, usage[0].DataPoints[0].Date);
        Assert.Equal(2000, usage[0].DataPoints[0].BytesTransferred);
        Assert.Equal(500, usage[0].DataPoints[0].BytesUploaded);
        Assert.Equal(1500, usage[0].DataPoints[0].BytesDownloaded);
    }
}

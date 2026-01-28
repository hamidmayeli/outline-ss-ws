using Microsoft.Extensions.DependencyInjection;
using OutlineManager.API.Interfaces;
using OutlineManager.API.Models;
using OutlineManager.API.Tests.TestHelpers;

namespace OutlineManager.API.Tests.TestCases;

public class MetricsServiceTests : TestCaseBase
{
    [Fact]
    public async Task When_GetClientUsageLast30Days_WithNoData_ReturnsZeroUsage()
    {
        // Arrange
        var clientId = "test-client-1";
        _fixture.SetupPrometheusInstantResponses([]);
        
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

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var bytesResponse = PrometheusResponseBuilder.BuildInstant(
            new PrometheusResponseBuilder.InstantSample(
                new Dictionary<string, string> { { "access_key", accessKeyId.ToString() }, { "dir", "c>p" } },
                now,
                2097152
            ),
            new PrometheusResponseBuilder.InstantSample(
                new Dictionary<string, string> { { "access_key", accessKeyId.ToString() }, { "dir", "c<p" } },
                now,
                4194304
            )
        );
        var tunnelResponse = PrometheusResponseBuilder.BuildInstant(
            new PrometheusResponseBuilder.InstantSample(
                new Dictionary<string, string> { { "access_key", accessKeyId.ToString() } },
                now,
                3600.5
            )
        );
        var connectionsResponse = PrometheusResponseBuilder.BuildInstant(
            new PrometheusResponseBuilder.InstantSample(
                new Dictionary<string, string> { { "access_key", accessKeyId.ToString() } },
                now,
                150
            )
        );

        _fixture.SetupPrometheusInstantResponses(new Dictionary<string, string>
        {
            { "shadowsocks_data_bytes", bytesResponse },
            { "shadowsocks_tunnel_time_seconds", tunnelResponse },
            { "shadowsocks_tcp_connections_closed", connectionsResponse }
        });

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
        _fixture.SetupPrometheusRangeResponses([]);
        
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

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var bytesResponse = PrometheusResponseBuilder.BuildRange(
            new PrometheusResponseBuilder.RangeSeries(
                new Dictionary<string, string> { { "access_key", "1" }, { "dir", "c>p" } },
                [new(now, 1000)]
            ),
            new PrometheusResponseBuilder.RangeSeries(
                new Dictionary<string, string> { { "access_key", "2" }, { "dir", "c>p" } },
                [new(now, 2000)]
            )
        );
        var connectionsResponse = PrometheusResponseBuilder.BuildRange();

        _fixture.SetupPrometheusRangeResponses(new Dictionary<string, string>
        {
            { "shadowsocks_data_bytes", bytesResponse },
            { "shadowsocks_tcp_connections_closed", connectionsResponse }
        });

        var metricsService = _fixture.Services.GetRequiredService<IMetricsService>();

        // Act
        var usage = await metricsService.GetAllClientsHourlyUsageAsync(24);

        // Assert
        Assert.NotNull(usage);
        Assert.Equal(2, usage.Count);
        Assert.Contains(usage, u => u.ClientId == "1" && u.ClientName == "Client1");
        Assert.Contains(usage, u => u.ClientId == "2" && u.ClientName == "Client2");
        Assert.All(usage, u => Assert.Equal(24, u.DataPoints.Count));
    }

    [Fact]
    public async Task When_GetAllClientsDailyUsage_WithNoClients_ReturnsEmptyList()
    {
        // Arrange
        _fixture.SetupPrometheusRangeResponses([]);
        
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

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var bytesResponse = PrometheusResponseBuilder.BuildRange(
            new PrometheusResponseBuilder.RangeSeries(
                new Dictionary<string, string> { { "access_key", "1" }, { "dir", "c>p" } },
                [new(now, 500)]
            ),
            new PrometheusResponseBuilder.RangeSeries(
                new Dictionary<string, string> { { "access_key", "1" }, { "dir", "c<p" } },
                [new(now, 1500)]
            )
        );

        _fixture.SetupPrometheusRangeResponses(new Dictionary<string, string>
        {
            { "shadowsocks_data_bytes", bytesResponse }
        });

        var metricsService = _fixture.Services.GetRequiredService<IMetricsService>();

        // Act
        var usage = await metricsService.GetAllClientsDailyUsageAsync(30);

        // Assert
        Assert.NotNull(usage);
        Assert.Single(usage);
        Assert.Equal("1", usage[0].ClientId);
        Assert.Equal("Client1", usage[0].ClientName);
        Assert.Equal(30, usage[0].DataPoints.Count);
        Assert.All(usage[0].DataPoints, point =>
        {
            Assert.Equal(point.BytesUploaded + point.BytesDownloaded, point.BytesTransferred);
        });
    }
}

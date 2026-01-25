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
        
        // Add specific routes for this test - use query patterns that match what MetricsService sends
        _fixture.PrometheusHttpHandler.AddRoute($"increase(shadowsocks_data_bytes{{access_key=\"{clientId}\"}}[30d])", $$"""
            {
                "status": "success",
                "data": {
                    "resultType": "vector",
                    "result": [
                        {
                            "metric": {
                                "access_key": "{{clientId}}",
                                "dir": "c>p",
                                "proto": "tcp"
                            },
                            "value": [1234567890, "1048576"]
                        },
                        {
                            "metric": {
                                "access_key": "{{clientId}}",
                                "dir": "c<p",
                                "proto": "tcp"
                            },
                            "value": [1234567890, "2097152"]
                        }
                    ]
                }
            }
            """);
        
        _fixture.PrometheusHttpHandler.AddRoute($"shadowsocks_tunnel_time_seconds{{access_key=\"{clientId}\"}}", $$"""
            {
                "status": "success",
                "data": {
                    "resultType": "vector",
                    "result": [
                        {
                            "metric": {
                                "access_key": "{{clientId}}"
                            },
                            "value": [1234567890, "3600.5"]
                        }
                    ]
                }
            }
            """);
        
        _fixture.PrometheusHttpHandler.AddRoute($"increase(shadowsocks_tcp_connections_closed{{access_key=\"{clientId}\"}}[30d])", $$"""
            {
                "status": "success",
                "data": {
                    "resultType": "vector",
                    "result": [
                        {
                            "metric": {
                                "access_key": "{{clientId}}",
                                "status": "OK"
                            },
                            "value": [1234567890, "150"]
                        }
                    ]
                }
            }
            """);

        var metricsService = _fixture.Services.GetRequiredService<IMetricsService>();

        // Act
        var usage = await metricsService.GetClientUsageLast30DaysAsync(clientId);

        // Assert
        Assert.NotNull(usage);
        Assert.Equal(3145728, usage.TotalBytesTransferred); // 1MB upload + 2MB download
        Assert.Equal(1048576, usage.BytesUploaded); // 1MB
        Assert.Equal(2097152, usage.BytesDownloaded); // 2MB
        Assert.Equal(3600.5, usage.TunnelTimeSeconds);
        Assert.Equal(150, usage.TotalConnections);
    }

    [Fact]
    public async Task When_GetAllClientsHourlyUsage_WithNoClients_ReturnsEmptyList()
    {
        // Arrange
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
            IsActive = true
        };

        await _fixture.SetClient([client1]);

        var metricsService = _fixture.Services.GetRequiredService<IMetricsService>();

        // Act
        var usage = await metricsService.GetAllClientsDailyUsageAsync(30);

        // Assert
        Assert.NotNull(usage);
        Assert.Single(usage);
        Assert.Equal("1", usage[0].ClientId);
        Assert.Equal("Client1", usage[0].ClientName);
        Assert.Equal(30, usage[0].DataPoints.Count);
        Assert.All(usage[0].DataPoints, dp =>
        {
            Assert.True(dp.Date <= DateTime.UtcNow.Date);
            Assert.True(dp.Date >= DateTime.UtcNow.Date.AddDays(-30));
        });
    }
}

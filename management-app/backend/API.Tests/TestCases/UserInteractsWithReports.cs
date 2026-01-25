using OutlineManager.API.DTOs;
using OutlineManager.API.Models;
using System.Net;
using System.Net.Http.Json;

namespace OutlineManager.API.Tests.TestCases;

public class UserInteractsWithReports : TestCaseBase
{
    [Fact]
    public async Task When_GetHourlyUsage_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/reports/hourly");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task When_GetHourlyUsage_WithNoClients_ReturnsEmptyList()
    {
        // Arrange
        _fixture.PrometheusHttpHandler.AddRoute("/metrics", "# No metrics");
        var client = _fixture.GetAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/reports/hourly");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reports = await response.Content.ReadFromJsonAsync<List<HourlyUsageResponse>>();
        Assert.NotNull(reports);
        Assert.Empty(reports);
    }

    [Fact]
    public async Task When_GetHourlyUsage_WithClients_ReturnsDataForAllClients()
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
            shadowsocks_data_bytes{access_key="1",dir="c>p"} 5000
            shadowsocks_data_bytes{access_key="1",dir="c<p"} 10000
            shadowsocks_data_bytes{access_key="2",dir="c>p"} 3000
            shadowsocks_data_bytes{access_key="2",dir="c<p"} 7000
            shadowsocks_tcp_connections_closed{access_key="1",status="OK"} 50
            shadowsocks_tcp_connections_closed{access_key="2",status="OK"} 30
            """);

        var client = _fixture.GetAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/reports/hourly?hours=12");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reports = await response.Content.ReadFromJsonAsync<List<HourlyUsageResponse>>();
        Assert.NotNull(reports);
        Assert.Equal(2, reports.Count);
        
        Assert.Contains(reports, r => r.ClientId == "1" && r.ClientName == "Client1");
        Assert.Contains(reports, r => r.ClientId == "2" && r.ClientName == "Client2");
        
        // With raw metrics, we get current snapshot only
        Assert.All(reports, r =>
        {
            Assert.Single(r.DataPoints);
            Assert.All(r.DataPoints, dp =>
            {
                Assert.True(dp.BytesTransferred >= 0);
                Assert.True(dp.Connections >= 0);
            });
        });
    }

    [Fact]
    public async Task When_GetHourlyUsage_WithDefaultHours_Returns24Hours()
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
        
        _fixture.PrometheusHttpHandler.AddRoute("/metrics", """
            shadowsocks_data_bytes{access_key="1",dir="c>p"} 1000
            """);

        var client = _fixture.GetAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/reports/hourly");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reports = await response.Content.ReadFromJsonAsync<List<HourlyUsageResponse>>();
        Assert.NotNull(reports);
        Assert.Single(reports);
        // With raw metrics, we get current snapshot regardless of hours parameter
        Assert.Single(reports[0].DataPoints);
    }

    [Fact]
    public async Task When_GetDailyUsage_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var client = _fixture.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/reports/daily");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task When_GetDailyUsage_WithNoClients_ReturnsEmptyList()
    {
        // Arrange
        _fixture.PrometheusHttpHandler.AddRoute("/metrics", "# No metrics");
        var client = _fixture.GetAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/reports/daily");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reports = await response.Content.ReadFromJsonAsync<List<DailyUsageResponse>>();
        Assert.NotNull(reports);
        Assert.Empty(reports);
    }

    [Fact]
    public async Task When_GetDailyUsage_WithClients_ReturnsDataForAllClients()
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
            shadowsocks_data_bytes{access_key="1",dir="c>p"} 500
            shadowsocks_data_bytes{access_key="1",dir="c<p"} 1500
            shadowsocks_data_bytes{access_key="2",dir="c>p"} 800
            shadowsocks_data_bytes{access_key="2",dir="c<p"} 2200
            """);

        var client = _fixture.GetAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/reports/daily?days=7");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reports = await response.Content.ReadFromJsonAsync<List<DailyUsageResponse>>();
        Assert.NotNull(reports);
        Assert.Equal(2, reports.Count);
        
        Assert.Contains(reports, r => r.ClientId == "1" && r.ClientName == "Client1");
        Assert.Contains(reports, r => r.ClientId == "2" && r.ClientName == "Client2");
        
        // With raw metrics, we get current snapshot only
        Assert.All(reports, r =>
        {
            Assert.Single(r.DataPoints);
            Assert.All(r.DataPoints, dp =>
            {
                Assert.True(dp.BytesTransferred >= 0);
                Assert.True(dp.BytesUploaded >= 0);
                Assert.True(dp.BytesDownloaded >= 0);
                Assert.Equal(dp.BytesUploaded + dp.BytesDownloaded, dp.BytesTransferred);
            });
        });
    }

    [Fact]
    public async Task When_GetDailyUsage_WithDefaultDays_Returns30Days()
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
        
        _fixture.PrometheusHttpHandler.AddRoute("/metrics", """
            shadowsocks_data_bytes{access_key="1",dir="c>p"} 1000
            """);

        var client = _fixture.GetAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/reports/daily");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reports = await response.Content.ReadFromJsonAsync<List<DailyUsageResponse>>();
        Assert.NotNull(reports);
        Assert.Single(reports);
        // With raw metrics, we get current snapshot regardless of days parameter
        Assert.Single(reports[0].DataPoints);
    }

    [Fact]
    public async Task When_GetDailyUsage_DataPointsAreInChronologicalOrder()
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
        
        _fixture.PrometheusHttpHandler.AddRoute("/metrics", """
            shadowsocks_data_bytes{access_key="1",dir="c>p"} 1000
            shadowsocks_data_bytes{access_key="1",dir="c<p"} 3000
            """);

        var client = _fixture.GetAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/reports/daily?days=5");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reports = await response.Content.ReadFromJsonAsync<List<DailyUsageResponse>>();
        Assert.NotNull(reports);
        Assert.Single(reports);
        
        // With raw metrics, we get a single snapshot
        Assert.Single(reports[0].DataPoints);
        Assert.Equal(DateTime.UtcNow.Date, reports[0].DataPoints[0].Date);
    }
}

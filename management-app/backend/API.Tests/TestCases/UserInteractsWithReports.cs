using OutlineManager.API.DTOs;
using OutlineManager.API.Models;
using OutlineManager.API.Tests.TestHelpers;
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
        _fixture.SetupPrometheusRangeResponses([]);
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
        
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var bytesResponse = PrometheusResponseBuilder.BuildRange(
            new PrometheusResponseBuilder.RangeSeries(
                new Dictionary<string, string> { { "access_key", "1" }, { "dir", "c>p" } },
                [new(now, 5000)]
            ),
            new PrometheusResponseBuilder.RangeSeries(
                new Dictionary<string, string> { { "access_key", "1" }, { "dir", "c<p" } },
                [new(now, 10000)]
            ),
            new PrometheusResponseBuilder.RangeSeries(
                new Dictionary<string, string> { { "access_key", "2" }, { "dir", "c>p" } },
                [new(now, 3000)]
            ),
            new PrometheusResponseBuilder.RangeSeries(
                new Dictionary<string, string> { { "access_key", "2" }, { "dir", "c<p" } },
                [new(now, 7000)]
            )
        );
        var connectionsResponse = PrometheusResponseBuilder.BuildRange(
            new PrometheusResponseBuilder.RangeSeries(
                new Dictionary<string, string> { { "access_key", "1" } },
                [new(now, 50)]
            ),
            new PrometheusResponseBuilder.RangeSeries(
                new Dictionary<string, string> { { "access_key", "2" } },
                [new(now, 30)]
            )
        );

        _fixture.SetupPrometheusRangeResponses(new Dictionary<string, string>
        {
            { "shadowsocks_data_bytes", bytesResponse },
            { "shadowsocks_tcp_connections_closed", connectionsResponse }
        });

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
        
        Assert.All(reports, r =>
        {
            Assert.Equal(12, r.DataPoints.Count);
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
        
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var bytesResponse = PrometheusResponseBuilder.BuildRange(
            new PrometheusResponseBuilder.RangeSeries(
                new Dictionary<string, string> { { "access_key", "1" }, { "dir", "c>p" } },
                [new(now, 1000)]
            )
        );
        _fixture.SetupPrometheusRangeResponses(new Dictionary<string, string>
        {
            { "shadowsocks_data_bytes", bytesResponse }
        });

        var client = _fixture.GetAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/reports/hourly");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reports = await response.Content.ReadFromJsonAsync<List<HourlyUsageResponse>>();
        Assert.NotNull(reports);
        Assert.Single(reports);
        Assert.Equal(24, reports[0].DataPoints.Count);
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
        _fixture.SetupPrometheusRangeResponses([]);
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
        
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var bytesResponse = PrometheusResponseBuilder.BuildRange(
            new PrometheusResponseBuilder.RangeSeries(
                new Dictionary<string, string> { { "access_key", "1" }, { "dir", "c>p" } },
                [new(now, 500)]
            ),
            new PrometheusResponseBuilder.RangeSeries(
                new Dictionary<string, string> { { "access_key", "1" }, { "dir", "c<p" } },
                [new(now, 1500)]
            ),
            new PrometheusResponseBuilder.RangeSeries(
                new Dictionary<string, string> { { "access_key", "2" }, { "dir", "c>p" } },
                [new(now, 800)]
            ),
            new PrometheusResponseBuilder.RangeSeries(
                new Dictionary<string, string> { { "access_key", "2" }, { "dir", "c<p" } },
                [new(now, 2200)]
            )
        );
        _fixture.SetupPrometheusRangeResponses(new Dictionary<string, string>
        {
            { "shadowsocks_data_bytes", bytesResponse }
        });

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
        
        Assert.All(reports, r =>
        {
            Assert.Equal(7, r.DataPoints.Count);
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
        
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var bytesResponse = PrometheusResponseBuilder.BuildRange(
            new PrometheusResponseBuilder.RangeSeries(
                new Dictionary<string, string> { { "access_key", "1" }, { "dir", "c>p" } },
                [new(now, 1000)]
            )
        );
        _fixture.SetupPrometheusRangeResponses(new Dictionary<string, string>
        {
            { "shadowsocks_data_bytes", bytesResponse }
        });

        var client = _fixture.GetAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/reports/daily");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reports = await response.Content.ReadFromJsonAsync<List<DailyUsageResponse>>();
        Assert.NotNull(reports);
        Assert.Single(reports);
        Assert.Equal(30, reports[0].DataPoints.Count);
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
        
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var bytesResponse = PrometheusResponseBuilder.BuildRange(
            new PrometheusResponseBuilder.RangeSeries(
                new Dictionary<string, string> { { "access_key", "1" }, { "dir", "c>p" } },
                [new(now, 1000)]
            ),
            new PrometheusResponseBuilder.RangeSeries(
                new Dictionary<string, string> { { "access_key", "1" }, { "dir", "c<p" } },
                [new(now, 3000)]
            )
        );
        _fixture.SetupPrometheusRangeResponses(new Dictionary<string, string>
        {
            { "shadowsocks_data_bytes", bytesResponse }
        });

        var client = _fixture.GetAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/reports/daily?days=5");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reports = await response.Content.ReadFromJsonAsync<List<DailyUsageResponse>>();
        Assert.NotNull(reports);
        Assert.Single(reports);
        
        Assert.Equal(5, reports[0].DataPoints.Count);
        var ordered = reports[0].DataPoints.OrderBy(p => p.Date).ToList();
        Assert.Equal(ordered, reports[0].DataPoints);
    }
}

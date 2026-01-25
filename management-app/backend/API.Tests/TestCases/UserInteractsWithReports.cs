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
                Assert.True(dp.Timestamp <= DateTime.UtcNow);
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
                Assert.True(dp.Date <= DateTime.UtcNow.Date);
                Assert.True(dp.Date >= DateTime.UtcNow.Date.AddDays(-7));
                Assert.True(dp.BytesTransferred >= 0);
                Assert.True(dp.BytesUploaded >= 0);
                Assert.True(dp.BytesDownloaded >= 0);
                Assert.True(dp.Connections >= 0);
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

        var client = _fixture.GetAuthenticatedClient();

        // Act
        var response = await client.GetAsync("/api/v1/reports/daily?days=5");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reports = await response.Content.ReadFromJsonAsync<List<DailyUsageResponse>>();
        Assert.NotNull(reports);
        Assert.Single(reports);
        
        var dates = reports[0].DataPoints.Select(dp => dp.Date).ToList();
        Assert.Equal(dates.OrderBy(d => d).ToList(), dates);
    }
}

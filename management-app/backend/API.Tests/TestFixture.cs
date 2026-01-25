using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OutlineManager.API.Interfaces;
using OutlineManager.API.Models;
using OutlineManager.API.Tests.Mocks;
using System.Text.Json;

namespace OutlineManager.API.Tests;

public class TestFixture : WebApplicationFactory<Program>
{
    private readonly string _dataDirectory = Directory.CreateTempSubdirectory("OutlineManagerTests").FullName;
    private readonly MockHttpMessageHandler _mockHttpHandler = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DataDirectory", _dataDirectory },
                { "AppSettings:PrometheusUrl", "http://mock-prometheus:9091" }
            })
            .Build();

        builder.UseConfiguration(configuration);
        
        builder.ConfigureServices(services =>
        {
            // Replace HttpClient for MetricsService with mocked version
            services.AddHttpClient<IMetricsService, Services.MetricsService>()
                .ConfigurePrimaryHttpMessageHandler(() => _mockHttpHandler);
        });

        base.ConfigureWebHost(builder);
        
        // Setup default Prometheus mock responses
        SetupDefaultPrometheusMocks();
    }

    private void SetupDefaultPrometheusMocks()
    {
        // Default empty responses for metrics
        _mockHttpHandler.AddRoute("shadowsocks_data_bytes", """
            {
                "status": "success",
                "data": {
                    "resultType": "vector",
                    "result": []
                }
            }
            """);
        
        _mockHttpHandler.AddRoute("shadowsocks_tunnel_time_seconds", """
            {
                "status": "success",
                "data": {
                    "resultType": "vector",
                    "result": []
                }
            }
            """);
        
        _mockHttpHandler.AddRoute("shadowsocks_tcp_connections_closed", """
            {
                "status": "success",
                "data": {
                    "resultType": "vector",
                    "result": []
                }
            }
            """);
    }

    public MockHttpMessageHandler PrometheusHttpHandler => _mockHttpHandler;

    override protected void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                Directory.Delete(_dataDirectory, true);
            }
            catch { }
        }
        base.Dispose(disposing);
    }

    public HttpClient GetHttpClient(User user)
    {
        var token = Services.GetRequiredService<IJwtService>().GenerateToken(user);

        var client = CreateClient();

        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

        return client;
    }

    public Task SetClient(params IEnumerable<Client> clients)
    {
        var filePath = Path.Combine(_dataDirectory, "clients.json");
        var json = JsonSerializer.Serialize(clients);
        return File.WriteAllTextAsync(filePath, json);
    }

    public Task SetUsers(params IEnumerable<User> users)
    {
        var filePath = Path.Combine(_dataDirectory, "users.json");
        var json = JsonSerializer.Serialize(users);
        return File.WriteAllTextAsync(filePath, json);
    }

    public HttpClient GetAuthenticatedClient()
    {
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "testuser",
            PasswordHash = "hashedpassword"
        };
        return GetHttpClient(user);
    }
}

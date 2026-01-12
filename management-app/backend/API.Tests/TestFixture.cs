using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OutlineManager.API.Interfaces;
using OutlineManager.API.Models;
using System.Text.Json;

namespace OutlineManager.API.Tests;

public class TestFixture : WebApplicationFactory<Program>
{
    private readonly string _dataDirectory = Directory.CreateTempSubdirectory("OutlineManagerTests").FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DataDirectory", _dataDirectory }
            })
            .Build();

        builder.UseConfiguration(configuration);

        base.ConfigureWebHost(builder);
    }

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
}

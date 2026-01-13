using OutlineManager.API.Interfaces;
using OutlineManager.API.Models;
using System.Text.Json.Serialization.Metadata;

namespace OutlineManager.API.Repositories;

public class ClientRepository : RepositoryBase<Client>, IClientRepository
{
    private readonly ILogger<ClientRepository> _logger;

    protected override string DataFilePath { get; }

    protected override JsonTypeInfo<List<Client>> JsonTypeInfo => AppJsonSerializerContext.Default.ListClient;

    public ClientRepository(IConfiguration configuration, ILogger<ClientRepository> logger)
        : base(logger)
    {
        var dataDirectory = configuration["DataDirectory"] ?? "/app/data";
        Directory.CreateDirectory(dataDirectory);
        DataFilePath = Path.Combine(dataDirectory, "clients.json");
        _logger = logger;
    }

    public async Task<Client?> GetByIdAsync(string id)
    {
        var clients = await LoadAsync();
        return clients.FirstOrDefault(c => c.Id == id);
    }

    public async Task<Client?> GetByNameAsync(string name)
    {
        var clients = await LoadAsync();
        return clients.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IEnumerable<Client>> GetAllAsync()
    {
        return await LoadAsync();
    }

    public Task<Client> CreateAsync(string name)
    {
        var secret = GenerateSecret();

        var client = new Client
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Secret = secret,
            Cipher = "chacha20-ietf-poly1305",
            IsActive = true
        };

        return WithFileLockAsync(async () =>
        {
            var clients = (await LoadAsync()).ToList();
            clients.Add(client);
            await SaveAsync(clients);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Created client: {ClientId} with name: {ClientName}", client.Id, client.Name);
            }

            return client;
        });
    }

    public Task<Client?> UpdateAsync(string id, Client client)
    {
        return WithFileLockAsync(async () =>
        {
            var clients = (await LoadAsync()).ToList();
            var existingClient = clients.FirstOrDefault(c => c.Id == id);

            if (existingClient == null)
                return null;

            existingClient.Name = client.Name;
            existingClient.Secret = client.Secret;
            existingClient.Cipher = client.Cipher;
            existingClient.IsActive = client.IsActive;

            await SaveAsync(clients);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Updated client: {ClientId}", id);
            }

            return existingClient;
        });
    }

    public Task<bool> DeleteAsync(string id)
    {
        return WithFileLockAsync(async () =>
        {
            var clients = (await LoadAsync()).ToList();
            var client = clients.FirstOrDefault(c => c.Id == id);

            if (client == null)
                return false;

            clients.Remove(client);
            await SaveAsync(clients);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Deleted client: {ClientId}", id);
            }

            return true;
        });
    }

    public async Task<bool> ExistsAsync(string name)
    {
        var client = await GetByNameAsync(name);
        return client != null;
    }

    private static string GenerateSecret()
    {
        return Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
    }
}

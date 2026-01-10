using System.Text.Json;
using OutlineManager.API.Interfaces;
using OutlineManager.API.Models;

namespace OutlineManager.API.Repositories;

public class ClientRepository : IClientRepository
{
    private readonly string _dataFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly ILogger<ClientRepository> _logger;

    public ClientRepository(IConfiguration configuration, ILogger<ClientRepository> logger)
    {
        var dataDirectory = configuration["DataDirectory"] ?? "/app/data";
        Directory.CreateDirectory(dataDirectory);
        _dataFilePath = Path.Combine(dataDirectory, "clients.json");
        _logger = logger;
    }

    public async Task<Client?> GetByIdAsync(string id)
    {
        var clients = await LoadClientsAsync();
        return clients.FirstOrDefault(c => c.Id == id);
    }

    public async Task<Client?> GetByNameAsync(string name)
    {
        var clients = await LoadClientsAsync();
        return clients.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IEnumerable<Client>> GetAllAsync()
    {
        return await LoadClientsAsync();
    }

    public async Task<Client> CreateAsync(string name)
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

        await _fileLock.WaitAsync();
        try
        {
            var clients = (await LoadClientsAsync()).ToList();
            clients.Add(client);
            await SaveClientsAsync(clients);
            
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Created client: {ClientId} with name: {ClientName}", client.Id, client.Name);
            }
            
            return client;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<Client?> UpdateAsync(string id, Client client)
    {
        await _fileLock.WaitAsync();
        try
        {
            var clients = (await LoadClientsAsync()).ToList();
            var existingClient = clients.FirstOrDefault(c => c.Id == id);
            
            if (existingClient == null)
                return null;

            existingClient.Name = client.Name;
            existingClient.Secret = client.Secret;
            existingClient.Cipher = client.Cipher;
            existingClient.IsActive = client.IsActive;

            await SaveClientsAsync(clients);
            
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Updated client: {ClientId}", id);
            }
            
            return existingClient;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<bool> DeleteAsync(string id)
    {
        await _fileLock.WaitAsync();
        try
        {
            var clients = (await LoadClientsAsync()).ToList();
            var client = clients.FirstOrDefault(c => c.Id == id);
            
            if (client == null)
                return false;

            clients.Remove(client);
            await SaveClientsAsync(clients);
            
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Deleted client: {ClientId}", id);
            }
            
            return true;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<bool> ExistsAsync(string name)
    {
        var client = await GetByNameAsync(name);
        return client != null;
    }

    private async Task<List<Client>> LoadClientsAsync()
    {
        if (!File.Exists(_dataFilePath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(_dataFilePath);
            return JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.ListClient) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load clients from file");
            return [];
        }
    }

    private async Task SaveClientsAsync(IEnumerable<Client> clients)
    {
        var json = JsonSerializer.Serialize(clients, AppJsonSerializerContext.Default.IEnumerableClient);
        await File.WriteAllTextAsync(_dataFilePath, json);
    }

    private static string GenerateSecret()
    {
        return Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
    }
}

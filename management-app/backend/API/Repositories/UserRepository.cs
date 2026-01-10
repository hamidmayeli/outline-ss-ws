using System.Text.Json;
using OutlineManager.API.Interfaces;
using OutlineManager.API.Models;

namespace OutlineManager.API.Repositories;

public class UserRepository : IUserRepository
{
    private readonly string _dataFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(IConfiguration configuration, ILogger<UserRepository> logger)
    {
        var dataDirectory = configuration["DataDirectory"] ?? "/app/data";
        Directory.CreateDirectory(dataDirectory);
        _dataFilePath = Path.Combine(dataDirectory, "users.json");
        _logger = logger;
    }

    public async Task<User?> GetByIdAsync(string id)
    {
        var users = await LoadUsersAsync();
        return users.FirstOrDefault(u => u.Id == id);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        var users = await LoadUsersAsync();
        return users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await LoadUsersAsync();
    }

    public async Task<User> CreateAsync(User user)
    {
        await _fileLock.WaitAsync();
        try
        {
            var users = (await LoadUsersAsync()).ToList();
            user.Id = Guid.NewGuid().ToString();
            user.CreatedAt = DateTime.UtcNow;
            users.Add(user);
            await SaveUsersAsync(users);
            
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Created user: {UserId} with username: {Username}", user.Id, user.Username);
            }
            
            return user;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<User?> UpdateAsync(string id, User user)
    {
        await _fileLock.WaitAsync();
        try
        {
            var users = (await LoadUsersAsync()).ToList();
            var existingUser = users.FirstOrDefault(u => u.Id == id);
            
            if (existingUser == null)
                return null;

            existingUser.Username = user.Username;
            existingUser.PasswordHash = user.PasswordHash;
            existingUser.UpdatedAt = DateTime.UtcNow;

            await SaveUsersAsync(users);
            
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Updated user: {UserId}", id);
            }
            
            return existingUser;
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
            var users = (await LoadUsersAsync()).ToList();
            var user = users.FirstOrDefault(u => u.Id == id);
            
            if (user == null)
                return false;

            users.Remove(user);
            await SaveUsersAsync(users);
            
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Deleted user: {UserId}", id);
            }
            
            return true;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<bool> ExistsAsync(string username)
    {
        var user = await GetByUsernameAsync(username);
        return user != null;
    }

    private async Task<List<User>> LoadUsersAsync()
    {
        if (!File.Exists(_dataFilePath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(_dataFilePath);
            return JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.ListUser) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load users from file");
            return [];
        }
    }

    private async Task SaveUsersAsync(IEnumerable<User> users)
    {
        var json = JsonSerializer.Serialize(users, AppJsonSerializerContext.Default.IEnumerableUser);
        await File.WriteAllTextAsync(_dataFilePath, json);
    }
}

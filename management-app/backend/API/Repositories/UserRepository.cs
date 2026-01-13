using System.Text.Json.Serialization.Metadata;
using OutlineManager.API.Interfaces;
using OutlineManager.API.Models;

namespace OutlineManager.API.Repositories;

public class UserRepository : RepositoryBase<User>, IUserRepository
{
    private readonly ILogger<UserRepository> _logger;

    protected override string DataFilePath { get; }

    protected override JsonTypeInfo<List<User>> JsonTypeInfo => AppJsonSerializerContext.Default.ListUser;

    public UserRepository(IConfiguration configuration, ILogger<UserRepository> logger)
        : base(logger)
    {
        var dataDirectory = configuration["DataDirectory"] ?? "/app/data";
        Directory.CreateDirectory(dataDirectory);
        DataFilePath = Path.Combine(dataDirectory, "users.json");
        _logger = logger;
    }

    public async Task<User?> GetByIdAsync(string id)
    {
        var users = await LoadAsync();
        return users.FirstOrDefault(u => u.Id == id);
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        var users = await LoadAsync();
        return users.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await LoadAsync();
    }

    public Task<User> CreateAsync(User user)
    {
        return WithFileLockAsync(async () =>
        {
            var users = (await LoadAsync()).ToList();
            user.Id = Guid.NewGuid().ToString();
            user.CreatedAt = DateTime.UtcNow;
            users.Add(user);
            await SaveAsync(users);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Created user: {UserId} with username: {Username}", user.Id, user.Username);
            }

            return user;
        });
    }

    public Task<User?> UpdateAsync(string id, User user)
    {
        return WithFileLockAsync(async () =>
        {
            var users = (await LoadAsync()).ToList();
            var existingUser = users.FirstOrDefault(u => u.Id == id);

            if (existingUser == null)
                return null;

            existingUser.Username = user.Username;
            existingUser.PasswordHash = user.PasswordHash;
            existingUser.UpdatedAt = DateTime.UtcNow;

            await SaveAsync(users);

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Updated user: {UserId}", id);
            }

            return existingUser;
        });
    }

    public Task<bool> DeleteAsync(string id)
    {
        return WithFileLockAsync(async () =>
        {
            var users = (await LoadAsync()).ToList();
            var user = users.FirstOrDefault(u => u.Id == id);

            if (user == null)
                return false;

            users.Remove(user);
            await SaveAsync(users);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Deleted user: {UserId}", id);
            }

            return true;
        });
    }

    public async Task<bool> ExistsAsync(string username)
    {
        var user = await GetByUsernameAsync(username);
        return user != null;
    }
}

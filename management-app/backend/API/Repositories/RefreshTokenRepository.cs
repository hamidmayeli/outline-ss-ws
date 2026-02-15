using System.Text.Json.Serialization.Metadata;
using OutlineManager.API.Interfaces;
using OutlineManager.API.Models;

namespace OutlineManager.API.Repositories;

public class RefreshTokenRepository : RepositoryBase<RefreshToken>, IRefreshTokenRepository
{
    private readonly ILogger<RefreshTokenRepository> _logger;

    protected override string DataFilePath { get; }

    protected override JsonTypeInfo<List<RefreshToken>> JsonTypeInfo => AppJsonSerializerContext.Default.ListRefreshToken;

    public RefreshTokenRepository(IConfiguration configuration, ILogger<RefreshTokenRepository> logger)
        : base(logger)
    {
        var dataDirectory = configuration["DataDirectory"] ?? "/app/data";
        Directory.CreateDirectory(dataDirectory);
        DataFilePath = Path.Combine(dataDirectory, "refresh-tokens.json");
        _logger = logger;
    }

    public Task<RefreshToken> CreateAsync(RefreshToken refreshToken)
    {
        return WithFileLockAsync(async () =>
        {
            var tokens = (await LoadAsync()).ToList();
            RemoveExpired(tokens);
            tokens.Add(refreshToken);
            await SaveAsync(tokens);
            return refreshToken;
        });
    }

    public Task<RefreshToken?> GetByTokenAsync(string token)
    {
        return WithFileLockAsync(async () =>
        {
            var tokens = (await LoadAsync()).ToList();
            RemoveExpired(tokens);

            var refreshToken = tokens.FirstOrDefault(t => t.Token == token);
            if (refreshToken == null)
            {
                await SaveAsync(tokens);
                return null;
            }

            await SaveAsync(tokens);
            return refreshToken;
        });
    }

    public Task<RefreshToken?> RotateAsync(string token, string newToken, DateTime expiresAt)
    {
        return WithFileLockAsync(async () =>
        {
            var tokens = (await LoadAsync()).ToList();
            RemoveExpired(tokens);

            var refreshToken = tokens.FirstOrDefault(t => t.Token == token);
            if (refreshToken == null)
            {
                await SaveAsync(tokens);
                return null;
            }

            refreshToken.Token = newToken;
            refreshToken.ExpiresAt = expiresAt;
            refreshToken.LastUsedAt = DateTime.UtcNow;

            await SaveAsync(tokens);
            return refreshToken;
        });
    }

    public Task<bool> DeleteByTokenAsync(string token)
    {
        return WithFileLockAsync(async () =>
        {
            var tokens = (await LoadAsync()).ToList();
            var removed = tokens.RemoveAll(t => t.Token == token || t.ExpiresAt <= DateTime.UtcNow) > 0;
            await SaveAsync(tokens);
            return removed;
        });
    }

    public Task<int> DeleteExpiredAsync()
    {
        return WithFileLockAsync(async () =>
        {
            var tokens = (await LoadAsync()).ToList();
            var removed = RemoveExpired(tokens);
            if (removed > 0)
            {
                await SaveAsync(tokens);
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Deleted {Count} expired refresh tokens", removed);
                }
            }

            return removed;
        });
    }

    private static int RemoveExpired(List<RefreshToken> tokens)
    {
        return tokens.RemoveAll(t => t.ExpiresAt <= DateTime.UtcNow);
    }
}

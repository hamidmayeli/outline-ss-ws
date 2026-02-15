using OutlineManager.API.Models;

namespace OutlineManager.API.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken> CreateAsync(RefreshToken refreshToken);
    Task<RefreshToken?> GetByTokenAsync(string token);
    Task<RefreshToken?> RotateAsync(string token, string newToken, DateTime expiresAt);
    Task<bool> DeleteByTokenAsync(string token);
    Task<int> DeleteExpiredAsync();
}

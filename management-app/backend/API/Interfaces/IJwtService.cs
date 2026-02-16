using OutlineManager.API.Models;

namespace OutlineManager.API.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user);
    string? ValidateToken(string token);
    Task<RefreshToken> GenerateRefreshTokenAsync(User user);
    Task<(User? User, RefreshToken? RefreshToken)> ValidateAndRefreshTokenAsync(string refreshToken);
    Task DeleteRefreshTokenAsync(string refreshToken);
}

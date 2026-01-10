using OutlineManager.API.Models;

namespace OutlineManager.API.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user);
    string? ValidateToken(string token);
}

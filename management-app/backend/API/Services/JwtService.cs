using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using OutlineManager.API.Interfaces;
using OutlineManager.API.Models;

namespace OutlineManager.API.Services;

public class JwtService(
    IConfiguration configuration,
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository) : IJwtService
{
    private readonly string _secretKey = configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
    private readonly string _issuer = configuration["Jwt:Issuer"] ?? "OutlineManager";
    private readonly string _audience = configuration["Jwt:Audience"] ?? "OutlineManagerAPI";
    private readonly int _expirationMinutes = int.Parse(configuration["Jwt:ExpirationMinutes"] ?? "15");
    private readonly int _refreshTokenExpirationDays = int.Parse(configuration["Jwt:RefreshTokenExpirationDays"] ?? "30");
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;

    public string GenerateToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_secretKey);
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.Username)
            ]),
            Expires = DateTime.UtcNow.AddMinutes(_expirationMinutes),
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public string? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_secretKey);

        try
        {
            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var userId = jwtToken.Claims.First(x => x.Type == ClaimTypes.NameIdentifier).Value;

            return userId;
        }
        catch
        {
            return null;
        }
    }

    public async Task<RefreshToken> GenerateRefreshTokenAsync(User user)
    {
        await _refreshTokenRepository.DeleteExpiredAsync();

        var refreshToken = new RefreshToken
        {
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenExpirationDays),
            LastUsedAt = DateTime.UtcNow,
        };

        await _refreshTokenRepository.CreateAsync(refreshToken);
        return refreshToken;
    }

    public async Task<(User? User, RefreshToken? RefreshToken)> ValidateAndRefreshTokenAsync(string refreshToken)
    {
        await _refreshTokenRepository.DeleteExpiredAsync();

        var storedToken = await _refreshTokenRepository.GetByTokenAsync(refreshToken);
        if (storedToken == null)
        {
            return (null, null);
        }

        var user = await _userRepository.GetByIdAsync(storedToken.UserId);
        if (user == null)
        {
            await _refreshTokenRepository.DeleteByTokenAsync(refreshToken);
            return (null, null);
        }

        var rotatedToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var expiresAt = DateTime.UtcNow.AddDays(_refreshTokenExpirationDays);
        var refreshed = await _refreshTokenRepository.RotateAsync(refreshToken, rotatedToken, expiresAt);

        if (refreshed == null)
        {
            return (null, null);
        }

        return (user, refreshed);
    }

    public async Task DeleteRefreshTokenAsync(string refreshToken)
    {
        await _refreshTokenRepository.DeleteByTokenAsync(refreshToken);
    }
}

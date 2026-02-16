using OutlineManager.API.DTOs;
using OutlineManager.API.Models;
using OutlineManager.API.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

namespace OutlineManager.API.Tests.TestCases;

public class UserInteractsWithRefreshTokens : TestCaseBase
{
    [Fact]
    public async Task When_Login_SetsSecureHttpOnlyRefreshCookie()
    {
        var request = new LoginRequest
        {
            Username = "admin",
            Password = "password123"
        };

        var client = _fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookieHeaders));

        var refreshCookie = cookieHeaders.FirstOrDefault(v => v.StartsWith("refreshToken=", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(refreshCookie);
        Assert.Contains("HttpOnly", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Secure", refreshCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task When_RefreshTokenIsValid_ReturnsNewAccessTokenAndRotatesRefreshToken()
    {
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "existinguser",
            PasswordHash = PasswordHasher.HashPassword("password123")
        };

        await _fixture.SetUsers([user]);

        var loginClient = _fixture.CreateClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest
        {
            Username = "existinguser",
            Password = "password123"
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var setCookie = loginResponse.Headers.GetValues("Set-Cookie")
            .First(v => v.StartsWith("refreshToken=", StringComparison.OrdinalIgnoreCase));
        var originalRefreshToken = ExtractCookieValue(setCookie, "refreshToken");
        Assert.False(string.IsNullOrWhiteSpace(originalRefreshToken));

        var refreshClient = _fixture.CreateClient();
        refreshClient.DefaultRequestHeaders.Add("Cookie", $"refreshToken={originalRefreshToken}");

        var refreshResponse = await refreshClient.GetAsync("/api/v1/auth/refreshToken");
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var refreshedPayload = await refreshResponse.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(refreshedPayload);
        Assert.Equal("existinguser", refreshedPayload.Username);
        Assert.NotEmpty(refreshedPayload.Token);

        var rotatedSetCookie = refreshResponse.Headers.GetValues("Set-Cookie")
            .First(v => v.StartsWith("refreshToken=", StringComparison.OrdinalIgnoreCase));
        var rotatedRefreshToken = ExtractCookieValue(rotatedSetCookie, "refreshToken");

        Assert.False(string.IsNullOrWhiteSpace(rotatedRefreshToken));
        Assert.NotEqual(originalRefreshToken, rotatedRefreshToken);

        var storedTokens = await _fixture.GetRefreshTokens();
        Assert.Single(storedTokens);
        Assert.Equal(Uri.UnescapeDataString(rotatedRefreshToken), storedTokens[0].Token);
        Assert.True(storedTokens[0].ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task When_RefreshTokenIsExpired_RefreshReturnsUnauthorizedAndExpiredTokenIsCleaned()
    {
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "existinguser",
            PasswordHash = PasswordHasher.HashPassword("password123")
        };

        var expiredToken = new RefreshToken
        {
            Token = "expired-token-value",
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-40),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            LastUsedAt = DateTime.UtcNow.AddDays(-1)
        };

        await _fixture.SetUsers([user]);
        await _fixture.SetRefreshTokens([expiredToken]);

        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "refreshToken=expired-token-value");

        var response = await client.GetAsync("/api/v1/auth/refreshToken");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var remainingTokens = await _fixture.GetRefreshTokens();
        Assert.Empty(remainingTokens);
    }

    [Fact]
    public async Task When_LogoutIsCalled_RefreshTokenIsRevoked()
    {
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "existinguser",
            PasswordHash = PasswordHasher.HashPassword("password123")
        };

        var token = new RefreshToken
        {
            Token = "active-token-value",
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            LastUsedAt = DateTime.UtcNow
        };

        await _fixture.SetUsers([user]);
        await _fixture.SetRefreshTokens([token]);

        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", "refreshToken=active-token-value");

        var logoutResponse = await client.DeleteAsync("/api/v1/auth/login");
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var remainingTokens = await _fixture.GetRefreshTokens();
        Assert.Empty(remainingTokens);
    }

    [Fact]
    public async Task When_AccessTokenExpires_ProtectedEndpointReturnsUnauthorized()
    {
        var configuration = _fixture.Services.GetRequiredService<IConfiguration>();
        var secretKey = configuration["Jwt:SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
        var issuer = configuration["Jwt:Issuer"] ?? "OutlineManager";
        var audience = configuration["Jwt:Audience"] ?? "OutlineManagerAPI";

        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "expired-user",
            PasswordHash = "hash"
        };

        var expiredToken = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.Username)
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-10),
            expires: DateTime.UtcNow.AddMinutes(-5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                SecurityAlgorithms.HmacSha256)
        ));

        var client = _fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        var response = await client.GetAsync("/api/v1/clients/");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string ExtractCookieValue(string cookieHeader, string cookieName)
    {
        var parts = cookieHeader.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var cookie = parts.FirstOrDefault(p => p.StartsWith($"{cookieName}=", StringComparison.OrdinalIgnoreCase));
        if (cookie == null)
        {
            return string.Empty;
        }

        var index = cookie.IndexOf('=');
        return index >= 0 ? Uri.UnescapeDataString(cookie[(index + 1)..]) : string.Empty;
    }
}

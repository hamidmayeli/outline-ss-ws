using OutlineManager.API.DTOs;
using OutlineManager.API.Models;
using OutlineManager.API.Services;
using System.Net;
using System.Net.Http.Json;

namespace OutlineManager.API.Tests.TestCases;

public class UserInteractsWithAuth : TestCaseBase
{
    [Fact]
    public async Task When_FirstLogin_Creates_NewUser()
    {
        var request = new LoginRequest
        {
            Username = "admin",
            Password = "password123"
        };

        var client = _fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginResponse);
        Assert.Equal("admin", loginResponse.Username);
        Assert.NotEmpty(loginResponse.Token);
    }

    [Fact]
    public async Task When_LoginWithExistingUser_ReturnsToken()
    {
        var passwordHash = PasswordHasher.HashPassword("password123");
        
        await _fixture.SetUsers(new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "existinguser",
            PasswordHash = passwordHash
        });

        var request = new LoginRequest
        {
            Username = "existinguser",
            Password = "password123"
        };

        var client = _fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(loginResponse);
        Assert.Equal("existinguser", loginResponse.Username);
        Assert.NotEmpty(loginResponse.Token);
    }

    [Fact]
    public async Task When_LoginWithWrongPassword_ReturnsUnauthorized()
    {
        var passwordHash = PasswordHasher.HashPassword("correctpassword");
        
        await _fixture.SetUsers(new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "existinguser",
            PasswordHash = passwordHash
        });

        var request = new LoginRequest
        {
            Username = "existinguser",
            Password = "wrongpassword"
        };

        var client = _fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task When_LoginWithNonExistingUserAfterUsersExist_ReturnsUnauthorized()
    {
        var passwordHash = PasswordHasher.HashPassword("password123");
        
        await _fixture.SetUsers(new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "existinguser",
            PasswordHash = passwordHash
        });

        var request = new LoginRequest
        {
            Username = "nonexistinguser",
            Password = "password123"
        };

        var client = _fixture.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

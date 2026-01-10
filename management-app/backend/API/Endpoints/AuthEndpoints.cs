using Microsoft.AspNetCore.Http.HttpResults;
using OutlineManager.API.DTOs;
using OutlineManager.API.Interfaces;
using OutlineManager.API.Models;
using OutlineManager.API.Services;

namespace OutlineManager.API.Endpoints;

public static class AuthEndpoints
{
    extension(RouteGroupBuilder group)
    {
        public RouteGroupBuilder MapAuthEndpoints()
        {
            group.MapPost("/login", LoginAsync)
                .WithName("Login")
                .AllowAnonymous();

            return group;
        }
    }

    private static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> LoginAsync(
        LoginRequest request,
        IUserRepository userRepository,
        IJwtService jwtService)
    {
        var users = await userRepository.GetAllAsync();

        if (users.Any()) return await AuthenticateUser(request, users, jwtService);

        else return await CreateUser(request, userRepository, jwtService);
    }

    private static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> AuthenticateUser(
        LoginRequest request,
        IEnumerable<User> users,
        IJwtService jwtService)
    {
        var user = users.FirstOrDefault(u => u.Username == request.Username);

        if (user == null || !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return TypedResults.Unauthorized();
        }

        var token = jwtService.GenerateToken(user);

        return TypedResults.Ok(new LoginResponse
        {
            Token = token,
            Username = user.Username
        });
    }

    private static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> CreateUser(
        LoginRequest request,
        IUserRepository userRepository,
        IJwtService jwtService)
    {
        var newUser = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = request.Username,
            PasswordHash = PasswordHasher.HashPassword(request.Password)
        };

        await userRepository.CreateAsync(newUser);
        var token = jwtService.GenerateToken(newUser);

        return TypedResults.Ok(new LoginResponse
        {
            Token = token,
            Username = newUser.Username
        });
    }
}
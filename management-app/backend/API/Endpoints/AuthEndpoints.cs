using Microsoft.AspNetCore.Http.HttpResults;
using OutlineManager.API.DTOs;
using OutlineManager.API.Interfaces;
using OutlineManager.API.Models;
using OutlineManager.API.Services;

namespace OutlineManager.API.Endpoints;

public static class AuthEndpoints
{
    private const string RefreshTokenCookieKey = "refreshToken";

    extension(RouteGroupBuilder group)
    {
        public RouteGroupBuilder MapAuthEndpoints()
        {
            group.MapPost("/login", LoginAsync)
                .WithName("Login")
                .AllowAnonymous();

            group.MapGet("/refreshToken", RefreshTokenAsync)
                .WithName("RefreshToken")
                .AllowAnonymous();

            group.MapDelete("/login", LogoutAsync)
                .WithName("Logout")
                .AllowAnonymous();

            return group;
        }
    }

    private static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> LoginAsync(
        LoginRequest request,
        IUserRepository userRepository,
        IJwtService jwtService,
        HttpContext context)
    {
        var users = await userRepository.GetAllAsync();

        if (users.Any()) return await AuthenticateUser(request, users, jwtService, context);

        else return await CreateUser(request, userRepository, jwtService, context);
    }

    private static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> AuthenticateUser(
        LoginRequest request,
        IEnumerable<User> users,
        IJwtService jwtService,
        HttpContext context)
    {
        var user = users.FirstOrDefault(u => u.Username == request.Username);

        if (user == null || !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return TypedResults.Unauthorized();
        }

        var token = jwtService.GenerateToken(user);
        var refreshToken = await jwtService.GenerateRefreshTokenAsync(user);

        SetRefreshTokenCookie(context, refreshToken);

        return TypedResults.Ok(new LoginResponse
        {
            Token = token,
            Username = user.Username
        });
    }

    private static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> CreateUser(
        LoginRequest request,
        IUserRepository userRepository,
        IJwtService jwtService,
        HttpContext context)
    {
        var newUser = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = request.Username,
            PasswordHash = PasswordHasher.HashPassword(request.Password)
        };

        await userRepository.CreateAsync(newUser);
        var token = jwtService.GenerateToken(newUser);
        var refreshToken = await jwtService.GenerateRefreshTokenAsync(newUser);

        SetRefreshTokenCookie(context, refreshToken);

        return TypedResults.Ok(new LoginResponse
        {
            Token = token,
            Username = newUser.Username
        });
    }

    private static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> RefreshTokenAsync(
        HttpContext context,
        IJwtService jwtService)
    {
        if (!context.Request.Cookies.TryGetValue(RefreshTokenCookieKey, out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
        {
            return TypedResults.Unauthorized();
        }

        var (user, refreshedToken) = await jwtService.ValidateAndRefreshTokenAsync(refreshToken);
        if (user == null || refreshedToken == null)
        {
            DeleteRefreshTokenCookie(context);
            return TypedResults.Unauthorized();
        }

        SetRefreshTokenCookie(context, refreshedToken);

        return TypedResults.Ok(new LoginResponse
        {
            Token = jwtService.GenerateToken(user),
            Username = user.Username,
        });
    }

    private static async Task<NoContent> LogoutAsync(HttpContext context, IJwtService jwtService)
    {
        if (context.Request.Cookies.TryGetValue(RefreshTokenCookieKey, out var refreshToken) && !string.IsNullOrWhiteSpace(refreshToken))
        {
            await jwtService.DeleteRefreshTokenAsync(refreshToken);
        }

        DeleteRefreshTokenCookie(context);
        return TypedResults.NoContent();
    }

    private static void SetRefreshTokenCookie(HttpContext context, RefreshToken refreshToken)
    {
        context.Response.Cookies.Append(
            RefreshTokenCookieKey,
            refreshToken.Token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                Expires = refreshToken.ExpiresAt,
                SameSite = SameSiteMode.Strict,
                IsEssential = true,
            });
    }

    private static void DeleteRefreshTokenCookie(HttpContext context)
    {
        context.Response.Cookies.Delete(
            RefreshTokenCookieKey,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                IsEssential = true,
            });
    }
}
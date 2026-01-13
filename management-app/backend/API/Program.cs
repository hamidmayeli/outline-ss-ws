using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OutlineManager.API;
using OutlineManager.API.Endpoints;
using OutlineManager.API.Interfaces;
using OutlineManager.API.Models;
using OutlineManager.API.Repositories;
using OutlineManager.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure JSON serialization for Native AOT
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

// Configure AppSettings
var appSettings = new AppSettings
{
    Domain = builder.Configuration["AppSettings:Domain"] ?? "localhost",
    TcpPath = builder.Configuration["AppSettings:TcpPath"] ?? "/tcp-ws",
    UdpPath = builder.Configuration["AppSettings:UdpPath"] ?? "/udp-ws",
    OutlineConfigPath = builder.Configuration["AppSettings:OutlineConfigPath"] ?? "/etc/outline/config.yaml"
};
builder.Services.AddSingleton(appSettings);

// Configure JWT Authentication
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? 
    throw new InvalidOperationException("JWT SecretKey is not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "OutlineManager",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "OutlineManagerAPI",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Register services
builder.Services.AddSingleton<IUserRepository, UserRepository>();
builder.Services.AddSingleton<IClientRepository, ClientRepository>();
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddSingleton<IOutlineSyncService, OutlineSyncService>();
builder.Services.AddSingleton<IIPResolver, IPResolver>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add OpenAPI
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenApi();
}

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Enable static files serving
app.UseStaticFiles();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Map endpoints
var apiV1 = app.MapGroup("/api/v1");

apiV1.MapGroup("/auth")
    .MapAuthEndpoints()
    .WithTags("Authentication");

apiV1.MapGroup("/clients")
    .MapClientEndpoints()
    .WithTags("Clients");

apiV1.MapGroup("/config")
    .MapConfigEndpoints()
    .WithTags("Configuration");

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithTags("Health");

// Serve home.html at root
app.MapGet("/", () => Results.File("home.html", "text/html"))
    .ExcludeFromDescription();

// Fallback to index.html for SPA routing (for all non-API, non-static routes)
app.MapFallback(context =>
{
    // Don't intercept API calls or static files
    if (context.Request.Path.StartsWithSegments("/api") || 
        context.Request.Path.StartsWithSegments("/assets") ||
        context.Request.Path.Value?.Contains('.') == true)
    {
        return Task.CompletedTask;
    }
    
    context.Response.ContentType = "text/html";
    return context.Response.SendFileAsync("wwwroot/index.html");
});

app.Run();

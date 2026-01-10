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
app.UseDefaultFiles();
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

// Fallback to index.html for SPA routing
app.MapFallbackToFile("index.html");

app.Run();

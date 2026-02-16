using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OutlineManager.API;
using OutlineManager.API.Commands;
using OutlineManager.API.Endpoints;
using OutlineManager.API.Interfaces;
using OutlineManager.API.Models;
using OutlineManager.API.Repositories;
using OutlineManager.API.Services;

// Check for create user command
if (await CreateUserCommand.AskForUserCreation(args)) return;

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
    OutlineConfigPath = builder.Configuration["AppSettings:OutlineConfigPath"] ?? "/etc/outline/config.yaml",
    PrometheusUrl = builder.Configuration["AppSettings:PrometheusUrl"] ?? "http://outline-server:9092",
    ClientLimitCheckMinutes = int.TryParse(builder.Configuration["AppSettings:ClientLimitCheckMinutes"], out var checkMinutes)
        ? checkMinutes
        : 15
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
builder.Services.AddSingleton<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddSingleton<IClientRepository, ClientRepository>();
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddSingleton<IOutlineSyncService, OutlineSyncService>();
builder.Services.AddHostedService<ClientLimitMonitorService>();

// Register HttpClient for MetricsService
builder.Services.AddHttpClient<IMetricsService, MetricsService>(client =>
{
    client.BaseAddress = new Uri(appSettings.PrometheusUrl);
});

// Add CORS
var corsPolicyName = builder.Environment.IsProduction() ? "ProductionOpenCors" : "LocalhostAnyPort";

builder.Services.AddCors(options =>
{
    options.AddPolicy("ProductionOpenCors", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });

    options.AddPolicy("LocalhostAnyPort", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
              {
                  if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                  {
                      return false;
                  }

                  return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                      || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                      || uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase);
              })
              .AllowCredentials()
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

// Enable static files serving with no caching
var staticFileOptions = new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
        ctx.Context.Response.Headers.Append("Pragma", "no-cache");
        ctx.Context.Response.Headers.Append("Expires", "0");
    }
};
app.UseStaticFiles(staticFileOptions);

app.UseCors(corsPolicyName);
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

apiV1.MapGroup("/reports")
    .MapReportEndpoints()
    .WithTags("Reports");

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

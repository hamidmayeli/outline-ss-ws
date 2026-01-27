using OutlineManager.API.Models;
using OutlineManager.API.Repositories;
using OutlineManager.API.Services;

namespace OutlineManager.API.Commands;

public static class CreateUserCommand
{
    public static async Task<bool> AskForUserCreation(string[] args)
    {
        // Check for create user command
        if (args.Length > 0 && args.Contains("-u") && args.Contains("-p"))
        {
            var usernameIndex = Array.IndexOf(args, "-u");
            var passwordIndex = Array.IndexOf(args, "-p");

            if (usernameIndex >= 0 && usernameIndex + 1 < args.Length &&
                passwordIndex >= 0 && passwordIndex + 1 < args.Length)
            {
                var username = args[usernameIndex + 1];
                var password = args[passwordIndex + 1];

                await ExecuteAsync(username, password, args);
            }
            else
            {
                Console.WriteLine("Error: Invalid arguments. Usage: api.exe -u <username> -p <password>");
            }

            return true;
        }

        return false;
    }

    private static async Task ExecuteAsync(string username, string password, string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddCommandLine(args)
            .Build();

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var logger = loggerFactory.CreateLogger<UserRepository>();
        var userRepository = new UserRepository(configuration, logger);

        try
        {
            var existingUser = await userRepository.GetByUsernameAsync(username);
            if (existingUser != null)
            {
                Console.WriteLine($"Error: User '{username}' already exists.");
                return;
            }

            var passwordHash = PasswordHasher.HashPassword(password);
            var newUser = new User
            {
                Id = Guid.NewGuid().ToString(),
                Username = username,
                PasswordHash = passwordHash,
                CreatedAt = DateTime.UtcNow
            };

            await userRepository.CreateAsync(newUser);
            Console.WriteLine($"User '{username}' created successfully.");
            Console.WriteLine($"User ID: {newUser.Id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating user: {ex.Message}");
        }
    }
}

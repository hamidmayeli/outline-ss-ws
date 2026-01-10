namespace OutlineManager.API.Models;

/// <summary>
/// Admin user for managing the application
/// </summary>
public class User
{
    public required string Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

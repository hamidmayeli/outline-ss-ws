namespace OutlineManager.API.Models;

/// <summary>
/// Outline client/user with VPN access
/// </summary>
public class Client
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Secret { get; set; }
    public string Cipher { get; set; } = "chacha20-ietf-poly1305";
    public long? Limit { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSingleConnection { get; set; }
    public int AccessKeyId { get; set; }
}

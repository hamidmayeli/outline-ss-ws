namespace OutlineManager.API.DTOs;

public class ClientResponse
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; }
    public ClientUsageResponse? UsageLast30Days { get; set; }
}

namespace OutlineManager.API.DTOs;

public class CreateClientRequest
{
    public required string Name { get; set; }
    public long? Limit { get; set; }
    public bool IsSingleConnection { get; set; }
}

public class UpdateClientRequest
{
    public required string Name { get; set; }
    public long? Limit { get; set; }
    public bool IsSingleConnection { get; set; }
}

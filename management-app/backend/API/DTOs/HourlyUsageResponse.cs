namespace OutlineManager.API.DTOs;

public class HourlyUsageResponse
{
    public required string ClientId { get; set; }
    public required string ClientName { get; set; }
    public required List<HourlyDataPoint> DataPoints { get; set; }
}

public class HourlyDataPoint
{
    public required DateTime Timestamp { get; set; }
    public long BytesTransferred { get; set; }
    public int Connections { get; set; }
}

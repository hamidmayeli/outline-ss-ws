namespace OutlineManager.API.DTOs;

public class DailyUsageResponse
{
    public required string ClientId { get; set; }
    public required string ClientName { get; set; }
    public required List<DailyDataPoint> DataPoints { get; set; }
}

public class DailyDataPoint
{
    public required DateTime Date { get; set; }
    public long BytesTransferred { get; set; }
    public long BytesUploaded { get; set; }
    public long BytesDownloaded { get; set; }
    public int Connections { get; set; }
}

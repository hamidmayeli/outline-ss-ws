namespace OutlineManager.API.DTOs;

public class ClientUsageResponse
{
    public long TotalBytesTransferred { get; set; }
    public long BytesUploaded { get; set; }
    public long BytesDownloaded { get; set; }
    public double TunnelTimeSeconds { get; set; }
    public int TotalConnections { get; set; }
}

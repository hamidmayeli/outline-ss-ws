namespace OutlineManager.API.Models;

public class AppSettings
{
    public required string Domain { get; set; }
    public required string TcpPath { get; set; }
    public required string UdpPath { get; set; }
    public required string OutlineConfigPath { get; set; }
}

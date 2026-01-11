using System.Text.Json.Serialization;

namespace OutlineManager.API.DTOs;

public class ConfigResponse
{
    public required TransportConfig Transport { get; set; }
}

public class TransportConfig
{
    [JsonPropertyName("$type")]
    public string Type { get; set; } = "tcpudp";
    
    public required ShadowsocksConfig Tcp { get; set; }
    
    public required ShadowsocksConfig Udp { get; set; }
}

public class ShadowsocksConfig
{
    [JsonPropertyName("$type")]
    public string Type { get; set; } = "shadowsocks";
    
    public required EndpointConfig Endpoint { get; set; }
    
    public string Cipher { get; set; } = "chacha20-ietf-poly1305";
    
    public required string Secret { get; set; }
}

public class EndpointConfig
{
    [JsonPropertyName("$type")]
    public string Type { get; set; } = "websocket";
    
    public required string Url { get; set; }
}

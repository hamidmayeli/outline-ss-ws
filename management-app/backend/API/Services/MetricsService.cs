using System.Text.Json;
using OutlineManager.API.DTOs;
using OutlineManager.API.Interfaces;
using OutlineManager.API.Models;

namespace OutlineManager.API.Services;

public class MetricsService : IMetricsService
{
    private readonly HttpClient _httpClient;
    private readonly IClientRepository _clientRepository;
    private readonly ILogger<MetricsService> _logger;

    public MetricsService(
        HttpClient httpClient,
        IClientRepository clientRepository,
        ILogger<MetricsService> logger)
    {
        _httpClient = httpClient;
        _clientRepository = clientRepository;
        _logger = logger;
    }

    public async Task<ClientUsageResponse> GetClientUsageLast30DaysAsync(string clientId)
    {
        try
        {
            // Query Prometheus for the last 30 days of data
            var endTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var startTime = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeSeconds();

            // Get total bytes transferred
            var bytesQuery = $"increase(shadowsocks_data_bytes{{access_key=\"{clientId}\"}}[30d])";
            var bytesData = await QueryPrometheusAsync(bytesQuery);
            
            // Get tunnel time
            var tunnelQuery = $"shadowsocks_tunnel_time_seconds{{access_key=\"{clientId}\"}}";
            var tunnelData = await QueryPrometheusAsync(tunnelQuery);
            
            // Get connections
            var connectionsQuery = $"increase(shadowsocks_tcp_connections_closed{{access_key=\"{clientId}\"}}[30d])";
            var connectionsData = await QueryPrometheusAsync(connectionsQuery);

            return ParseClientUsage(bytesData, tunnelData, connectionsData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch metrics for client {ClientId}", clientId);
            return new ClientUsageResponse();
        }
    }

    public async Task<List<HourlyUsageResponse>> GetAllClientsHourlyUsageAsync(int hours = 24)
    {
        try
        {
            var clients = await _clientRepository.GetAllAsync();
            var responses = new List<HourlyUsageResponse>();

            foreach (var client in clients)
            {
                var dataPoints = new List<HourlyDataPoint>();
                var endTime = DateTime.UtcNow;

                for (int i = 0; i < hours; i++)
                {
                    var timestamp = endTime.AddHours(-i);
                    var query = $"increase(shadowsocks_data_bytes{{access_key=\"{client.Id}\"}}[1h])";
                    
                    // For simplicity, we'll get current values. 
                    // In production, you'd want to use range queries
                    var data = await QueryPrometheusAsync(query);
                    var bytes = ParseTotalBytes(data);

                    dataPoints.Add(new HourlyDataPoint
                    {
                        Timestamp = timestamp,
                        BytesTransferred = bytes,
                        Connections = 0 // Can be enhanced with connection metrics
                    });
                }

                responses.Add(new HourlyUsageResponse
                {
                    ClientId = client.Id,
                    ClientName = client.Name,
                    DataPoints = dataPoints.OrderBy(d => d.Timestamp).ToList()
                });
            }

            return responses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch hourly usage");
            return [];
        }
    }

    public async Task<List<DailyUsageResponse>> GetAllClientsDailyUsageAsync(int days = 30)
    {
        try
        {
            var clients = await _clientRepository.GetAllAsync();
            var responses = new List<DailyUsageResponse>();

            foreach (var client in clients)
            {
                var dataPoints = new List<DailyDataPoint>();
                var endDate = DateTime.UtcNow.Date;

                for (int i = 0; i < days; i++)
                {
                    var date = endDate.AddDays(-i);
                    
                    // Query for bytes in that day
                    var bytesQuery = $"increase(shadowsocks_data_bytes{{access_key=\"{client.Id}\"}}[1d])";
                    var bytesData = await QueryPrometheusAsync(bytesQuery);
                    
                    var (uploaded, downloaded) = ParseUploadDownload(bytesData);

                    dataPoints.Add(new DailyDataPoint
                    {
                        Date = date,
                        BytesTransferred = uploaded + downloaded,
                        BytesUploaded = uploaded,
                        BytesDownloaded = downloaded,
                        Connections = 0 // Can be enhanced
                    });
                }

                responses.Add(new DailyUsageResponse
                {
                    ClientId = client.Id,
                    ClientName = client.Name,
                    DataPoints = dataPoints.OrderBy(d => d.Date).ToList()
                });
            }

            return responses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch daily usage");
            return [];
        }
    }

    private async Task<string> QueryPrometheusAsync(string query)
    {
        var response = await _httpClient.GetAsync($"/api/v1/query?query={Uri.EscapeDataString(query)}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private ClientUsageResponse ParseClientUsage(string bytesData, string tunnelData, string connectionsData)
    {
        var usage = new ClientUsageResponse();

        try
        {
            var bytesDoc = JsonDocument.Parse(bytesData);
            var results = bytesDoc.RootElement.GetProperty("data").GetProperty("result");

            long totalUp = 0, totalDown = 0;

            foreach (var result in results.EnumerateArray())
            {
                var metric = result.GetProperty("metric");
                var direction = metric.GetProperty("dir").GetString() ?? "";
                var value = result.GetProperty("value")[1].GetString() ?? "0";
                var bytes = long.Parse(value.Split('.')[0]);

                if (direction.Contains(">")) // Upload direction (c>p, p>t)
                    totalUp += bytes;
                else if (direction.Contains("<")) // Download direction (c<p, p<t)
                    totalDown += bytes;
            }

            usage.BytesUploaded = totalUp;
            usage.BytesDownloaded = totalDown;
            usage.TotalBytesTransferred = totalUp + totalDown;

            // Parse tunnel time
            var tunnelDoc = JsonDocument.Parse(tunnelData);
            var tunnelResults = tunnelDoc.RootElement.GetProperty("data").GetProperty("result");
            if (tunnelResults.GetArrayLength() > 0)
            {
                var tunnelValue = tunnelResults[0].GetProperty("value")[1].GetString() ?? "0";
                usage.TunnelTimeSeconds = double.Parse(tunnelValue);
            }

            // Parse connections
            var connectionsDoc = JsonDocument.Parse(connectionsData);
            var connResults = connectionsDoc.RootElement.GetProperty("data").GetProperty("result");
            int totalConns = 0;
            foreach (var result in connResults.EnumerateArray())
            {
                var value = result.GetProperty("value")[1].GetString() ?? "0";
                totalConns += int.Parse(value.Split('.')[0]);
            }
            usage.TotalConnections = totalConns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Prometheus response");
        }

        return usage;
    }

    private long ParseTotalBytes(string data)
    {
        try
        {
            var doc = JsonDocument.Parse(data);
            var results = doc.RootElement.GetProperty("data").GetProperty("result");
            long total = 0;

            foreach (var result in results.EnumerateArray())
            {
                var value = result.GetProperty("value")[1].GetString() ?? "0";
                total += long.Parse(value.Split('.')[0]);
            }

            return total;
        }
        catch
        {
            return 0;
        }
    }

    private (long uploaded, long downloaded) ParseUploadDownload(string data)
    {
        try
        {
            var doc = JsonDocument.Parse(data);
            var results = doc.RootElement.GetProperty("data").GetProperty("result");
            long totalUp = 0, totalDown = 0;

            foreach (var result in results.EnumerateArray())
            {
                var metric = result.GetProperty("metric");
                var direction = metric.GetProperty("dir").GetString() ?? "";
                var value = result.GetProperty("value")[1].GetString() ?? "0";
                var bytes = long.Parse(value.Split('.')[0]);

                if (direction.Contains(">"))
                    totalUp += bytes;
                else if (direction.Contains("<"))
                    totalDown += bytes;
            }

            return (totalUp, totalDown);
        }
        catch
        {
            return (0, 0);
        }
    }
}

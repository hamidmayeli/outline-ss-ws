using System.Text.Json;
using System.Text.RegularExpressions;
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
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Fetching usage for client: {ClientId}", clientId);
            }
            
            var client = await _clientRepository.GetByIdAsync(clientId);
            if (client == null)
            {
                _logger.LogWarning("Client {ClientId} not found", clientId);
                return new ClientUsageResponse();
            }
            
            var metricsText = await FetchRawMetricsAsync();
            return ParseClientUsageFromRawMetrics(metricsText, client.AccessKeyId.ToString());
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
            var metricsText = await FetchRawMetricsAsync();
            var responses = new List<HourlyUsageResponse>();

            foreach (var client in clients)
            {
                var usage = ParseClientUsageFromRawMetrics(metricsText, client.AccessKeyId.ToString());
                
                // Note: Raw metrics give us current counters, not historical hourly data
                // For proper hourly data, you'd need Prometheus with historical storage
                // For now, we return current usage as a single data point
                responses.Add(new HourlyUsageResponse
                {
                    ClientId = client.Id,
                    ClientName = client.Name,
                    DataPoints = new List<HourlyDataPoint>
                    {
                        new HourlyDataPoint
                        {
                            Timestamp = DateTime.UtcNow,
                            BytesTransferred = usage.TotalBytesTransferred,
                            Connections = usage.TotalConnections
                        }
                    }
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
            var metricsText = await FetchRawMetricsAsync();
            var responses = new List<DailyUsageResponse>();

            foreach (var client in clients)
            {
                var usage = ParseClientUsageFromRawMetrics(metricsText, client.AccessKeyId.ToString());
                
                // Note: Raw metrics give us current counters, not historical daily data
                // For proper daily data, you'd need Prometheus with historical storage
                // For now, we return current usage as a single data point
                responses.Add(new DailyUsageResponse
                {
                    ClientId = client.Id,
                    ClientName = client.Name,
                    DataPoints = new List<DailyDataPoint>
                    {
                        new DailyDataPoint
                        {
                            Date = DateTime.UtcNow.Date,
                            BytesTransferred = usage.TotalBytesTransferred,
                            BytesUploaded = usage.BytesUploaded,
                            BytesDownloaded = usage.BytesDownloaded,
                            Connections = usage.TotalConnections
                        }
                    }
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

    private async Task<string> FetchRawMetricsAsync()
    {
        try
        {
            var url = "/metrics";
            
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Fetching raw metrics from: {BaseAddress}{Path}", _httpClient.BaseAddress, url);
            }
            
            var response = await _httpClient.GetAsync(url);
            
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Metrics response status: {StatusCode}", response.StatusCode);
            }
            
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Metrics response length: {Length} bytes", content.Length);
            }
            
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch raw metrics");
            throw;
        }
    }

    private ClientUsageResponse ParseClientUsageFromRawMetrics(string metricsText, string accessKeyId)
    {
        var usage = new ClientUsageResponse();

        try
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Parsing metrics for access_key={AccessKeyId}", accessKeyId);
            }
            
            // Parse shadowsocks_data_bytes for the client
            // Format: shadowsocks_data_bytes{access_key="client-id",dir="c<-p",proto="tcp"} 12345
            var bytesPattern = $@"shadowsocks_data_bytes{{access_key=""{Regex.Escape(accessKeyId)}"",dir=""([^""]+)""[^}}]*}} ([0-9.]+(?:e[+-]?[0-9]+)?)";
            var bytesMatches = Regex.Matches(metricsText, bytesPattern);

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Found {Count} byte metrics for access_key={AccessKeyId}", bytesMatches.Count, accessKeyId);
            }

            long totalUp = 0, totalDown = 0;

            foreach (Match match in bytesMatches)
            {
                var direction = match.Groups[1].Value;
                var valueString = match.Groups[2].Value;
                
                // Handle scientific notation (e.g., 7.782434e+06)
                long value;
                if (valueString.Contains("e") || valueString.Contains("E"))
                {
                    value = (long)double.Parse(valueString, System.Globalization.NumberStyles.Float);
                }
                else
                {
                    value = long.Parse(valueString.Split('.')[0]);
                }

                if (direction.Contains(">")) // Upload: c>p or p>t
                    totalUp += value;
                else if (direction.Contains("<")) // Download: c<p or t<p or p<t
                    totalDown += value;
            }

            usage.BytesUploaded = totalUp;
            usage.BytesDownloaded = totalDown;
            usage.TotalBytesTransferred = totalUp + totalDown;

            // Parse shadowsocks_tunnel_time_seconds
            var tunnelPattern = $@"shadowsocks_tunnel_time_seconds{{access_key=""{Regex.Escape(accessKeyId)}""[^}}]*}} ([0-9.]+(?:e[+-]?[0-9]+)?)";
            var tunnelMatch = Regex.Match(metricsText, tunnelPattern);
            if (tunnelMatch.Success)
            {
                usage.TunnelTimeSeconds = double.Parse(tunnelMatch.Groups[1].Value);
            }

            // Parse shadowsocks_tcp_connections_closed
            var connectionsPattern = $@"shadowsocks_tcp_connections_closed{{access_key=""{Regex.Escape(accessKeyId)}""[^}}]*status=""([^""]+)""[^}}]*}} ([0-9.]+(?:e[+-]?[0-9]+)?)";
            var connectionsMatches = Regex.Matches(metricsText, connectionsPattern);
            
            int totalConns = 0;
            foreach (Match match in connectionsMatches)
            {
                totalConns += int.Parse(match.Groups[2].Value.Split('.')[0]);
            }
            usage.TotalConnections = totalConns;

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Parsed usage for access_key={AccessKeyId}: {Upload}↑ {Download}↓ {Connections} conns", 
                    accessKeyId, usage.BytesUploaded, usage.BytesDownloaded, usage.TotalConnections);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse raw metrics for access_key {AccessKeyId}", accessKeyId);
        }

        return usage;
    }
}
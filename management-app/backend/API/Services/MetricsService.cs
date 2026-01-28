using System.Globalization;
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

            var accessKey = GetClientMetricKey(client);
            var bytesQuery = $"sum by (dir) (increase(shadowsocks_data_bytes{{access_key=\"{accessKey}\"}}[30d]))";
            var connectionsQuery = $"sum (increase(shadowsocks_tcp_connections_closed{{access_key=\"{accessKey}\"}}[30d]))";
            var tunnelQuery = $"sum (increase(shadowsocks_tunnel_time_seconds{{access_key=\"{accessKey}\"}}[30d]))";

            var bytesSamples = await QueryInstantAsync(bytesQuery);
            var connectionsSamples = await QueryInstantAsync(connectionsQuery);
            var tunnelSamples = await QueryInstantAsync(tunnelQuery);

            var usage = new ClientUsageResponse();

            foreach (var sample in bytesSamples)
            {
                sample.Metric.TryGetValue("dir", out var direction);
                if (direction?.Contains('>') == true)
                {
                    usage.BytesUploaded += (long)sample.Value;
                }
                else if (direction?.Contains('<') == true)
                {
                    usage.BytesDownloaded += (long)sample.Value;
                }
            }

            usage.TotalBytesTransferred = usage.BytesUploaded + usage.BytesDownloaded;
            usage.TotalConnections = (int)connectionsSamples.Sum(s => s.Value);
            usage.TunnelTimeSeconds = tunnelSamples.Sum(s => s.Value);

            return usage;
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
            if (!clients.Any())
            {
                return [];
            }

            var hoursCount = Math.Max(1, hours);
            var end = AlignToHour(DateTime.UtcNow);
            var start = end.AddHours(-(hoursCount - 1));
            var step = TimeSpan.FromHours(1);

            var bytesQuery = "sum by (access_key, dir) (increase(shadowsocks_data_bytes[1h]))";
            var connectionsQuery = "sum by (access_key) (increase(shadowsocks_tcp_connections_closed[1h]))";

            var bytesSeries = await QueryRangeAsync(bytesQuery, start, end, step);
            var connectionsSeries = await QueryRangeAsync(connectionsQuery, start, end, step);

            var clientLookup = BuildClientLookup(clients);
            var data = InitializeHourlyData(clientLookup.Values, start, hoursCount, step);

            foreach (var series in bytesSeries)
            {
                if (!series.Metric.TryGetValue("access_key", out var accessKey) ||
                    !data.TryGetValue(accessKey, out var points))
                {
                    continue;
                }

                series.Metric.TryGetValue("dir", out var direction);

                foreach (var sample in series.Values)
                {
                    if (!points.TryGetValue(sample.Timestamp, out var point))
                    {
                        continue;
                    }

                    if (direction?.Contains('>') == true)
                    {
                        point.BytesTransferred += (long)sample.Value;
                    }
                    else if (direction?.Contains('<') == true)
                    {
                        point.BytesTransferred += (long)sample.Value;
                    }
                }
            }

            foreach (var series in connectionsSeries)
            {
                if (!series.Metric.TryGetValue("access_key", out var accessKey) ||
                    !data.TryGetValue(accessKey, out var points))
                {
                    continue;
                }

                foreach (var sample in series.Values)
                {
                    if (points.TryGetValue(sample.Timestamp, out var point))
                    {
                        point.Connections += (int)sample.Value;
                    }
                }
            }

            return BuildHourlyResponses(clientLookup, data);
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
            if (!clients.Any())
            {
                return [];
            }

            var daysCount = Math.Max(1, days);
            var end = DateTime.UtcNow.Date;
            var start = end.AddDays(-(daysCount - 1));
            var step = TimeSpan.FromDays(1);

            var bytesQuery = "sum by (access_key, dir) (increase(shadowsocks_data_bytes[1d]))";
            var connectionsQuery = "sum by (access_key) (increase(shadowsocks_tcp_connections_closed[1d]))";

            var bytesSeries = await QueryRangeAsync(bytesQuery, start, end, step);
            var connectionsSeries = await QueryRangeAsync(connectionsQuery, start, end, step);

            var clientLookup = BuildClientLookup(clients);
            var data = InitializeDailyData(clientLookup.Values, start, daysCount, step);

            foreach (var series in bytesSeries)
            {
                if (!series.Metric.TryGetValue("access_key", out var accessKey) ||
                    !data.TryGetValue(accessKey, out var points))
                {
                    continue;
                }

                series.Metric.TryGetValue("dir", out var direction);

                foreach (var sample in series.Values)
                {
                    if (!points.TryGetValue(sample.Timestamp.Date, out var point))
                    {
                        continue;
                    }

                    if (direction?.Contains('>') == true)
                    {
                        point.BytesUploaded += (long)sample.Value;
                    }
                    else if (direction?.Contains('<') == true)
                    {
                        point.BytesDownloaded += (long)sample.Value;
                    }
                }
            }

            foreach (var series in connectionsSeries)
            {
                if (!series.Metric.TryGetValue("access_key", out var accessKey) ||
                    !data.TryGetValue(accessKey, out var points))
                {
                    continue;
                }

                foreach (var sample in series.Values)
                {
                    var date = sample.Timestamp.Date;
                    if (points.TryGetValue(date, out var point))
                    {
                        point.Connections += (int)sample.Value;
                    }
                }
            }

            return BuildDailyResponses(clientLookup, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch daily usage");
            return [];
        }
    }

    private async Task<List<PrometheusInstantSample>> QueryInstantAsync(string query)
    {
        var url = $"/api/v1/query?query={Uri.EscapeDataString(query)}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);

        var result = new List<PrometheusInstantSample>();
        if (!TryGetResultArray(doc, out var resultArray))
        {
            return result;
        }

        foreach (var item in resultArray.EnumerateArray())
        {
            if (!item.TryGetProperty("metric", out var metricElement) ||
                !item.TryGetProperty("value", out var valueElement) ||
                valueElement.ValueKind != JsonValueKind.Array ||
                valueElement.GetArrayLength() < 2)
            {
                continue;
            }

            var metric = ReadMetric(metricElement);
            var value = ParseValue(valueElement[1]);
            result.Add(new PrometheusInstantSample(metric, value));
        }

        return result;
    }

    private async Task<List<PrometheusRangeSeries>> QueryRangeAsync(string query, DateTime startUtc, DateTime endUtc, TimeSpan step)
    {
        var start = new DateTimeOffset(startUtc).ToUnixTimeSeconds();
        var end = new DateTimeOffset(endUtc).ToUnixTimeSeconds();
        var stepSeconds = (int)step.TotalSeconds;

        var url = $"/api/v1/query_range?query={Uri.EscapeDataString(query)}&start={start}&end={end}&step={stepSeconds}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);

        var result = new List<PrometheusRangeSeries>();
        if (!TryGetResultArray(doc, out var resultArray))
        {
            return result;
        }

        foreach (var item in resultArray.EnumerateArray())
        {
            if (!item.TryGetProperty("metric", out var metricElement) ||
                !item.TryGetProperty("values", out var valuesElement) ||
                valuesElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var metric = ReadMetric(metricElement);
            var values = new List<PrometheusRangeValue>();

            foreach (var value in valuesElement.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() < 2)
                {
                    continue;
                }

                var ts = value[0].GetDouble();
                var timestamp = DateTimeOffset.FromUnixTimeSeconds((long)ts).UtcDateTime;
                var val = ParseValue(value[1]);
                values.Add(new PrometheusRangeValue(timestamp, val));
            }

            result.Add(new PrometheusRangeSeries(metric, values));
        }

        return result;
    }

    private static bool TryGetResultArray(JsonDocument doc, out JsonElement resultArray)
    {
        resultArray = default;
        if (!doc.RootElement.TryGetProperty("data", out var dataElement) ||
            !dataElement.TryGetProperty("result", out resultArray) ||
            resultArray.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return true;
    }

    private static Dictionary<string, string> ReadMetric(JsonElement metricElement)
    {
        var metric = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in metricElement.EnumerateObject())
        {
            metric[property.Name] = property.Value.GetString() ?? string.Empty;
        }

        return metric;
    }

    private static double ParseValue(JsonElement valueElement)
    {
        if (valueElement.ValueKind == JsonValueKind.Number && valueElement.TryGetDouble(out var numeric))
        {
            return numeric;
        }

        var valueString = valueElement.GetString();
        if (double.TryParse(valueString, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return 0;
    }

    private static Dictionary<string, Dictionary<DateTime, HourlyDataPoint>> InitializeHourlyData(
        IEnumerable<Client> clients,
        DateTime start,
        int count,
        TimeSpan step)
    {
        var data = new Dictionary<string, Dictionary<DateTime, HourlyDataPoint>>(StringComparer.OrdinalIgnoreCase);
        var timestamps = BuildTimestamps(start, count, step);

        foreach (var client in clients)
        {
            var points = new Dictionary<DateTime, HourlyDataPoint>();
            foreach (var timestamp in timestamps)
            {
                points[timestamp] = new HourlyDataPoint
                {
                    Timestamp = timestamp,
                    BytesTransferred = 0,
                    Connections = 0
                };
            }

            data[GetClientMetricKey(client)] = points;
        }

        return data;
    }

    private static Dictionary<string, Dictionary<DateTime, DailyDataPoint>> InitializeDailyData(
        IEnumerable<Client> clients,
        DateTime start,
        int count,
        TimeSpan step)
    {
        var data = new Dictionary<string, Dictionary<DateTime, DailyDataPoint>>(StringComparer.OrdinalIgnoreCase);
        var timestamps = BuildTimestamps(start, count, step).Select(t => t.Date).Distinct();

        foreach (var client in clients)
        {
            var points = new Dictionary<DateTime, DailyDataPoint>();
            foreach (var timestamp in timestamps)
            {
                points[timestamp] = new DailyDataPoint
                {
                    Date = timestamp,
                    BytesTransferred = 0,
                    BytesUploaded = 0,
                    BytesDownloaded = 0,
                    Connections = 0
                };
            }

            data[GetClientMetricKey(client)] = points;
        }

        return data;
    }

    private static List<DateTime> BuildTimestamps(DateTime start, int count, TimeSpan step)
    {
        var list = new List<DateTime>();
        var cursor = start;
        for (var i = 0; i < count; i++)
        {
            list.Add(DateTime.SpecifyKind(cursor, DateTimeKind.Utc));
            cursor = cursor.Add(step);
        }

        return list;
    }

    private static List<HourlyUsageResponse> BuildHourlyResponses(
        Dictionary<string, Client> clients,
        Dictionary<string, Dictionary<DateTime, HourlyDataPoint>> data)
    {
        var responses = new List<HourlyUsageResponse>();
        foreach (var (accessKey, points) in data)
        {
            if (!clients.TryGetValue(accessKey, out var client))
            {
                continue;
            }

            responses.Add(new HourlyUsageResponse
            {
                ClientId = client.Id,
                ClientName = client.Name,
                DataPoints = points.Values.OrderBy(p => p.Timestamp).ToList()
            });
        }

        return responses;
    }

    private static List<DailyUsageResponse> BuildDailyResponses(
        Dictionary<string, Client> clients,
        Dictionary<string, Dictionary<DateTime, DailyDataPoint>> data)
    {
        var responses = new List<DailyUsageResponse>();
        foreach (var (accessKey, points) in data)
        {
            if (!clients.TryGetValue(accessKey, out var client))
            {
                continue;
            }

            foreach (var point in points.Values)
            {
                point.BytesTransferred = point.BytesUploaded + point.BytesDownloaded;
            }

            responses.Add(new DailyUsageResponse
            {
                ClientId = client.Id,
                ClientName = client.Name,
                DataPoints = points.Values.OrderBy(p => p.Date).ToList()
            });
        }

        return responses;
    }

    private sealed record PrometheusInstantSample(Dictionary<string, string> Metric, double Value);

    private sealed record PrometheusRangeSeries(Dictionary<string, string> Metric, List<PrometheusRangeValue> Values);

    private sealed record PrometheusRangeValue(DateTime Timestamp, double Value);

    private static Dictionary<string, Client> BuildClientLookup(IEnumerable<Client> clients)
    {
        var lookup = new Dictionary<string, Client>(StringComparer.OrdinalIgnoreCase);
        foreach (var client in clients)
        {
            var key = GetClientMetricKey(client);
            if (!lookup.ContainsKey(key))
            {
                lookup[key] = client;
            }
        }

        return lookup;
    }

    private static string GetClientMetricKey(Client client)
    {
        return client.AccessKeyId > 0
            ? client.AccessKeyId.ToString(CultureInfo.InvariantCulture)
            : client.Id;
    }

    private static DateTime AlignToHour(DateTime utc)
    {
        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, DateTimeKind.Utc);
    }
}
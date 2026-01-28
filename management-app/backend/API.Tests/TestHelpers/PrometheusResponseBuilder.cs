using System.Globalization;
using System.Text.Json;

namespace OutlineManager.API.Tests.TestHelpers;

public static class PrometheusResponseBuilder
{
    public static string EmptyInstant() => BuildInstant();

    public static string EmptyRange() => BuildRange();

    public static string BuildInstant(params InstantSample[] samples)
    {
        var result = samples.Select(sample => new
        {
            metric = sample.Metric,
            value = new object[]
            {
                sample.Timestamp,
                sample.Value.ToString(CultureInfo.InvariantCulture)
            }
        });

        var payload = new
        {
            status = "success",
            data = new
            {
                resultType = "vector",
                result
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    public static string BuildRange(params RangeSeries[] series)
    {
        var result = series.Select(item => new
        {
            metric = item.Metric,
            values = item.Values.Select(value => new object[]
            {
                value.Timestamp,
                value.Value.ToString(CultureInfo.InvariantCulture)
            })
        });

        var payload = new
        {
            status = "success",
            data = new
            {
                resultType = "matrix",
                result
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    public readonly record struct InstantSample(Dictionary<string, string> Metric, long Timestamp, double Value);

    public readonly record struct RangeSeries(Dictionary<string, string> Metric, List<RangeValue> Values);

    public readonly record struct RangeValue(long Timestamp, double Value);
}

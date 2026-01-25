using Microsoft.AspNetCore.Http.HttpResults;
using OutlineManager.API.DTOs;
using OutlineManager.API.Interfaces;

namespace OutlineManager.API.Endpoints;

public static class ReportEndpoints
{
    extension(RouteGroupBuilder group)
    {
        public RouteGroupBuilder MapReportEndpoints()
        {
            group.MapGet("/hourly", GetHourlyUsageAsync)
                .WithName("GetHourlyUsage")
                .RequireAuthorization();

            group.MapGet("/daily", GetDailyUsageAsync)
                .WithName("GetDailyUsage")
                .RequireAuthorization();

            return group;
        }
    }

    private static async Task<Ok<List<HourlyUsageResponse>>> GetHourlyUsageAsync(
        IMetricsService metricsService,
        int hours = 24)
    {
        var usage = await metricsService.GetAllClientsHourlyUsageAsync(hours);
        return TypedResults.Ok(usage);
    }

    private static async Task<Ok<List<DailyUsageResponse>>> GetDailyUsageAsync(
        IMetricsService metricsService,
        int days = 30)
    {
        var usage = await metricsService.GetAllClientsDailyUsageAsync(days);
        return TypedResults.Ok(usage);
    }
}

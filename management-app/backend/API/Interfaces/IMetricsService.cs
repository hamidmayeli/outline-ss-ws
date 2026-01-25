using OutlineManager.API.DTOs;

namespace OutlineManager.API.Interfaces;

public interface IMetricsService
{
    Task<ClientUsageResponse> GetClientUsageLast30DaysAsync(string clientId);
    Task<List<HourlyUsageResponse>> GetAllClientsHourlyUsageAsync(int hours = 24);
    Task<List<DailyUsageResponse>> GetAllClientsDailyUsageAsync(int days = 30);
}

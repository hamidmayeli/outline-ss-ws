using OutlineManager.API.Interfaces;
using OutlineManager.API.Models;

namespace OutlineManager.API.Services;

public class ClientLimitMonitorService : BackgroundService
{
    private readonly IClientRepository _clientRepository;
    private readonly IMetricsService _metricsService;
    private readonly IOutlineSyncService _outlineSyncService;
    private readonly ILogger<ClientLimitMonitorService> _logger;
    private readonly TimeSpan _interval;

    public ClientLimitMonitorService(
        IClientRepository clientRepository,
        IMetricsService metricsService,
        IOutlineSyncService outlineSyncService,
        AppSettings appSettings,
        ILogger<ClientLimitMonitorService> logger)
    {
        _clientRepository = clientRepository;
        _metricsService = metricsService;
        _outlineSyncService = outlineSyncService;
        _logger = logger;

        var minutes = Math.Max(1, appSettings.ClientLimitCheckMinutes);
        _interval = TimeSpan.FromMinutes(minutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Client limit monitor started. Interval: {IntervalMinutes} minutes", _interval.TotalMinutes);
        }

        await CheckLimitsAsync(stoppingToken);

        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckLimitsAsync(stoppingToken);
        }
    }

    private async Task CheckLimitsAsync(CancellationToken stoppingToken)
    {
        var clients = (await _clientRepository.GetAllAsync()).ToList();
        if (clients.Count == 0)
        {
            return;
        }

        var anyChanges = false;

        foreach (var client in clients)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            var usage = await _metricsService.GetClientUsageLast30DaysAsync(client.Id);
            bool isNotOverLimit() => usage.TotalBytesTransferred <= client.Limit.Value;
            var shouldBeActive = client.Limit is null || isNotOverLimit();

            if (client.IsActive == shouldBeActive)
            {
                continue;
            }

            client.IsActive = shouldBeActive;
            await _clientRepository.UpdateAsync(client.Id, client);
            anyChanges = true;

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Client {ClientId} status changed to {IsActive} (Usage: {UsageBytes}, Limit: {LimitBytes})",
                    client.Id,
                    client.IsActive,
                    usage.TotalBytesTransferred,
                    client.Limit);
            }
        }

        if (!anyChanges)
        {
            return;
        }

        var updatedClients = await _clientRepository.GetAllAsync();
        await _outlineSyncService.SyncClientsToOutlineAsync(updatedClients);
    }
}

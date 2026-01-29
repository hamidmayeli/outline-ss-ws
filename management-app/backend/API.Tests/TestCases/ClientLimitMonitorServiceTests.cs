using System.Reflection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OutlineManager.API.DTOs;
using OutlineManager.API.Interfaces;
using OutlineManager.API.Models;
using OutlineManager.API.Services;

namespace OutlineManager.API.Tests.TestCases;

public class ClientLimitMonitorServiceTests
{
    private static ClientLimitMonitorService CreateService(
        IClientRepository clientRepository,
        IMetricsService metricsService,
        IOutlineSyncService outlineSyncService)
    {
        var appSettings = new AppSettings
        {
            Domain = "test.local",
            TcpPath = "/tcp",
            UdpPath = "/udp",
            OutlineConfigPath = "/tmp/config.yaml",
            PrometheusUrl = "http://prometheus",
            ClientLimitCheckMinutes = 15
        };

        var logger = Substitute.For<ILogger<ClientLimitMonitorService>>();

        return new ClientLimitMonitorService(
            clientRepository,
            metricsService,
            outlineSyncService,
            appSettings,
            logger);
    }

    private static Task InvokeCheckLimitsAsync(ClientLimitMonitorService service, CancellationToken token)
    {
        var method = typeof(ClientLimitMonitorService)
            .GetMethod("CheckLimitsAsync", BindingFlags.Instance | BindingFlags.NonPublic);

        if (method is null)
        {
            throw new InvalidOperationException("CheckLimitsAsync method not found.");
        }

        return (Task)method.Invoke(service, new object[] { token })!;
    }

    [Fact]
    public async Task When_NoClients_DoesNotCallMetricsOrSync()
    {
        var clientRepository = Substitute.For<IClientRepository>();
        var metricsService = Substitute.For<IMetricsService>();
        var outlineSyncService = Substitute.For<IOutlineSyncService>();

        clientRepository.GetAllAsync().Returns([]);

        var service = CreateService(clientRepository, metricsService, outlineSyncService);

        await InvokeCheckLimitsAsync(service, CancellationToken.None);

        await metricsService.DidNotReceive().GetClientUsageLast30DaysAsync(Arg.Any<string>());
        await clientRepository.DidNotReceive().UpdateAsync(Arg.Any<string>(), Arg.Any<Client>());
        await outlineSyncService.DidNotReceive().SyncClientsToOutlineAsync(Arg.Any<IEnumerable<Client>>());
    }

    [Fact]
    public async Task When_LimitIsNull_DoesNotChangeClientStatusOrSync()
    {
        var clientRepository = Substitute.For<IClientRepository>();
        var metricsService = Substitute.For<IMetricsService>();
        var outlineSyncService = Substitute.For<IOutlineSyncService>();

        var client = new Client
        {
            Id = "client-1",
            Name = "Client One",
            Secret = "secret",
            IsActive = true,
            Limit = null,
            AccessKeyId = 1
        };

        clientRepository.GetAllAsync().Returns([client]);
        metricsService.GetClientUsageLast30DaysAsync(client.Id)
            .Returns(new ClientUsageResponse { TotalBytesTransferred = 999 });

        var service = CreateService(clientRepository, metricsService, outlineSyncService);

        await InvokeCheckLimitsAsync(service, CancellationToken.None);

        await clientRepository.DidNotReceive().UpdateAsync(Arg.Any<string>(), Arg.Any<Client>());
        await outlineSyncService.DidNotReceive().SyncClientsToOutlineAsync(Arg.Any<IEnumerable<Client>>());
    }

    [Fact]
    public async Task When_UsageExceedsLimit_DeactivatesAndSyncs()
    {
        var clientRepository = Substitute.For<IClientRepository>();
        var metricsService = Substitute.For<IMetricsService>();
        var outlineSyncService = Substitute.For<IOutlineSyncService>();

        var client = new Client
        {
            Id = "client-1",
            Name = "Client One",
            Secret = "secret",
            IsActive = true,
            Limit = 100,
            AccessKeyId = 1
        };

        clientRepository.GetAllAsync().Returns([client], [client]);
        metricsService.GetClientUsageLast30DaysAsync(client.Id)
            .Returns(new ClientUsageResponse { TotalBytesTransferred = 101 });

        var service = CreateService(clientRepository, metricsService, outlineSyncService);

        await InvokeCheckLimitsAsync(service, CancellationToken.None);

        await clientRepository.Received(1)
            .UpdateAsync(client.Id, Arg.Is<Client>(c => c.IsActive == false));

        await outlineSyncService.Received(1)
            .SyncClientsToOutlineAsync(Arg.Is<IEnumerable<Client>>(clients => clients.First().IsActive == false));
    }

    [Fact]
    public async Task When_UsageBelowLimit_ActivatesAndSyncs()
    {
        var clientRepository = Substitute.For<IClientRepository>();
        var metricsService = Substitute.For<IMetricsService>();
        var outlineSyncService = Substitute.For<IOutlineSyncService>();

        var client = new Client
        {
            Id = "client-1",
            Name = "Client One",
            Secret = "secret",
            IsActive = false,
            Limit = 100,
            AccessKeyId = 1
        };

        clientRepository.GetAllAsync().Returns([client], [client]);
        metricsService.GetClientUsageLast30DaysAsync(client.Id)
            .Returns(new ClientUsageResponse { TotalBytesTransferred = 99 });

        var service = CreateService(clientRepository, metricsService, outlineSyncService);

        await InvokeCheckLimitsAsync(service, CancellationToken.None);

        await clientRepository.Received(1)
            .UpdateAsync(client.Id, Arg.Is<Client>(c => c.IsActive == true));

        await outlineSyncService.Received(1)
            .SyncClientsToOutlineAsync(Arg.Is<IEnumerable<Client>>(clients => clients.First().IsActive == true));
    }
}

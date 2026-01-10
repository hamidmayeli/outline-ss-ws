using OutlineManager.API.Interfaces;
using OutlineManager.API.Models;
using System.Text;

namespace OutlineManager.API.Services;

public class OutlineSyncService(
    AppSettings appSettings,
    ILogger<OutlineSyncService> logger) : IOutlineSyncService
{
    private readonly AppSettings _appSettings = appSettings;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public async Task SyncClientsToOutlineAsync(IEnumerable<Client> clients)
    {
        await _syncLock.WaitAsync();
        try
        {
            var activeClients = clients.Where(c => c.IsActive).ToList();
            await WriteOutlineConfigAsync(activeClients);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Synced {Count} active clients to Outline config", activeClients.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync clients to Outline config");
            throw;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task ReloadOutlineServerAsync()
    {
        try
        {
            // Since outline-ss-server watches the config file, we just need to touch it
            // or in production, you might send a signal to the container
            if (File.Exists(_appSettings.OutlineConfigPath))
            {
                File.SetLastWriteTimeUtc(_appSettings.OutlineConfigPath, DateTime.UtcNow);
                
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Triggered Outline server reload");
                }
            }
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reload Outline server");
            throw;
        }
    }

    private async Task WriteOutlineConfigAsync(List<Client> activeClients)
    {
        var yaml = BuildYamlConfig(activeClients);
        
        var directory = Path.GetDirectoryName(_appSettings.OutlineConfigPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(_appSettings.OutlineConfigPath, yaml);
    }

    private string BuildYamlConfig(List<Client> activeClients)
    {
        var sb = new StringBuilder();
        sb.AppendLine("web:");
        sb.AppendLine("  servers:");
        sb.AppendLine("    - id: ws-server");
        sb.AppendLine("      listen:");
        sb.AppendLine("        - \"0.0.0.0:9090\"");
        sb.AppendLine();
        sb.AppendLine("services:");
        sb.AppendLine("  - listeners:");
        sb.AppendLine("      - type: websocket-stream");
        sb.AppendLine("        web_server: ws-server");
        sb.AppendLine($"        path: \"{_appSettings.TcpPath}\"");
        sb.AppendLine("      - type: websocket-packet");
        sb.AppendLine("        web_server: ws-server");
        sb.AppendLine($"        path: \"{_appSettings.UdpPath}\"");
        sb.AppendLine("    keys:");

        for (int i = 0; i < activeClients.Count; i++)
        {
            var client = activeClients[i];
            sb.AppendLine($"      - id: {i + 1}");
            sb.AppendLine($"        cipher: {client.Cipher}");
            sb.AppendLine($"        secret: {client.Secret}");
        }

        return sb.ToString();
    }
}

using OutlineManager.API.Models;

namespace OutlineManager.API.Interfaces;

public interface IOutlineSyncService
{
    Task SyncClientsToOutlineAsync(IEnumerable<Client> clients);
}

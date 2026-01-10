using OutlineManager.API.Models;

namespace OutlineManager.API.Interfaces;

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(string id);
    Task<Client?> GetByNameAsync(string name);
    Task<IEnumerable<Client>> GetAllAsync();
    Task<Client> CreateAsync(string name);
    Task<Client?> UpdateAsync(string id, Client client);
    Task<bool> DeleteAsync(string id);
    Task<bool> ExistsAsync(string name);
}

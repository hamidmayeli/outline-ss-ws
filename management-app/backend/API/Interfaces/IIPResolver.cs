namespace OutlineManager.API.Interfaces;

public interface IIPResolver
{
    Task<string?> ResolveAsync(string hostName);
}

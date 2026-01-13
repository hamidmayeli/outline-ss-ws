using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace OutlineManager.API.Repositories;

public abstract class RepositoryBase<T>(ILogger<RepositoryBase<T>> logger) where T : class
{
    protected abstract string DataFilePath { get; }
    protected abstract JsonTypeInfo<List<T>> JsonTypeInfo { get; }

    private readonly SemaphoreSlim _fileLock = new(1, 1);

    protected async Task<TResult> WithFileLockAsync<TResult>(Func<Task<TResult>> action)
    {
        await _fileLock.WaitAsync();
        try
        {
            return await action();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    protected async Task<IList<T>> LoadAsync()
    {
        if (!File.Exists(DataFilePath))
            return [];

        try
        {
            var json = await File.ReadAllTextAsync(DataFilePath);
            return JsonSerializer.Deserialize(json, JsonTypeInfo) ?? [];
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
                logger.LogError(ex, "Failed to load {type} from file", typeof(T));

            return [];
        }
    }

    protected async Task SaveAsync(IEnumerable<T> values)
    {
        var json = JsonSerializer.Serialize(values, JsonTypeInfo);
        await File.WriteAllTextAsync(DataFilePath, json);
    }
}

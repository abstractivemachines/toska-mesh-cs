using System.Collections.ObjectModel;

namespace ToskaMesh.Runtime;

/// <summary>
/// Simple key/value store abstraction for mesh services.
/// </summary>
public interface IKeyValueStore
{
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> ListKeysAsync(string prefix = "", int pageSize = 100, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> ListAsync<T>(string prefix = "", int pageSize = 100, CancellationToken cancellationToken = default);
}

using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ToskaMesh.Runtime;

/// <summary>
/// HTTP/JSON-backed key/value store using ToskaStore.
/// </summary>
public sealed class ToskaStoreKeyValueStore : IKeyValueStore, IDisposable
{
    private readonly HttpClient _client;
    private readonly ToskaStoreKeyValueOptions _options;
    private readonly string _keyPrefix;
    private readonly string _indexKey;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public ToskaStoreKeyValueStore(ToskaStoreKeyValueOptions options, MeshServiceOptions serviceOptions)
    {
        _options = options;
        _keyPrefix = BuildPrefix(options, serviceOptions);
        _indexKey = string.IsNullOrWhiteSpace(options.KeyIndexKey) ? "__keys" : options.KeyIndexKey!;

        _client = new HttpClient
        {
            BaseAddress = new Uri(options.BaseUrl)
        };

        if (!string.IsNullOrWhiteSpace(options.AuthToken))
        {
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.AuthToken}");
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        var serialized = JsonSerializer.Serialize(value, _jsonOptions);
        await PutValueAsync(key, serialized, ttl, cancellationToken);
        await UpdateIndexAsync(key, add: true, cancellationToken);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var response = await _client.GetAsync(BuildKeyPath(key), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        await EnsureSuccessAsync(response, "GET", key, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<ToskaStoreGetResponse>(_jsonOptions, cancellationToken);
        if (payload?.Value is null)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(payload.Value, _jsonOptions);
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var response = await _client.DeleteAsync(BuildKeyPath(key), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            await UpdateIndexAsync(key, add: false, cancellationToken);
            return false;
        }

        await EnsureSuccessAsync(response, "DELETE", key, cancellationToken);
        await UpdateIndexAsync(key, add: false, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(string prefix = "", int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var limit = Math.Max(pageSize, 1);
        var listed = await TryListKeysFromApi(prefix, limit, cancellationToken);
        if (listed is not null)
        {
            return listed;
        }

        if (!_options.EnableKeyIndex)
        {
            throw new NotSupportedException("ToskaStore key listing requires EnableKeyIndex=true when /kv/keys is unavailable.");
        }

        var keys = await GetIndexAsync(cancellationToken);
        return string.IsNullOrEmpty(prefix)
            ? keys
            : keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToList();
    }

    public async Task<IReadOnlyList<T>> ListAsync<T>(string prefix = "", int pageSize = 100, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableKeyIndex)
        {
            throw new NotSupportedException("ToskaStore list operations require EnableKeyIndex=true.");
        }

        var keys = await ListKeysAsync(prefix, pageSize, cancellationToken);
        if (keys.Count == 0)
        {
            return Array.Empty<T>();
        }

        var results = new List<T>();
        var batchSize = Math.Max(pageSize, 1);
        foreach (var batch in keys.Chunk(batchSize))
        {
            var values = await MgetAsync(batch, cancellationToken);
            foreach (var key in batch)
            {
                if (!values.TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
                {
                    continue;
                }

                var item = JsonSerializer.Deserialize<T>(value, _jsonOptions);
                if (item is not null)
                {
                    results.Add(item);
                }
            }
        }

        return results;
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    private async Task PutValueAsync(string key, string value, TimeSpan? ttl, CancellationToken cancellationToken)
    {
        long? ttlMs = ttl.HasValue ? Math.Max((long)ttl.Value.TotalMilliseconds, 0L) : null;
        var request = new ToskaStorePutRequest(value, ttlMs);

        var response = await _client.PutAsJsonAsync(BuildKeyPath(key), request, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, "PUT", key, cancellationToken);
    }

    private async Task<Dictionary<string, string?>> MgetAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
    {
        var request = new ToskaStoreMgetRequest(keys);
        var response = await _client.PostAsJsonAsync("kv/mget", request, _jsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, "MGET", "_batch", cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<ToskaStoreMgetResponse>(_jsonOptions, cancellationToken);
        return payload?.Values ?? new Dictionary<string, string?>(StringComparer.Ordinal);
    }

    private async Task<IReadOnlyList<string>> GetIndexAsync(CancellationToken cancellationToken)
    {
        var stored = await GetAsync<List<string>>(_indexKey, cancellationToken);
        if (stored is null || stored.Count == 0)
        {
            return Array.Empty<string>();
        }

        return stored.Distinct(StringComparer.Ordinal).ToList();
    }

    private async Task<IReadOnlyList<string>?> TryListKeysFromApi(
        string prefix,
        int limit,
        CancellationToken cancellationToken)
    {
        var prefixParam = Uri.EscapeDataString(BuildKey(prefix));
        var response = await _client.GetAsync($"kv/keys?prefix={prefixParam}&limit={limit}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, "LIST", "_keys", cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<ToskaStoreKeysResponse>(_jsonOptions, cancellationToken);
        if (payload?.Keys is null)
        {
            return Array.Empty<string>();
        }

        return payload.Keys
            .Select(RemovePrefix)
            .Where(key => !string.IsNullOrEmpty(key))
            .ToList();
    }

    private async Task UpdateIndexAsync(string key, bool add, CancellationToken cancellationToken)
    {
        if (!_options.EnableKeyIndex || string.Equals(key, _indexKey, StringComparison.Ordinal))
        {
            return;
        }

        var current = await GetIndexAsync(cancellationToken);
        var updated = new HashSet<string>(current, StringComparer.Ordinal);
        if (add)
        {
            updated.Add(key);
        }
        else
        {
            updated.Remove(key);
        }

        var serialized = JsonSerializer.Serialize(updated.ToList(), _jsonOptions);
        await PutValueAsync(_indexKey, serialized, null, cancellationToken);
    }

    private string BuildKeyPath(string key) => $"kv/{Uri.EscapeDataString(BuildKey(key))}";

    private string BuildKey(string key) => $"{_keyPrefix}{key}";

    private string RemovePrefix(string key) => key.StartsWith(_keyPrefix, StringComparison.Ordinal)
        ? key[_keyPrefix.Length..]
        : key;

    private static string BuildPrefix(ToskaStoreKeyValueOptions options, MeshServiceOptions serviceOptions)
    {
        var prefix = options.KeyPrefix;
        if (string.IsNullOrWhiteSpace(prefix))
        {
            prefix = serviceOptions.ServiceName;
        }

        if (!prefix.EndsWith(":", StringComparison.Ordinal))
        {
            prefix += ":";
        }

        return prefix;
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        string key,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"ToskaStore {operation} failed for '{key}': {(int)response.StatusCode} {body}");
    }

    private sealed record ToskaStoreGetResponse(string Key, string? Value);

    private sealed record ToskaStorePutRequest(
        string Value,
        [property: JsonPropertyName("ttl_ms")] long? TtlMs);

    private sealed record ToskaStoreMgetRequest(IEnumerable<string> Keys);

    private sealed record ToskaStoreMgetResponse(Dictionary<string, string?> Values);

    private sealed record ToskaStoreKeysResponse(IReadOnlyList<string> Keys);
}

/// <summary>
/// Options for configuring the ToskaStore-backed key/value store.
/// </summary>
public class ToskaStoreKeyValueOptions
{
    public string BaseUrl { get; set; } = "http://localhost:4000";
    public string? AuthToken { get; set; }
    public string? KeyPrefix { get; set; }
    public bool EnableKeyIndex { get; set; }
    public string? KeyIndexKey { get; set; }
}

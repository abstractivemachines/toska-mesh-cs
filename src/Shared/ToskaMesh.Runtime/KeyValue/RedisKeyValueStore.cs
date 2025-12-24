using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace ToskaMesh.Runtime;

/// <summary>
/// Redis-backed key/value store for mesh services.
/// </summary>
public class RedisKeyValueStore : IKeyValueStore
{
    private readonly IConnectionMultiplexer _connection;
    private readonly IDatabase _database;
    private readonly string _keyPrefix;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public RedisKeyValueStore(IConnectionMultiplexer connection, RedisKeyValueOptions options, MeshServiceOptions serviceOptions)
    {
        _connection = connection;
        _database = connection.GetDatabase();
        _keyPrefix = BuildPrefix(options, serviceOptions);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        var serialized = JsonSerializer.Serialize(value, _jsonOptions);
        var redisKey = BuildKey(key);
        await _database.StringSetAsync(redisKey, serialized, ttl);
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var redisKey = BuildKey(key);
        var value = await _database.StringGetAsync(redisKey);
        if (value.IsNullOrEmpty)
        {
            return default;
        }

        var json = value.ToString();
        return string.IsNullOrEmpty(json) ? default : JsonSerializer.Deserialize<T>(json, _jsonOptions);
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var redisKey = BuildKey(key);
        return await _database.KeyDeleteAsync(redisKey);
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(string prefix = "", int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var server = GetServer();
        var pattern = BuildKey($"{prefix}*");
        var keys = new List<string>();

        await foreach (var key in server.KeysAsync(pattern: pattern, pageSize: pageSize))
        {
            keys.Add(RemovePrefix(key.ToString()));
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        return keys;
    }

    public async Task<IReadOnlyList<T>> ListAsync<T>(string prefix = "", int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var result = new List<T>();
        var keys = await ListKeysAsync(prefix, pageSize, cancellationToken);

        foreach (var key in keys)
        {
            var value = await GetAsync<T>(key, cancellationToken);
            if (value is not null)
            {
                result.Add(value);
            }
        }

        return result;
    }

    private string BuildKey(string key) => $"{_keyPrefix}{key}";

    private string RemovePrefix(string key) => key.StartsWith(_keyPrefix, StringComparison.Ordinal)
        ? key[_keyPrefix.Length..]
        : key;

    private IServer GetServer()
    {
        var endpoints = _connection.GetEndPoints();
        var endpoint = endpoints.FirstOrDefault() ?? throw new InvalidOperationException("No Redis endpoints available.");
        return _connection.GetServer(endpoint);
    }

    private static string BuildPrefix(RedisKeyValueOptions options, MeshServiceOptions serviceOptions)
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
}

/// <summary>
/// Options for configuring the Redis-backed key/value store.
/// </summary>
public class RedisKeyValueOptions
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public string? KeyPrefix { get; set; }
    public int? Database { get; set; }
}

public static class MeshKeyValueServiceCollectionExtensions
{
    /// <summary>
    /// Adds a Redis-backed key/value store for mesh services.
    /// </summary>
    public static IServiceCollection AddMeshKeyValueStore(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<RedisKeyValueOptions>? configure = null)
    {
        var options = configuration.GetSection("Mesh:KeyValue:Redis").Get<RedisKeyValueOptions>() ?? new RedisKeyValueOptions();
        configure?.Invoke(options);

        var connectionString = ApplyDatabase(options.ConnectionString, options.Database);

        services.AddSingleton(options);
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
        services.AddSingleton<IKeyValueStore>(sp =>
        {
            var opts = sp.GetRequiredService<RedisKeyValueOptions>();
            var meshOptions = sp.GetRequiredService<MeshServiceOptions>();
            var connection = sp.GetRequiredService<IConnectionMultiplexer>();
            return new RedisKeyValueStore(connection, opts, meshOptions);
        });

        return services;
    }

    private static string ApplyDatabase(string connectionString, int? database)
    {
        if (!database.HasValue)
        {
            return connectionString;
        }

        if (connectionString.Contains("defaultDatabase", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        return $"{connectionString},defaultDatabase={database.Value}";
    }
}

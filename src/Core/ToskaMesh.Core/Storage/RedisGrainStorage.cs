using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Hosting;
using Orleans.Runtime;
using Orleans.Storage;
using StackExchange.Redis;

namespace ToskaMesh.Core.Storage;

/// <summary>
/// Redis-backed grain storage with optimistic concurrency (ETag) and no TTL/eviction.
/// </summary>
public sealed class RedisGrainStorage : IGrainStorage, IAsyncDisposable, IDisposable
{
    private const string EtagField = "etag";
    private const string StateField = "state";

    private readonly string _name;
    private readonly string _keyPrefix;
    private readonly IConnectionMultiplexer _connection;
    private readonly IDatabase _database;
    private readonly ILogger<RedisGrainStorage> _logger;
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private const string WriteScript = @"
local current = redis.call('HGET', KEYS[1], 'etag')
if current ~= false and ARGV[1] ~= '' and current ~= ARGV[1] then
    return 0
end
redis.call('HSET', KEYS[1], 'etag', ARGV[2], 'state', ARGV[3])
return 1";

    private const string ClearScript = @"
local current = redis.call('HGET', KEYS[1], 'etag')
if current ~= false and ARGV[1] ~= '' and current ~= ARGV[1] then
    return 0
end
redis.call('DEL', KEYS[1])
return 1";

    public RedisGrainStorage(
        string name,
        RedisGrainStorageOptions options,
        ILogger<RedisGrainStorage> logger)
    {
        _name = name;
        _logger = logger;

        var normalized = Normalize(options);
        _keyPrefix = normalized.KeyPrefix!;
        _connection = ConnectionMultiplexer.Connect(ApplyDatabase(normalized.ConnectionString, normalized.Database));
        _database = _connection.GetDatabase();
    }

    public async Task ReadStateAsync<T>(string grainType, GrainId grainId, IGrainState<T> grainState)
    {
        var key = BuildKey(grainType, grainId);
        var entries = await _database.HashGetAllAsync(key);

        if (entries.Length == 0)
        {
            grainState.RecordExists = false;
            grainState.ETag = null;
            grainState.State ??= Activator.CreateInstance<T>();
            return;
        }

        var map = entries.ToDictionary(e => e.Name, e => e.Value);
        var etag = map.TryGetValue(EtagField, out var etagValue) ? (string?)etagValue : null;
        var stateValue = map.TryGetValue(StateField, out var state) ? state : RedisValue.Null;

        if (!stateValue.IsNullOrEmpty)
        {
            var deserialized = JsonSerializer.Deserialize<T>(stateValue!, _serializerOptions);
            grainState.State = deserialized ?? Activator.CreateInstance<T>();
        }

        grainState.ETag = etag;
        grainState.RecordExists = true;
    }

    public async Task WriteStateAsync<T>(string grainType, GrainId grainId, IGrainState<T> grainState)
    {
        var key = BuildKey(grainType, grainId);
        var expectedEtag = grainState.ETag ?? string.Empty;
        var newEtag = Guid.NewGuid().ToString("N");
        var payload = JsonSerializer.Serialize(grainState.State, _serializerOptions);

        var result = (int)(long)await _database.ScriptEvaluateAsync(
            WriteScript,
            new RedisKey[] { key },
            new RedisValue[] { expectedEtag, newEtag, payload });

        if (result == 0)
        {
            throw new InconsistentStateException(
                $"ETag mismatch for grain {grainId.ToString()} in storage '{_name}'. Expected '{expectedEtag}'.");
        }

        grainState.ETag = newEtag;
        grainState.RecordExists = true;
    }

    public async Task ClearStateAsync<T>(string grainType, GrainId grainId, IGrainState<T> grainState)
    {
        var key = BuildKey(grainType, grainId);
        var expectedEtag = grainState.ETag ?? string.Empty;

        var result = (int)(long)await _database.ScriptEvaluateAsync(
            ClearScript,
            new RedisKey[] { key },
            new RedisValue[] { expectedEtag });

        if (result == 0)
        {
            throw new InconsistentStateException(
                $"ETag mismatch when clearing grain {grainId.ToString()} in storage '{_name}'. Expected '{expectedEtag}'.");
        }

        grainState.ETag = null;
        grainState.RecordExists = false;
        grainState.State = Activator.CreateInstance<T>();
    }

    private string BuildKey(string grainType, GrainId grainId) =>
        $"{_keyPrefix}{grainType}:{grainId.ToString()}";

    private static RedisGrainStorageOptions Normalize(RedisGrainStorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException("Redis grain storage requires a connection string.");
        }

        options.KeyPrefix ??= "orleans:grain:";

        if (!options.KeyPrefix.EndsWith(":", StringComparison.Ordinal))
        {
            options.KeyPrefix += ":";
        }

        return options;
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

    public async ValueTask DisposeAsync()
    {
        await _connection.CloseAsync();
        _connection.Dispose();
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}

/// <summary>
/// Options for Redis-backed grain storage.
/// </summary>
public sealed class RedisGrainStorageOptions
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public int? Database { get; set; }
    public string? KeyPrefix { get; set; }
}

public static class RedisGrainStorageSiloBuilderExtensions
{
    public static ISiloBuilder AddRedisGrainStorage(
        this ISiloBuilder builder,
        string name,
        Action<RedisGrainStorageOptions>? configureOptions = null)
    {
        builder.ConfigureServices(services =>
        {
            if (configureOptions is not null)
            {
                services.Configure(name, configureOptions);
            }
        });

        builder.ConfigureServices(services =>
        {
            services.AddKeyedSingleton<IGrainStorage>(name, (serviceProvider, key) =>
            {
                var providerName = key as string ?? name;
                var options = serviceProvider.GetRequiredService<IOptionsMonitor<RedisGrainStorageOptions>>().Get(providerName);
                var logger = serviceProvider.GetRequiredService<ILogger<RedisGrainStorage>>();
                return new RedisGrainStorage(providerName, options, logger);
            });
        });

        return builder;
    }

    public static ISiloBuilder AddRedisGrainStorageAsDefault(
        this ISiloBuilder builder,
        Action<RedisGrainStorageOptions>? configureOptions = null)
    {
        return builder.AddRedisGrainStorage("Default", configureOptions);
    }
}

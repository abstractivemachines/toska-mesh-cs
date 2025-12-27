using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using ToskaMesh.Security;

namespace ToskaMesh.Telemetry.Tracing;

public sealed class TracingIngestExporter : BaseExporter<Activity>
{
    public const string HttpClientName = "MeshTracingIngest";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly TracingIngestExporterOptions _options;
    private readonly IMeshServiceTokenProvider? _tokenProvider;
    private readonly ILogger<TracingIngestExporter> _logger;
    private readonly Uri _endpoint;

    public TracingIngestExporter(
        IHttpClientFactory httpClientFactory,
        TracingIngestExporterOptions options,
        ILogger<TracingIngestExporter> logger,
        IMeshServiceTokenProvider? tokenProvider = null)
    {
        _httpClient = httpClientFactory.CreateClient(HttpClientName);
        _options = options;
        _logger = logger;
        _tokenProvider = tokenProvider;

        if (options.UseMeshServiceAuth && _tokenProvider is null)
        {
            throw new InvalidOperationException("Mesh service auth is enabled for trace ingest, but no token provider is registered.");
        }

        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint))
        {
            throw new InvalidOperationException($"Tracing ingest endpoint '{options.Endpoint}' is not a valid absolute URL.");
        }

        _endpoint = endpoint;
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        if (!_options.Enabled || batch.Count == 0)
        {
            return ExportResult.Success;
        }

        var spanCapacity = batch.Count > int.MaxValue ? int.MaxValue : (int)batch.Count;
        var spans = new List<TracingSpanDto>(spanCapacity);
        foreach (var activity in batch)
        {
            if (activity == null)
            {
                continue;
            }

            spans.Add(MapSpan(activity));
        }

        if (spans.Count == 0)
        {
            return ExportResult.Success;
        }

        var payload = new TracingIngestRequest
        {
            Spans = spans,
            Collector = _options.Collector
        };

        try
        {
            var json = JsonSerializer.Serialize(payload, SerializerOptions);
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            if (_options.UseMeshServiceAuth && _tokenProvider != null)
            {
                var token = _tokenProvider.GetTokenAsync().GetAwaiter().GetResult();
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var suppressScope = SuppressInstrumentationScope.Begin();
            using var response = _httpClient.Send(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Tracing ingest failed with status code {StatusCode}", response.StatusCode);
                return ExportResult.Failure;
            }

            return ExportResult.Success;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tracing ingest exporter failed");
            return ExportResult.Failure;
        }
    }

    private TracingSpanDto MapSpan(Activity activity)
    {
        var traceId = activity.TraceId.ToHexString();
        var spanId = activity.SpanId.ToHexString();
        var parentSpanId = activity.ParentSpanId == default ? null : activity.ParentSpanId.ToHexString();
        var start = activity.StartTimeUtc;
        var end = activity.StartTimeUtc.Add(activity.Duration);

        return new TracingSpanDto
        {
            TraceId = traceId,
            SpanId = spanId,
            ParentSpanId = parentSpanId,
            ServiceName = _options.ServiceName,
            OperationName = activity.DisplayName,
            StartTime = new DateTimeOffset(start, TimeSpan.Zero),
            EndTime = new DateTimeOffset(end, TimeSpan.Zero),
            Status = activity.Status.ToString(),
            Kind = activity.Kind.ToString(),
            CorrelationId = traceId,
            Attributes = ToStringDictionary(activity.TagObjects),
            ResourceAttributes = new Dictionary<string, string?>
            {
                ["service.name"] = _options.ServiceName,
                ["service.version"] = _options.ServiceVersion
            }
        };
    }

    private static IReadOnlyDictionary<string, string?>? ToStringDictionary(IEnumerable<KeyValuePair<string, object?>>? tags)
    {
        if (tags == null)
        {
            return null;
        }

        Dictionary<string, string?>? result = null;
        foreach (var tag in tags)
        {
            result ??= new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            result[tag.Key] = tag.Value?.ToString();
        }

        return result;
    }

    private sealed record TracingIngestRequest
    {
        public IReadOnlyCollection<TracingSpanDto> Spans { get; init; } = Array.Empty<TracingSpanDto>();
        public string? Collector { get; init; }
        public string? CorrelationId { get; init; }
    }

    private sealed record TracingSpanDto
    {
        public string TraceId { get; init; } = string.Empty;
        public string SpanId { get; init; } = string.Empty;
        public string? ParentSpanId { get; init; }
        public string ServiceName { get; init; } = string.Empty;
        public string OperationName { get; init; } = string.Empty;
        public DateTimeOffset StartTime { get; init; }
        public DateTimeOffset EndTime { get; init; }
        public string Status { get; init; } = "Unset";
        public string? Kind { get; init; }
        public string? CorrelationId { get; init; }
        public IReadOnlyDictionary<string, string?>? Attributes { get; init; }
        public IReadOnlyDictionary<string, string?>? Events { get; init; }
        public IReadOnlyDictionary<string, string?>? ResourceAttributes { get; init; }
        public double? CpuUsage { get; init; }
        public double? MemoryUsageMb { get; init; }
    }
}

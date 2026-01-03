using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ToskaMesh.TracingService.Data;
using Microsoft.Extensions.Options;
using ToskaMesh.TracingService.Entities;
using ToskaMesh.TracingService.Models;

namespace ToskaMesh.TracingService.Services;

public interface ITraceStorageService
{
    Task IngestAsync(TraceIngestRequest request, CancellationToken cancellationToken);
    Task<TraceQueryResponse> QueryAsync(TraceQueryParameters query, CancellationToken cancellationToken);
    Task<TraceDetailDto?> GetTraceAsync(string traceId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TraceSummaryDto>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<string>> GetDistinctServiceNamesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<string>> GetDistinctOperationsAsync(string serviceName, CancellationToken cancellationToken);
}

public class TraceStorageService : ITraceStorageService
{
    private readonly TracingDbContext _dbContext;
    private readonly TraceQueryDefaultsOptions _queryDefaults;
    private static readonly string[] BuiltInNamespaces = { "toskamesh", "toskamesh-example", "toskamesh-infra" };
    private static readonly string[] BuiltInNamespacesLower = BuiltInNamespaces
        .Select(name => name.ToLowerInvariant())
        .ToArray();
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public TraceStorageService(
        TracingDbContext dbContext,
        IOptions<TraceQueryDefaultsOptions> queryDefaults)
    {
        _dbContext = dbContext;
        _queryDefaults = queryDefaults.Value ?? new TraceQueryDefaultsOptions();
    }

    public async Task IngestAsync(TraceIngestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Spans == null || request.Spans.Count == 0)
        {
            return;
        }

        var spanEntities = new List<TraceSpan>(request.Spans.Count);
        var traceIds = request.Spans.Select(span => span.TraceId).Distinct().ToList();

        var existingKeys = await _dbContext.TraceSpans
            .Where(span => traceIds.Contains(span.TraceId))
            .Select(span => new { span.TraceId, span.SpanId })
            .ToListAsync(cancellationToken);

        var existing = new HashSet<string>(
            existingKeys.Select(span => $"{span.TraceId}:{span.SpanId}"),
            StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var span in request.Spans)
        {
            var key = $"{span.TraceId}:{span.SpanId}";
            if (!seen.Add(key) || existing.Contains(key))
            {
                continue;
            }

            spanEntities.Add(new TraceSpan
            {
                TraceId = span.TraceId,
                SpanId = span.SpanId,
                ParentSpanId = span.ParentSpanId,
                ServiceName = span.ServiceName,
                OperationName = span.OperationName,
                StartTimeUtc = span.StartTime.UtcDateTime,
                EndTimeUtc = span.EndTime.UtcDateTime,
                DurationMs = (span.EndTime - span.StartTime).TotalMilliseconds,
                Status = string.IsNullOrWhiteSpace(span.Status) ? "Unset" : span.Status,
                Kind = span.Kind,
                CorrelationId = span.CorrelationId ?? request.CorrelationId,
                CpuUsage = span.CpuUsage,
                MemoryUsageMb = span.MemoryUsageMb,
                AttributesJson = SerializeDictionary(span.Attributes),
                EventsJson = SerializeDictionary(span.Events),
                ResourceAttributesJson = SerializeDictionary(span.ResourceAttributes)
            });
        }

        if (spanEntities.Count == 0)
        {
            return;
        }

        await _dbContext.TraceSpans.AddRangeAsync(spanEntities, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TraceQueryResponse> QueryAsync(TraceQueryParameters query, CancellationToken cancellationToken)
    {
        var filter = ApplySummaryFilters(_dbContext.TraceSummaries.AsNoTracking(), query, _queryDefaults);

        var page = Math.Max(query.Page, 1);
        var take = Math.Clamp(query.PageSize, 1, 500);
        var skip = (page - 1) * take;

        var summaries = await filter
            .OrderByDescending(summary => summary.EndTimeUtc)
            .Skip(skip)
            .Take(take)
            .Select(summary => new TraceSummaryDto(
                summary.TraceId,
                summary.ServiceName,
                summary.OperationName,
                summary.StartTimeUtc,
                summary.EndTimeUtc,
                summary.DurationMs,
                summary.Status,
                summary.SpanCount,
                summary.CorrelationId))
            .ToListAsync(cancellationToken);

        var includeTotal = query.IncludeTotal || _queryDefaults.IncludeTotalCounts;
        int? total = null;
        if (includeTotal)
        {
            if (page == 1 && summaries.Count < take)
            {
                total = summaries.Count;
            }
            else if (_queryDefaults.TotalTimeoutSeconds > 0)
            {
                using var totalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                totalCts.CancelAfter(TimeSpan.FromSeconds(_queryDefaults.TotalTimeoutSeconds));
                try
                {
                    total = await filter.CountAsync(totalCts.Token);
                }
                catch (OperationCanceledException) when (totalCts.IsCancellationRequested)
                {
                    total = null;
                }
            }
            else
            {
                total = await filter.CountAsync(cancellationToken);
            }
        }

        var resolvedTotal = total ?? skip + summaries.Count;

        if (summaries.Count == 0)
        {
            return new TraceQueryResponse(resolvedTotal, page, take, Array.Empty<TraceSummaryDto>());
        }

        return new TraceQueryResponse(resolvedTotal, page, take, summaries);
    }

    public async Task<TraceDetailDto?> GetTraceAsync(string traceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(traceId))
        {
            throw new ArgumentException("Trace identifier is required.", nameof(traceId));
        }

        var spans = await _dbContext.TraceSpans.AsNoTracking()
            .Where(span => span.TraceId == traceId)
            .OrderBy(span => span.StartTimeUtc)
            .ToListAsync(cancellationToken);

        if (spans.Count == 0)
        {
            return null;
        }

        var summary = new TraceSummaryDto(
            traceId,
            spans.First().ServiceName,
            spans.First().OperationName,
            spans.First().StartTimeUtc,
            spans.Last().EndTimeUtc,
            spans.Last().EndTimeUtc.Subtract(spans.First().StartTimeUtc).TotalMilliseconds,
            spans.First().Status,
            spans.Count,
            spans.First().CorrelationId);

        return new TraceDetailDto(summary, spans.Select(ToDto).ToList());
    }

    public async Task<IReadOnlyCollection<TraceSummaryDto>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return Array.Empty<TraceSummaryDto>();
        }

        var items = await _dbContext.TraceSpans.AsNoTracking()
            .Where(span => span.CorrelationId == correlationId)
            .GroupBy(span => span.TraceId)
            .Select(group => new TraceSummaryDto(
                group.Key,
                group.OrderBy(span => span.StartTimeUtc).Select(span => span.ServiceName).FirstOrDefault() ?? string.Empty,
                group.OrderBy(span => span.StartTimeUtc).Select(span => span.OperationName).FirstOrDefault() ?? string.Empty,
                group.Min(span => span.StartTimeUtc),
                group.Max(span => span.EndTimeUtc),
                group.Max(span => span.EndTimeUtc).Subtract(group.Min(span => span.StartTimeUtc)).TotalMilliseconds,
                group.OrderBy(span => span.StartTimeUtc).Select(span => span.Status).FirstOrDefault() ?? "Unset",
                group.Count(),
                correlationId))
            .ToListAsync(cancellationToken);

        return items;
    }

    public async Task<IReadOnlyCollection<string>> GetDistinctServiceNamesAsync(CancellationToken cancellationToken)
    {
        var serviceNames = await _dbContext.TraceSummaries.AsNoTracking()
            .Select(summary => summary.ServiceName)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);

        return serviceNames;
    }

    public async Task<IReadOnlyCollection<string>> GetDistinctOperationsAsync(string serviceName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return Array.Empty<string>();
        }

        var operations = await _dbContext.TraceSummaries.AsNoTracking()
            .Where(summary => summary.ServiceName == serviceName)
            .Select(summary => summary.OperationName)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync(cancellationToken);

        return operations;
    }

    private static IQueryable<TraceSummary> ApplySummaryFilters(
        IQueryable<TraceSummary> query,
        TraceQueryParameters parameters,
        TraceQueryDefaultsOptions defaults)
    {
        if (!string.IsNullOrWhiteSpace(parameters.ServiceName))
        {
            var serviceNameFilter = parameters.ServiceName.ToLowerInvariant();
            query = query.Where(summary => summary.ServiceName.ToLower().Contains(serviceNameFilter));
        }

        if (parameters.ExcludeBuiltInServices)
        {
            query = query.Where(summary =>
                summary.ServiceNamespace == null ||
                !BuiltInNamespacesLower.Contains(summary.ServiceNamespace.ToLower()));
        }

        if (!string.IsNullOrWhiteSpace(parameters.OperationName))
        {
            query = query.Where(summary => summary.OperationName == parameters.OperationName);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Status))
        {
            query = query.Where(summary => summary.Status == parameters.Status);
        }

        if (!string.IsNullOrWhiteSpace(parameters.CorrelationId))
        {
            query = query.Where(summary => summary.CorrelationId == parameters.CorrelationId);
        }

        if (defaults.EnableDefaultLookback && parameters.From is null && parameters.To is null)
        {
            var toUtc = DateTime.UtcNow;
            var fromUtc = toUtc.AddHours(-Math.Max(1, defaults.DefaultLookbackHours));
            query = query.Where(summary => summary.EndTimeUtc >= fromUtc && summary.StartTimeUtc <= toUtc);
        }
        else
        {
            if (parameters.From is not null)
            {
                var fromUtc = parameters.From.Value.UtcDateTime;
                query = query.Where(summary => summary.StartTimeUtc >= fromUtc);
            }

            if (parameters.To is not null)
            {
                var toUtc = parameters.To.Value.UtcDateTime;
                query = query.Where(summary => summary.EndTimeUtc <= toUtc);
            }
        }

        if (parameters.MinDurationMs is not null)
        {
            query = query.Where(summary => summary.DurationMs >= parameters.MinDurationMs.Value);
        }

        if (parameters.MaxDurationMs is not null)
        {
            query = query.Where(summary => summary.DurationMs <= parameters.MaxDurationMs.Value);
        }

        return query;
    }

    private static TraceSpanDto ToDto(TraceSpan span)
    {
        return new TraceSpanDto
        {
            TraceId = span.TraceId,
            SpanId = span.SpanId,
            ParentSpanId = span.ParentSpanId,
            ServiceName = span.ServiceName,
            OperationName = span.OperationName,
            StartTime = ToOffset(span.StartTimeUtc),
            EndTime = ToOffset(span.EndTimeUtc),
            Status = span.Status,
            Kind = span.Kind,
            CorrelationId = span.CorrelationId,
            Attributes = DeserializeDictionary(span.AttributesJson),
            Events = DeserializeDictionary(span.EventsJson),
            ResourceAttributes = DeserializeDictionary(span.ResourceAttributesJson),
            CpuUsage = span.CpuUsage,
            MemoryUsageMb = span.MemoryUsageMb
        };
    }

    private static DateTimeOffset ToOffset(DateTime value)
    {
        var utc = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return new DateTimeOffset(utc);
    }

    private static string? SerializeDictionary(IReadOnlyDictionary<string, string?>? value) =>
        value is null || value.Count == 0 ? null : JsonSerializer.Serialize(value, SerializerOptions);

    private static IReadOnlyDictionary<string, string?>? DeserializeDictionary(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string?>>(json, SerializerOptions);
}

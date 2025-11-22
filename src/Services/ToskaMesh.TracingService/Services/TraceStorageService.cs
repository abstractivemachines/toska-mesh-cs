using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ToskaMesh.TracingService.Data;
using ToskaMesh.TracingService.Entities;
using ToskaMesh.TracingService.Models;

namespace ToskaMesh.TracingService.Services;

public interface ITraceStorageService
{
    Task IngestAsync(TraceIngestRequest request, CancellationToken cancellationToken);
    Task<TraceQueryResponse> QueryAsync(TraceQueryParameters query, CancellationToken cancellationToken);
    Task<TraceDetailDto?> GetTraceAsync(string traceId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TraceSummaryDto>> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken);
}

public class TraceStorageService : ITraceStorageService
{
    private readonly TracingDbContext _dbContext;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public TraceStorageService(TracingDbContext dbContext)
    {
        _dbContext = dbContext;
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

        var existing = await _dbContext.TraceSpans
            .Where(span => traceIds.Contains(span.TraceId))
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            _dbContext.TraceSpans.RemoveRange(existing);
        }

        foreach (var span in request.Spans)
        {
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

        await _dbContext.TraceSpans.AddRangeAsync(spanEntities, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TraceQueryResponse> QueryAsync(TraceQueryParameters query, CancellationToken cancellationToken)
    {
        var filter = ApplyFilters(_dbContext.TraceSpans.AsNoTracking(), query);

        var total = await filter
            .Select(span => span.TraceId)
            .Distinct()
            .CountAsync(cancellationToken);

        var page = Math.Max(query.Page, 1);
        var take = Math.Clamp(query.PageSize, 1, 500);
        var skip = (page - 1) * take;

        var orderedTraceIds = await filter
            .GroupBy(span => span.TraceId)
            .OrderByDescending(group => group.Max(span => span.EndTimeUtc))
            .Skip(skip)
            .Take(take)
            .Select(group => group.Key)
            .ToListAsync(cancellationToken);

        if (orderedTraceIds.Count == 0)
        {
            return new TraceQueryResponse(total, page, take, Array.Empty<TraceSummaryDto>());
        }

        var orderLookup = orderedTraceIds
            .Select((traceId, index) => new { traceId, index })
            .ToDictionary(x => x.traceId, x => x.index);

        var spans = await _dbContext.TraceSpans.AsNoTracking()
            .Where(span => orderedTraceIds.Contains(span.TraceId))
            .ToListAsync(cancellationToken);

        var groupedSummaries = spans
            .GroupBy(span => span.TraceId)
            .Select(group =>
            {
                var ordered = group.OrderBy(span => span.StartTimeUtc).ToList();
                var start = ordered.First().StartTimeUtc;
                var end = ordered.Last().EndTimeUtc;
                var duration = (end - start).TotalMilliseconds;
                return new TraceSummaryDto(
                    group.Key,
                    ordered.First().ServiceName,
                    ordered.First().OperationName,
                    start,
                    end,
                    duration,
                    ordered.First().Status,
                    ordered.Count,
                    ordered.First().CorrelationId);
            })
            .OrderBy(summary => orderLookup[summary.TraceId])
            .ToList();

        return new TraceQueryResponse(total, page, take, groupedSummaries);
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

    private static IQueryable<TraceSpan> ApplyFilters(IQueryable<TraceSpan> query, TraceQueryParameters parameters)
    {
        if (!string.IsNullOrWhiteSpace(parameters.ServiceName))
        {
            query = query.Where(span => span.ServiceName == parameters.ServiceName);
        }

        if (!string.IsNullOrWhiteSpace(parameters.OperationName))
        {
            query = query.Where(span => span.OperationName == parameters.OperationName);
        }

        if (!string.IsNullOrWhiteSpace(parameters.Status))
        {
            query = query.Where(span => span.Status == parameters.Status);
        }

        if (!string.IsNullOrWhiteSpace(parameters.CorrelationId))
        {
            query = query.Where(span => span.CorrelationId == parameters.CorrelationId);
        }

        if (parameters.From is not null)
        {
            var fromUtc = parameters.From.Value.UtcDateTime;
            query = query.Where(span => span.StartTimeUtc >= fromUtc);
        }

        if (parameters.To is not null)
        {
            var toUtc = parameters.To.Value.UtcDateTime;
            query = query.Where(span => span.EndTimeUtc <= toUtc);
        }

        if (parameters.MinDurationMs is not null)
        {
            query = query.Where(span => span.DurationMs >= parameters.MinDurationMs.Value);
        }

        if (parameters.MaxDurationMs is not null)
        {
            query = query.Where(span => span.DurationMs <= parameters.MaxDurationMs.Value);
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

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ToskaMesh.TracingService.Data;
using ToskaMesh.TracingService.Entities;
using ToskaMesh.TracingService.Models;
using ToskaMesh.TracingService.Services;
using Xunit;

namespace ToskaMesh.TracingService.Tests;

public class TraceAnalyticsServiceTests
{
    [Fact]
    public async Task GetPerformanceAsync_ComputesAggregates()
    {
        await using var context = CreateDbContext();

        context.TraceSpans.AddRange(new[]
        {
            CreateSpan("trace-1", "span-1", "gateway", "HTTP GET /orders", "Ok", 100),
            CreateSpan("trace-2", "span-2", "gateway", "HTTP GET /orders", "Error", 300),
            CreateSpan("trace-3", "span-3", "inventory", "CheckStock", "Ok", 200)
        });
        await context.SaveChangesAsync();

        var service = new TraceAnalyticsService(context);
        var response = await service.GetPerformanceAsync(new TracePerformanceRequest(), CancellationToken.None);

        response.AverageDurationMs.Should().BeGreaterThan(0);
        response.P95DurationMs.Should().BeGreaterThan(0);
        response.ErrorRate.Should().BeGreaterThan(0);
        response.Services.Should().HaveCount(2);
        response.Hotspots.Should().NotBeEmpty();
    }

    private static TracingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TracingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TracingDbContext(options);
    }

    private static TraceSpan CreateSpan(string traceId, string spanId, string service, string operation, string status, double durationMs)
    {
        var start = DateTime.UtcNow.AddMilliseconds(-durationMs);
        var end = start.AddMilliseconds(durationMs);
        return new TraceSpan
        {
            TraceId = traceId,
            SpanId = spanId,
            ParentSpanId = null,
            ServiceName = service,
            OperationName = operation,
            StartTimeUtc = start,
            EndTimeUtc = end,
            DurationMs = durationMs,
            Status = status
        };
    }
}

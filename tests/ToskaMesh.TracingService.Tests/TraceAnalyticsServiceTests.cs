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

    [Fact]
    public async Task GetPerformanceAsync_CalculatesCorrectAverageDuration()
    {
        await using var context = CreateDbContext();

        context.TraceSpans.AddRange(new[]
        {
            CreateSpan("t1", "s1", "api", "Op", "Ok", 100),
            CreateSpan("t2", "s2", "api", "Op", "Ok", 200),
            CreateSpan("t3", "s3", "api", "Op", "Ok", 300)
        });
        await context.SaveChangesAsync();

        var service = new TraceAnalyticsService(context);
        var response = await service.GetPerformanceAsync(new TracePerformanceRequest(), CancellationToken.None);

        response.AverageDurationMs.Should().BeApproximately(200, 1);
    }

    [Fact]
    public async Task GetPerformanceAsync_CalculatesCorrectErrorRate()
    {
        await using var context = CreateDbContext();

        context.TraceSpans.AddRange(new[]
        {
            CreateSpan("t1", "s1", "api", "Op", "Ok", 100),
            CreateSpan("t2", "s2", "api", "Op", "Ok", 100),
            CreateSpan("t3", "s3", "api", "Op", "Error", 100),
            CreateSpan("t4", "s4", "api", "Op", "Ok", 100)
        });
        await context.SaveChangesAsync();

        var service = new TraceAnalyticsService(context);
        var response = await service.GetPerformanceAsync(new TracePerformanceRequest(), CancellationToken.None);

        response.ErrorRate.Should().BeApproximately(0.25, 0.01); // 1 error out of 4
    }

    [Fact]
    public async Task GetPerformanceAsync_FiltersByServiceName()
    {
        await using var context = CreateDbContext();

        context.TraceSpans.AddRange(new[]
        {
            CreateSpan("t1", "s1", "gateway", "Op", "Ok", 100),
            CreateSpan("t2", "s2", "gateway", "Op", "Error", 200),
            CreateSpan("t3", "s3", "orders", "Op", "Ok", 300),
            CreateSpan("t4", "s4", "orders", "Op", "Ok", 400)
        });
        await context.SaveChangesAsync();

        var service = new TraceAnalyticsService(context);
        var response = await service.GetPerformanceAsync(new TracePerformanceRequest
        {
            ServiceName = "gateway"
        }, CancellationToken.None);

        response.AverageDurationMs.Should().BeApproximately(150, 1);
        response.ErrorRate.Should().BeApproximately(0.5, 0.01);
    }

    [Fact]
    public async Task GetPerformanceAsync_ReturnsServiceBreakdown()
    {
        await using var context = CreateDbContext();

        context.TraceSpans.AddRange(new[]
        {
            CreateSpan("t1", "s1", "api", "Op1", "Ok", 100),
            CreateSpan("t2", "s2", "api", "Op2", "Ok", 200),
            CreateSpan("t3", "s3", "db", "Query", "Ok", 50),
            CreateSpan("t4", "s4", "db", "Query", "Error", 100)
        });
        await context.SaveChangesAsync();

        var service = new TraceAnalyticsService(context);
        var response = await service.GetPerformanceAsync(new TracePerformanceRequest(), CancellationToken.None);

        response.Services.Should().HaveCount(2);

        var apiService = response.Services.FirstOrDefault(s => s.ServiceName == "api");
        apiService.Should().NotBeNull();
        apiService!.AverageDurationMs.Should().BeApproximately(150, 1);
        apiService.ErrorRate.Should().Be(0);

        var dbService = response.Services.FirstOrDefault(s => s.ServiceName == "db");
        dbService.Should().NotBeNull();
        dbService!.ErrorRate.Should().BeApproximately(0.5, 0.01);
    }

    [Fact]
    public async Task GetPerformanceAsync_IdentifiesHotspots()
    {
        await using var context = CreateDbContext();

        // Create spans with different operations
        context.TraceSpans.AddRange(new[]
        {
            CreateSpan("t1", "s1", "api", "SlowOperation", "Ok", 500),
            CreateSpan("t2", "s2", "api", "SlowOperation", "Ok", 600),
            CreateSpan("t3", "s3", "api", "FastOperation", "Ok", 50),
            CreateSpan("t4", "s4", "api", "FastOperation", "Ok", 60)
        });
        await context.SaveChangesAsync();

        var service = new TraceAnalyticsService(context);
        var response = await service.GetPerformanceAsync(new TracePerformanceRequest(), CancellationToken.None);

        response.Hotspots.Should().NotBeEmpty();
        var slowOpHotspot = response.Hotspots.FirstOrDefault(h => h.OperationName == "SlowOperation");
        slowOpHotspot.Should().NotBeNull();
        slowOpHotspot!.AverageDurationMs.Should().BeGreaterThan(400);
    }

    [Fact]
    public async Task GetPerformanceAsync_ReturnsEmptyForNoData()
    {
        await using var context = CreateDbContext();
        var service = new TraceAnalyticsService(context);

        var response = await service.GetPerformanceAsync(new TracePerformanceRequest(), CancellationToken.None);

        response.AverageDurationMs.Should().Be(0);
        response.P95DurationMs.Should().Be(0);
        response.ErrorRate.Should().Be(0);
        response.Services.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPerformanceAsync_CalculatesP95Correctly()
    {
        await using var context = CreateDbContext();

        // Add 20 spans with durations 10, 20, 30, ..., 200
        for (int i = 1; i <= 20; i++)
        {
            context.TraceSpans.Add(CreateSpan($"t{i}", $"s{i}", "api", "Op", "Ok", i * 10));
        }
        await context.SaveChangesAsync();

        var service = new TraceAnalyticsService(context);
        var response = await service.GetPerformanceAsync(new TracePerformanceRequest(), CancellationToken.None);

        // P95 should be around the 95th percentile value (190 or close to it)
        response.P95DurationMs.Should().BeGreaterOrEqualTo(180);
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

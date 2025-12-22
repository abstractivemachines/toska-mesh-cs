using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ToskaMesh.MetricsService.Data;
using ToskaMesh.MetricsService.Models;
using ToskaMesh.MetricsService.Services;
using Xunit;

namespace ToskaMesh.MetricsService.Tests;

public class MetricHistoryServiceTests
{
    [Fact]
    public async Task RecordSampleAsync_PersistsSamplesAndAllowsQuery()
    {
        using var context = CreateDbContext();
        var service = new MetricHistoryService(context);

        await service.RecordSampleAsync(
            "mesh_requests_total",
            10,
            MetricType.Counter,
            new Dictionary<string, string> { { "route", "api" } },
            "requests",
            CancellationToken.None);

        await service.RecordSampleAsync(
            "mesh_requests_total",
            20,
            MetricType.Counter,
            new Dictionary<string, string> { { "route", "api" } },
            "requests",
            CancellationToken.None);

        var result = await service.QueryAsync(new MetricQuery
        {
            Name = "mesh_requests_total",
            Aggregation = MetricAggregation.Sum
        }, CancellationToken.None);

        result.Should().HaveCount(1);
        result.Single().Value.Should().Be(30);
    }

    [Fact]
    public async Task QueryAsync_WithAverageAggregation_ComputesCorrectAverage()
    {
        using var context = CreateDbContext();
        var service = new MetricHistoryService(context);

        await service.RecordSampleAsync("latency", 100, MetricType.Gauge, null, "ms", CancellationToken.None);
        await service.RecordSampleAsync("latency", 200, MetricType.Gauge, null, "ms", CancellationToken.None);
        await service.RecordSampleAsync("latency", 300, MetricType.Gauge, null, "ms", CancellationToken.None);

        var result = await service.QueryAsync(new MetricQuery
        {
            Name = "latency",
            Aggregation = MetricAggregation.Average
        }, CancellationToken.None);

        result.Should().HaveCount(1);
        result.Single().Value.Should().Be(200);
    }

    [Fact]
    public async Task QueryAsync_WithMinAggregation_ReturnsMinimumValue()
    {
        using var context = CreateDbContext();
        var service = new MetricHistoryService(context);

        await service.RecordSampleAsync("response_time", 50, MetricType.Histogram, null, "ms", CancellationToken.None);
        await service.RecordSampleAsync("response_time", 25, MetricType.Histogram, null, "ms", CancellationToken.None);
        await service.RecordSampleAsync("response_time", 75, MetricType.Histogram, null, "ms", CancellationToken.None);

        var result = await service.QueryAsync(new MetricQuery
        {
            Name = "response_time",
            Aggregation = MetricAggregation.Min
        }, CancellationToken.None);

        result.Should().HaveCount(1);
        result.Single().Value.Should().Be(25);
    }

    [Fact]
    public async Task QueryAsync_WithMaxAggregation_ReturnsMaximumValue()
    {
        using var context = CreateDbContext();
        var service = new MetricHistoryService(context);

        await service.RecordSampleAsync("cpu_usage", 45, MetricType.Gauge, null, "percent", CancellationToken.None);
        await service.RecordSampleAsync("cpu_usage", 92, MetricType.Gauge, null, "percent", CancellationToken.None);
        await service.RecordSampleAsync("cpu_usage", 67, MetricType.Gauge, null, "percent", CancellationToken.None);

        var result = await service.QueryAsync(new MetricQuery
        {
            Name = "cpu_usage",
            Aggregation = MetricAggregation.Max
        }, CancellationToken.None);

        result.Should().HaveCount(1);
        result.Single().Value.Should().Be(92);
    }

    [Fact]
    public async Task QueryAsync_WithLabelFilters_ReturnsMatchingSamplesOnly()
    {
        using var context = CreateDbContext();
        var service = new MetricHistoryService(context);

        await service.RecordSampleAsync("http_requests", 100, MetricType.Counter,
            new Dictionary<string, string> { { "method", "GET" }, { "path", "/api" } }, "count", CancellationToken.None);
        await service.RecordSampleAsync("http_requests", 50, MetricType.Counter,
            new Dictionary<string, string> { { "method", "POST" }, { "path", "/api" } }, "count", CancellationToken.None);
        await service.RecordSampleAsync("http_requests", 25, MetricType.Counter,
            new Dictionary<string, string> { { "method", "GET" }, { "path", "/health" } }, "count", CancellationToken.None);

        var result = await service.QueryAsync(new MetricQuery
        {
            Name = "http_requests",
            Labels = new Dictionary<string, string> { { "method", "GET" } },
            Aggregation = MetricAggregation.Sum
        }, CancellationToken.None);

        result.Should().HaveCount(1);
        result.Single().Value.Should().Be(125); // 100 + 25 (both GET requests)
    }

    [Fact]
    public async Task QueryAsync_WithNonExistentMetric_ReturnsEmpty()
    {
        using var context = CreateDbContext();
        var service = new MetricHistoryService(context);

        var result = await service.QueryAsync(new MetricQuery
        {
            Name = "nonexistent_metric",
            Aggregation = MetricAggregation.Sum
        }, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryAsync_WithTimeRange_FiltersCorrectly()
    {
        using var context = CreateDbContext();
        var service = new MetricHistoryService(context);

        await service.RecordSampleAsync("events", 10, MetricType.Counter, null, "count", CancellationToken.None);
        await service.RecordSampleAsync("events", 20, MetricType.Counter, null, "count", CancellationToken.None);

        var result = await service.QueryAsync(new MetricQuery
        {
            Name = "events",
            From = DateTime.UtcNow.AddMinutes(-5),
            To = DateTime.UtcNow.AddMinutes(5),
            Aggregation = MetricAggregation.Sum
        }, CancellationToken.None);

        result.Should().HaveCount(1);
        result.Single().Value.Should().Be(30);
    }

    private static MetricsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MetricsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MetricsDbContext(options);
    }
}

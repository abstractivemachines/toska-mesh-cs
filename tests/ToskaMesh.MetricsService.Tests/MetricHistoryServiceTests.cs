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

    private static MetricsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MetricsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MetricsDbContext(options);
    }
}

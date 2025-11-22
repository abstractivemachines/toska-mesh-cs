using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ToskaMesh.MetricsService.Data;
using ToskaMesh.MetricsService.Models;
using ToskaMesh.MetricsService.Services;
using Xunit;

namespace ToskaMesh.MetricsService.Tests;

public class AlertRuleServiceTests
{
    [Fact]
    public async Task EvaluateAsync_ReturnsTriggeredResult_WhenThresholdExceeded()
    {
        using var context = CreateDbContext();
        var history = new MetricHistoryService(context);
        var service = new AlertRuleService(context, history, NullLogger<AlertRuleService>.Instance);

        await history.RecordSampleAsync("mesh_latency_seconds", 0.5, MetricType.Histogram, null, "seconds", CancellationToken.None);
        await history.RecordSampleAsync("mesh_latency_seconds", 1.5, MetricType.Histogram, null, "seconds", CancellationToken.None);

        var rule = await service.CreateAsync(new CreateAlertRuleRequest
        {
            Name = "Latency high",
            MetricName = "mesh_latency_seconds",
            Aggregation = MetricAggregation.Max,
            Threshold = 1.0,
            Operator = AlertComparisonOperator.GreaterThan,
            WindowMinutes = 10
        }, CancellationToken.None);

        var result = await service.EvaluateAsync(rule.Id, CancellationToken.None);

        result.Triggered.Should().BeTrue();
        result.ObservedValue.Should().Be(1.5);
    }

    private static MetricsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MetricsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MetricsDbContext(options);
    }
}
